#!/usr/bin/env bash
set -euo pipefail

# -------- Config --------
APP="dotnet run --no-launch-profile"
A_URL="http://localhost:5001"
B_URL="http://localhost:5002"
LOG_A="nodeA.log"
LOG_B="nodeB.log"
DIFFICULTY=3

# -------- Helpers --------
die(){ echo "ERROR: $*" >&2; exit 1; }
need(){ command -v "$1" >/dev/null || die "Missing dependency: $1"; }
need jq
need curl

wait_for_http() { # url, timeout_s
  local url=$1; local t=${2:-20}; local i=0
  until curl -sf "$url/info" >/dev/null; do
    ((i++)); (( i>t )) && die "Timed out waiting for $url"
    sleep 1
  done
}

get_height(){ curl -s "$1/info" | jq -r '.height'; }
get_tip(){ curl -s "$1/info" | jq -r '.tipHash'; }
assert_eq(){ local exp=$1 act=$2 msg=$3; [[ "$act" == "$exp" ]] || die "$msg (expected=$exp, got=$act)"; }
assert_startswith(){ local s=$1 p=$2 msg=$3; [[ "$s" == "$p"* ]] || die "$msg (got=$s)"; }

pp(){ echo -e "\n=== $* ==="; }

# -------- Clean slate --------
pp "Cleaning old state / logs"
rm -f blockchain_state_5001.json blockchain_state_5002.json "$LOG_A" "$LOG_B"

# -------- Start nodes (background) --------
pp "Starting Node A ($A_URL)"
$APP --urls "$A_URL" >"$LOG_A" 2>&1 & A_PID=$!
pp "Starting Node B ($B_URL)"
$APP --urls "$B_URL" >"$LOG_B" 2>&1 & B_PID=$!

trap 'pp "Stopping nodes"; kill $A_PID $B_PID >/dev/null 2>&1 || true' EXIT

pp "Waiting for both nodes to be ready"
wait_for_http "$A_URL" 30
wait_for_http "$B_URL" 30

# -------- Health & genesis --------
pp "Health & genesis"
curl -s "$A_URL/info" | jq
curl -s "$B_URL/info" | jq

HA=$(get_height "$A_URL"); HB=$(get_height "$B_URL")
assert_eq 0 "$HA" "Node A not at genesis height"
assert_eq 0 "$HB" "Node B not at genesis height"

# Genesis allocations
GA=$(curl -s "$A_URL/chain" | jq '.[0].transactions')
echo "$GA" | jq

# Balances
curl -s "$A_URL/wallets/Alice/balance" | jq
curl -s "$A_URL/wallets/Bob/balance"   | jq

# -------- Tx on A & mine --------
pp "Create tx on A and mine"
curl -s -X POST "$A_URL/transactions/new" \
  -H "Content-Type: application/json" \
  -d '{"from":"Alice","to":"Bob","amount":2}' | jq

RES=$(curl -s -X POST "$A_URL/mine")
echo "$RES" | jq '.block.index, .block.hash'
LAST_HASH=$(echo "$RES" | jq -r '.block.hash')
assert_startswith "$LAST_HASH" $(printf "%0.s0" $(seq 1 $DIFFICULTY)) "PoW hash does not meet difficulty"

# B should have received the block
HB=$(get_height "$B_URL")
assert_eq 1 "$HB" "Node B did not receive broadcast (expected height 1)"

# Balances reflect tx + reward
curl -s "$A_URL/wallets/Alice/balance" | jq
curl -s "$A_URL/wallets/Bob/balance"   | jq
curl -s "$A_URL/wallets/my-miner-address/balance" | jq

# -------- Invalid announce should be rejected --------
pp "Invalid announce (tampered block) -> rejected"
BAD=$(curl -s "$A_URL/chain" | jq -c '.[-1] | .nonce = (.nonce + 1)')
RESP=$(curl -s -X POST "$B_URL/announce" -H "Content-Type: application/json" -d "$BAD")
echo "$RESP" | jq
echo "$RESP" | jq -r '.message' | grep -qi "rejected" || die "Invalid block was not rejected"

# -------- Make B longer, resolve on A --------
pp "Make B longer and resolve on A"
curl -s -X POST "$B_URL/transactions/new" -H "Content-Type: application/json" \
  -d '{"from":"Alice","to":"Bob","amount":1}' | jq

curl -s -X POST "$B_URL/mine" | jq '.block.index, .block.hash'
curl -s -X POST "$B_URL/mine" | jq '.block.index, .block.hash'

HB=$(get_height "$B_URL")
[[ "$HB" -ge 3 ]] || die "Node B did not reach expected height (>=3)"

curl -s "$A_URL/nodes/resolve" | jq
HA=$(get_height "$A_URL")
assert_eq "$HB" "$HA" "After resolve, A height != B height"

# -------- Persistence: restart A and verify height persists --------
pp "Restart Node A to verify persistence"
kill "$A_PID"
sleep 1
$APP --urls "$A_URL" >"$LOG_A" 2>&1 & A_PID=$!
wait_for_http "$A_URL" 30

HA2=$(get_height "$A_URL")
assert_eq "$HB" "$HA2" "After restart, Node A height did not persist"

pp "ALL CHECKS PASSED ✅"
