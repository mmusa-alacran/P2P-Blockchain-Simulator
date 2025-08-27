#!/usr/bin/env bash
set -euo pipefail

A="${A:-http://localhost:5001}"
B="${B:-http://localhost:5002}"

echo "Health:"
curl -s "$A/healthz" | jq
curl -s "$B/healthz" | jq

echo "Peers on A:"
curl -s "$A/peers" | jq

echo "Submit tx Alice->Bob:2 on A, then mine:"
curl -s -X POST "$A/transactions/new" -H 'Content-Type: application/json' -d '{"from":"Alice","to":"Bob","amount":2}' | jq
curl -s -X POST "$A/mine" | jq '.block.index,.block.hash'

echo "Chains:"
curl -s "$A/chain" | jq 'length'
curl -s "$B/chain" | jq 'length'

echo "Balances:"
for who in Alice Bob my-miner-address; do
  curl -s "$A/wallets/$who/balance" | jq
done
