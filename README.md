# P2P Blockchain Simulator (ASP.NET Core / .NET 8)

A compact, educational blockchain simulator with:

* **Minimal-API** HTTP node (mine, submit tx, inspect chain/wallets, manage peers)
* **P2P gossip** (broadcast mined blocks, longest-chain conflict resolution)
* **Deterministic PoW** (leading-zeros SHA-256, configurable difficulty)
* **Persistence** (state per HTTP port)
* **Health + readiness** endpoints and **Prometheus** metrics
* **Swagger UI** + a tiny **static HTML** tester
* **Docker**/**Compose** for multi-node demos
* **Integration & unit tests**

> ⚠️ Educational only. No signatures, mempool fees, or real security. Don’t use for real funds.

---

## Contents

* [Architecture & Components](#architecture--components)
* [Folder Structure](#folder-structure)
* [Configuration](#configuration)
* [Build & Run (local)](#build--run-local)
* [Docker / Compose (two or more nodes)](#docker--compose-two-or-more-nodes)
* [Expose nodes to the Internet with ngrok (remote demo)](#expose-nodes-to-the-internet-with-ngrok-remote-demo)
* [API Endpoints](#api-endpoints)
* [Prometheus Metrics](#prometheus-metrics)
* [Health / Readiness](#health--readiness)
* [Tests](#tests)
* [Design Notes (what each class does)](#design-notes-what-each-class-does)
* [Troubleshooting](#troubleshooting)

---

## Architecture & Components

**ASP.NET Core minimal APIs** host a single process node. Each node maintains:

* A **chain** (`List<Block>`) and a **mempool** (pending `Transaction`s).
* A **PoW miner** to append blocks with N leading zeros.
* **Wallet balances** recomputed deterministically from the chain.
* A **peer list** (config + runtime) with **broadcast** and **longest-chain** resolution.

We use **Swagger/OpenAPI** to explore endpoints, **Prometheus** to scrape metrics, **health checks** for orchestration/readiness, and **Docker/Compose** to run multiple nodes. Minimal APIs are great for lightweight HTTP services in .NET. 

---

## Folder Structure

```
.
├─ Core/
│  └─ Blockchain.cs                # Chain + PoW + validation + conflict resolution
├─ Models/
│  ├─ Block.cs                     # Block model + deterministic hashing
│  ├─ Transaction.cs               # Immutable money transfer
│  ├─ Wallet.cs                    # Simple wallet with balance
│  └─ BlockchainState.cs           # Persisted chain + wallet snapshot
├─ Services/
│  ├─ NodeService.cs               # Peer set, broadcast, resolve conflicts
│  ├─ WalletService.cs             # Wallets: snapshot/load, balances
│  ├─ PersistenceService.cs        # Save/load state file per HTTP port
│  └─ ChainHealthCheck.cs          # Validity + tip exposed to health checks
├─ wwwroot/
│  └─ index.html                   # Tiny in-browser tester UI
├─ Program.cs                      # Minimal API endpoints + DI + metrics + swagger
├─ CsharpBlockchainNode.csproj     # App project file
├─ docker-compose.yml              # Two-node demo with healthchecks
├─ Dockerfile                      # Multi-stage build (SDK -> runtime)
├─ appsettings.json                # Difficulty, genesis allocations, peers
└─ Blockchain.Tests/               # Unit + integration tests
```

---

## Configuration

### `appsettings.json`

```json
{
  "NodeUrl": "http://localhost:5001",
  "Blockchain": { "Difficulty": 3 },
  "Genesis": {
    "Allocations": [
      { "to": "Alice", "amount": 100 },
      { "to": "Bob",   "amount": 50 }
    ]
  },
  "PeerNodes": [
    "http://localhost:5001",
    "http://localhost:5002"
  ]
}
```

* **Difficulty**: number of leading zeros for PoW.
* **Genesis**: initial balances (as `system` → address transactions).
* **PeerNodes**: seed peers; runtime peers can be added via API.

> .NET configuration supports environment overrides (`:` in JSON ↔ `__` in env vars), which we use in Docker Compose.

---

## Build & Run (local)

1. **Restore, build, and test**

```bash
dotnet restore
dotnet build Project.sln
dotnet test  Project.sln
```

2. **Run a single node**

```bash
# choose a port and public URL (NodeUrl is used in logs/broadcasts)
export ASPNETCORE_URLS="http://localhost:5001"
export NodeUrl="http://localhost:5001"
dotnet run --project CsharpBlockchainNode.csproj
```

3. **Try it from a second terminal**

```bash
A=http://localhost:5001

curl -s $A/healthz | jq
curl -s $A/info    | jq
curl -s $A/wallets | jq

# add and mine a transaction
curl -s -X POST $A/transactions/new -H "Content-Type: application/json" \
  -d '{"from":"Alice","to":"Bob","amount":2}' | jq

curl -s -X POST $A/mine | jq '.block.index,.block.hash'

# check balances
curl -s $A/wallets/Alice/balance | jq
curl -s $A/wallets/Bob/balance   | jq
```

4. **Swagger UI & tiny HTML page**

* Swagger/OpenAPI: `http://localhost:5001/swagger` (helps explore/test endpoints). 
* Static tester UI: `http://localhost:5001/` (the page in `wwwroot/index.html`).

---

## Docker / Compose (two or more nodes)

Build images and start **two nodes** with Compose:

```bash
docker compose up -d --build
docker compose ps
```

You’ll see:

* Node1 on host port **5001** (container port 5000)
* Node2 on host port **5002**

Health checks hit `/healthz` inside the container. (Docker **Compose** is designed to run multi-container apps and manage their lifecycle.) 

**Demo (two local nodes):**

```bash
A=http://localhost:5001
B=http://localhost:5002

# sanity
curl -s $A/healthz | jq
curl -s $B/healthz | jq

# list peers (Compose pre-wires some)
curl -s $A/peers | jq
curl -s $B/peers | jq

# add a transaction to A and mine
curl -s -X POST $A/transactions/new -H "Content-Type: application/json" \
  -d '{"from":"Alice","to":"Bob","amount":2}' | jq

curl -s -X POST $A/mine | jq '.block.index,.block.hash'

# Resolve on B (longest chain)
curl -s $B/nodes/resolve | jq '.message'
```

**More than two nodes?** Yep. Start another container (or run more services in Compose) and `POST /peers/register?url=...` from each node to introduce them. Compose (or Docker) is just a convenient orchestration layer; the app does not limit the peer count. (Docker is a container platform to package and run applications consistently across environments; Compose orchestrates multi-container setups.) 

---

## Expose nodes to the Internet with ngrok (remote demo)

**ngrok** creates secure tunnels so your local HTTP endpoints are reachable from the public internet—perfect for P2P demos between different devices/networks.

1. **Install & sign in** (once)

```bash
ngrok config add-authtoken <YOUR_AUTHTOKEN>
```

2. **Create a config file** (example exposes both nodes)

`~/.config/ngrok/ngrok.yml`:

```yaml
version: "3"
endpoints:
  - name: node1
    description: "Expose node1 on 5001"
    type: http
    upstream:
      url: http://localhost:5001

  - name: node2
    description: "Expose node2 on 5002"
    type: http
    upstream:
      url: http://localhost:5002
```

3. **Start both tunnels**

```bash
ngrok start --all
```

Note the two **public HTTPS URLs** assigned to `node1` and `node2`.

4. **Register peers across the internet**

```bash
NGROK1="https://AAA.ngrok-free.app"
NGROK2="https://BBB.ngrok-free.app"

# tell node1 about node2 and vice versa
curl -s -X POST "$NGROK1/peers/register?url=$NGROK2" | jq
curl -s -X POST "$NGROK2/peers/register?url=$NGROK1" | jq

# mine on node1, resolve on node2
curl -s -X POST "$NGROK1/transactions/new" \
  -H "Content-Type: application/json" \
  -d '{"from":"Alice","to":"Bob","amount":5}' | jq

curl -s -X POST "$NGROK1/mine" | jq '.block.index,.block.hash'
curl -s "$NGROK2/nodes/resolve" | jq '.message'
```

> If you get an ngrok HTML error page with code **ERR\_NGROK\_8012**, the agent can’t reach your upstream—ensure your local service is running and ports match.

---

## API Endpoints

---

### Endpoints (quick tour)

| Method | Path                             | Description |
|-------:|----------------------------------|-------------|
| GET    | `/`                              | Redirects to Swagger UI |
| GET    | `/swagger`                       | OpenAPI UI |
| GET    | `/healthz`                       | Health status (height, tip) |
| GET    | `/ready`                         | Readiness status |
| GET    | `/metrics`                       | Prometheus metrics (via prometheus-net) |
| GET    | `/info`                          | Tip, difficulty, peers, self URL |
| GET    | `/wallets`                       | All balances snapshot |
| GET    | `/wallets/{address}/balance`     | Balance by address |
| GET    | `/mempool`                       | Pending txs |
| GET    | `/chain`                         | Full chain |
| POST   | `/transactions/new`              | `{ from, to, amount }` (validates) |
| POST   | `/mine`                          | Mines pending txs + reward, broadcasts |
| GET    | `/nodes/resolve`                 | Longest-chain resolution |
| GET    | `/peers`                         | Current peers (config + dynamic) |
| POST   | `/peers/register?url=...`        | Add dynamic peer (idempotent) |
| POST   | `/peers/unregister?url=...`      | Remove dynamic peer |
| POST   | `/announce`                      | Accept block from peer (internal) |
| POST   | `/announce-block`                | Back-compat alias for `/announce` |

---

**Chain & mempool**

* `GET /chain` – full chain.
* `GET /mempool` or `/transactions/pending` – pending transactions.
* `POST /transactions/new` – `{ "from": "...", "to": "...", "amount": 2 }`.
* `POST /mine` – mine a block from pending tx + 1 coinbase reward to `my-miner-address`.

**Wallets**

* `GET /wallets` – all balances.
* `GET /wallets/{address}/balance` – single balance.

**Peers & P2P**

* `GET /peers` – combined list (config + dynamic).
* `POST /peers/register?url={peerUrl}` – add a runtime peer.
* `POST /peers/unregister?url={peerUrl}` – remove a runtime peer.
* `GET /nodes/resolve` – ask peers for longer valid chains and adopt if found (longest-chain rule).
* `POST /announce` – peer announces a just-mined block (the node validates and appends).
* `POST /announce-block` – alias kept for back-compat.

**Node info & docs**

* `GET /info` – `{ height, tipHash, difficulty, peers, self }`.
* `GET /` – redirects to **Swagger UI**.
* `GET /swagger` – interactive OpenAPI explorer (helps you test endpoints).

**Health / readiness**

* `GET /healthz` – custom health (valid chain/tip).
* `GET /ready` – readiness gate (at least genesis is loaded).
* `GET /health` – built-in ASP.NET Core **Health Checks** middleware.

**Metrics**

* `GET /metrics` – Prometheus metrics exposition (via `prometheus-net.AspNetCore`). Prometheus scrapes a text format from this endpoint to collect time-series metrics.

---

## Prometheus Metrics

We expose:

* `node_tx_accepted_total` – tx accepted into mempool
* `node_tx_rejected_total` – tx rejected
* `node_blocks_mined_total` – blocks mined locally
* `node_blocks_external_accepted_total` – blocks accepted from peers
* Standard HTTP metrics from `UseHttpMetrics()` at `/metrics`

The **Prometheus** server scrapes `/metrics` to ingest measurements. See Prometheus client libraries/exposition format for how metrics are structured.

---

## Health / Readiness

* **Health Checks middleware** is registered and mapped to `/health`. We additionally provide `/healthz` (JSON with tip/height) and `/ready`. Health checks are the recommended pattern in ASP.NET Core for probes used by orchestrators and load balancers.

---

## Tests

Run everything:

```bash
dotnet test Project.sln
```

What’s covered:

* **Core/Blockchain**

  * PoW meets difficulty; `MinePendingTransactions` produces valid blocks
  * Transaction validation (non-empty addresses, positive amount, sufficient balance)
  * `ReplaceChain` adopts longer valid chains and **rebuilds balances**

* **Services/NodeService**

  * Dynamic peer registration/unregistration
  * Conflict resolution stubbed against peer chain samples

* **Integration (API)** using `Microsoft.AspNetCore.Mvc.Testing`

  * `/healthz`, `/info` return expected JSON
  * Posting a tx + mining updates balances deterministically
  * App spins up with low difficulty for quick test mining

ASP.NET Core’s integration test host lets us bootstrap the app in-memory and call endpoints with an `HttpClient`, which is the standard pattern for real HTTP integration tests in ASP.NET Core. 

---

## Design Notes (what each class does)

### `Core/Blockchain.cs`

* Holds the canonical `Chain`, `PendingTransactions`, and config (`_difficulty`, `IConfiguration`).
* **Genesis**: built from `Genesis:Allocations` (system → address txs) using a fixed timestamp for deterministic hash across nodes.
* **Mining**: `MinePendingTransactions(miner)` creates a block of (pending + reward), brute-forces nonce until SHA-256 hash has N leading zeros, appends, updates balances, clears mempool.
* **Validation**:

  * `IsChainValid()` – internal chain validation (hashes, links, difficulty)
  * `IsChainValid(List<Block>)` – external candidate validation (same genesis, PoW, links)
  * `ReplaceChain(List<Block>)` – adopt longer valid chain and **RebuildWalletsFromChain**
* **External blocks**: `TryAcceptExternalBlock` validates `PreviousHash`, PoW, hash integrity; appends and applies balances if valid.

### `Models/Block.cs`

* `Block` includes `Index`, `Timestamp`, `Transactions`, `Nonce`, `PreviousHash`, `Hash`.
* **Deterministic hash**: centralized JSON options ensure same serialization on all nodes before hashing (stable order, no spaces).

### `Models/Transaction.cs`

* Immutable `record struct` with `From`, `To`, `Amount`.

### `Models/Wallet.cs`

* Simple `PublicKey` + `Balance`.

### `Models/BlockchainState.cs`

* Serializable payload `{ Chain, Wallets }` persisted to disk.

### `Services/WalletService.cs`

* Thread-safe `ConcurrentDictionary<string,Wallet>`.
* `GetBalance`, `SetBalance`, `Snapshot`, `LoadWallets`.
* The only place that mutates balances (enforces deterministic replay semantics).

### `Services/PersistenceService.cs`

* Computes a state file name from the resolved URL/port: `blockchain_state_{PORT}.json`.
* Saves and loads `{Chain, Wallets}`.
* Lets multiple nodes on the same machine persist independently by port.

### `Services/NodeService.cs`

* **Peers** = config peers + runtime dynamic peers (concurrent dictionary).
* `RegisterPeer`/`UnregisterPeer` manage the runtime set.
* `BroadcastNewBlockAsync` → POSTs `/announce` to all peers (except self).
* `ResolveConflictsAsync` → fetches `/chain` from peers, adopts **longest valid**.

### `Services/ChainHealthCheck.cs`

* Implements `IHealthCheck` to report chain validity and tip in health responses.

### `Program.cs` (the host)

* DI wiring: `Blockchain`, `WalletService`, `NodeService`, `PersistenceService`, health checks, `AddHttpClient` (available if needed).
* **Swagger UI** + OpenAPI endpoints for interactive testing.
* **Prometheus**: `UseMetricServer("/metrics")` + `UseHttpMetrics()`.
* **Endpoints**: see [API Endpoints](#api-endpoints).
* Serves `wwwroot/index.html` (tiny single-file UI).

---

## Troubleshooting

* **ngrok 400 / ERR\_NGROK\_8012**: tunnel can’t connect to your upstream—verify the app is running on the port you exposed and that your config YAML uses `type: http` → `upstream.url: http://localhost:<port>`.
* **Blocks don’t propagate**: ensure both nodes have registered each other (`/peers`) and that you mined on one then called `/nodes/resolve` on the others.
* **State collisions**: different nodes should bind different host ports so their state files are distinct.
* **Compose health**: both services show `(healthy)` when `/healthz` returns 200; check `docker compose logs -f`.

---
