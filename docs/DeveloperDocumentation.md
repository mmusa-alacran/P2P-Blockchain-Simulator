# P2P Blockchain Simulator — Developer Guide

## 1) High-level architecture

This project is a compact, educational **proof-of-work blockchain** implemented as an ASP.NET Core **minimal API** service. Each instance is a “node” with:

* A canonical **chain** (`Core/Blockchain.cs`).
* A **mempool** of pending transactions.
* **Mining** (PoW) that builds new blocks from mempool + coinbase reward.
* A **wallet state** service that computes balances from the chain (`Services/WalletService.cs`).
* Simple **P2P** utilities to broadcast blocks and resolve to the longest valid chain (`Services/NodeService.cs`).
* **Persistence** of chain + wallet snapshot to a local JSON file keyed by HTTP port (`Services/PersistenceService.cs`).
* **Observability**: health/readiness endpoints, Prometheus metrics, and Swagger/OpenAPI.

> Minimal APIs/DI/hosting come from ASP.NET Core’s modern “minimal hosting” model. If you want background on the platform patterns we use (DI, minimal routing, WebApplicationFactory), Microsoft’s docs are a good anchor. 

---

## 2) Project map & responsibilities

### `Program.cs` — Composition & HTTP surface

* **Logging**: Serilog is wired via `builder.Host.UseSerilog(...)` for structured console logs.
* **Services**: registers `HttpClientFactory`, HealthChecks, `PersistenceService`, `WalletService`, `Blockchain`, `NodeService`.
* **Startup state**: loads saved `BlockchainState` (if present) and restores wallet snapshot; otherwise wallets are derived from genesis on first run.
* **Middleware**:

  * `UseSwagger` + `UseSwaggerUI` → live API docs.
  * `UseMetricServer("/metrics")` and `UseHttpMetrics()` → Prometheus scraping endpoint and per-request metrics.
  * Static file server for `wwwroot/index.html` demo UI.
* **Endpoints**:

  * `GET /healthz` and `GET /ready` (lightweight liveness/readiness).
  * `GET /health` via ASP.NET Core Health Checks middleware (structured health-report endpoint).
  * `GET /chain`, `GET /mempool`, `GET /wallets`, `GET /wallets/{address}/balance`.
  * `POST /transactions/new` (validate + enqueue).
  * `POST /mine` (mine a block; persist state; broadcast to peers).
  * `POST /announce` and `/announce-block` (accept a block from a peer).
  * `GET /nodes/resolve` (longest-valid-chain conflict resolution).
  * `GET /peers`, `POST /peers/register`, `POST /peers/unregister`.
  * `GET /info` (node status summary).

> Minimal APIs (mapping delegates to routes) is the small, composable style introduced in .NET 6+; see Microsoft’s minimal testing/hosting pages for the mental model.
> Health Checks are a first-class ASP.NET Core feature exposed via middleware that we surface as `/health`.
> Swagger/OpenAPI with `AddEndpointsApiExplorer` + `AddSwaggerGen` is the standard way to self-document an ASP.NET Core API.
> Prometheus metrics in ASP.NET Core are commonly done using `prometheus-net.AspNetCore` with `UseMetricServer` + `UseHttpMetrics`. The package’s README outlines the expected usage. 

---

### `Core/Blockchain.cs` — Ledger, PoW & validation

* **Genesis**: created with deterministic timestamp and allocations from `appsettings.json` (`Genesis:Allocations`).
* **Mempool**: `_pendingTransactions` holds validated but not-yet-mined transactions.
* **Mining**:

  * Appends a **coinbase** transaction (`system → miner: 1`) to mempool.
  * Builds a new block with `PreviousHash` set to the current tip.
  * Performs **proof-of-work** by incrementing `Nonce` until the block hash has `_difficulty` leading zeros.
  * Appends the block, applies balances to wallets (subtract non-system senders, add to recipients), and clears the mempool.
* **Validation**:

  * `IsChainValid()` walks from genesis and ensures each block’s hash matches its content, `PreviousHash` links properly, and each block meets difficulty.
  * `IsChainValid(List<Block>)` does the same on a candidate chain; also checks candidate’s genesis matches ours.
  * `ReplaceChain()` adopts a longer valid chain and fully **rebuilds** wallets by replay.
  * `TryAcceptExternalBlock()` validates a single extending block then appends and applies balances.

**Hashing**: implemented in `Models/Block.cs` using SHA-256 over a compact `System.Text.Json` serialization of `{ Index, Timestamp, Transactions, Nonce, PreviousHash }`. We set consistent options (camelCase, no indentation) for stability.

---

### `Models/` — Simple data shapes

* `Block`: immutable content plus `Nonce` and `Hash`; `CalculateHash()` uses SHA-256 over compact JSON.
* `Transaction`: `record struct` with `From`, `To`, `Amount` (decimal).
* `Wallet`: `PublicKey` + `Balance` (decimal).
* `BlockchainState`: composite DTO `{ Chain, Wallets }` persisted to disk.

---

### `Services/WalletService.cs` — Balances

* Maintains a **private** `ConcurrentDictionary<string, Wallet>` to avoid accidental mutation from outside.
* Provides `GetBalance`, `GetOrCreateWallet`, `SetBalance`, `GetAllBalances()`, `Snapshot()` (deep copy), `LoadWallets()` (replace all from snapshot).

---

### `Services/NodeService.cs` — P2P utilities

* **Peers**: union of configured peers (`PeerNodes` in config) and **dynamic** peers registered at runtime (kept in a `ConcurrentDictionary`).
* `RegisterPeer(url)`, `UnregisterPeer(url)`, `GetPeers()`.
* `BroadcastNewBlockAsync(block, selfUrl)`: POSTs the new block to each peer’s `/announce` endpoint (skips `selfUrl`).
* `ResolveConflictsAsync(blockchain, selfUrl)`: queries each peer’s `/chain`, picks the longest **valid** one, and asks the local `Blockchain` to `ReplaceChain(...)`.

---

### `Services/PersistenceService.cs` — On-disk state

* Derives a filename `blockchain_state_{port}.json` by parsing the hosting URL (from `urls`, `ASPNETCORE_URLS`, or `NodeUrl`).
* `SaveStateAsync(BlockchainState)` and `LoadStateAsync()` using `System.Text.Json`.
* Each container/instance keeps a **separate** file based on port, so running multiple nodes on one machine won’t clobber each other.

---

### `Services/ChainHealthCheck.cs` — Health report

* Uses `IHealthCheck` to report chain validity, height, and tip. We return `Healthy` with a small data bag on success, or `Unhealthy` with the same bag if validation fails.
* Exposed by `/health` via ASP.NET Core’s Health Checks middleware.

---

### `wwwroot/index.html` — Tiny demo UI

* A static single page that calls the API from the browser: shows status, creates transactions, mines, resolves, and manages peers.
* Handy during manual demos and when you want to see JSON responses without remembering curl incantations.

---

## 3) Configuration model

### `appsettings.json` (defaults you can override)

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

### Environment variables

ASP.NET Core config is hierarchical; **double underscores** (`__`) are used to map nested keys when using env vars (and can also index arrays). In Docker Compose we use keys like:

* `PeerNodes__0=http://node2:5000`
* `PeerNodes__1=http://localhost:5001`
* `Blockchain__Difficulty=3`

> That double-underscore mapping is how ASP.NET Core’s environment variable provider targets nested configuration and arrays. 

---

## 4) API endpoints

**Public (JSON):**

* `GET /healthz` → `{ status, height, tip }` (cheap liveness)
* `GET /ready` → `{ status, height }` (cheap readiness)
* `GET /health` → ASP.NET Health Checks payload (structured)
* `GET /info` → `{ height, tipHash, difficulty, peers[], self }`
* `GET /wallets` → `{ address: balance, ... }`
* `GET /wallets/{address}/balance` → `{ address, balance }`
* `GET /chain` → `Block[]`
* `GET /mempool` or `/transactions/pending` → `Transaction[]`
* `POST /transactions/new` (body: `{from,to,amount}`) → 200 or 400
* `POST /mine` → mines, persists state, broadcasts new block
* `GET /nodes/resolve` → invokes “longest valid chain” resolution
* `GET /peers` → string\[]
* `POST /peers/register?url=...` and `POST /peers/unregister?url=...`
* `POST /announce` (and legacy alias `/announce-block`) → accept a peer’s block

OpenAPI docs & the interactive **Swagger UI** are available at `/swagger`.

---

## 5) Observability

* **Logs**: Serilog emits structured console logs for requests and our custom messages.
* **Metrics**:

  * Global HTTP metrics via `UseHttpMetrics()`.
  * Custom counters:
    `node_tx_accepted_total`, `node_tx_rejected_total`,
    `node_blocks_mined_total`, `node_blocks_external_accepted_total`.
  * Scrape at `GET /metrics`.
* **Health**:

  * `/healthz` and `/ready` for container probes (very cheap).
  * `/health` for full ASP.NET Core Health Checks.

> The `prometheus-net.AspNetCore` package exposes `UseMetricServer()` and `UseHttpMetrics()`; these are the idiomatic entry points for scraping and per-request metrics. 

---

## 6) Testing

### Unit tests (logic)

* Focus on `Blockchain` behaviors: mining, validation, replacement, balance replay, transaction validation edge cases.

### Integration tests (API)

* **WebApplicationFactory** creates an in-memory server for the minimal API, letting you call endpoints via `HttpClient` without listening on a real TCP port.
* We **lower PoW** (difficulty=1) in tests to mine quickly.
* Typical flow:

  1. `POST /transactions/new` (Alice→Bob:2)
  2. `POST /mine`
  3. `GET /wallets/.../balance` for Alice, Bob, and miner; assert expected values.

> `WebApplicationFactory<TEntryPoint>` is the canonical test harness for ASP.NET Core integration tests (works fine with minimal APIs).

**Run everything:**

```bash
dotnet test Project.sln
```

---

## 7) Docker & Compose

### Dockerfile (multi-stage)

* **Build stage**: `mcr.microsoft.com/dotnet/sdk:8.0`

  * `dotnet restore`
  * `dotnet publish -c Release -o /out`
* **Runtime stage**: `mcr.microsoft.com/dotnet/aspnet:8.0`

  * Copies `/out`
  * `ASPNETCORE_URLS=http://+:5000`
  * `HEALTHCHECK` probes `GET /healthz`
  * `ENTRYPOINT ["dotnet","CsharpBlockchainNode.dll"]`

> Multi-stage Dockerfiles are the recommended way to produce small runtime images for .NET apps—build with the SDK image, run on the ASP.NET image.
> The `HEALTHCHECK` instruction is evaluated by Docker to mark a container healthy/unhealthy; it’s the right place to call `/healthz`.

### docker-compose.yml (two nodes)

* Spins up **node1** (mapped to host 5001) and **node2** (mapped to host 5002).
* Sets `NodeUrl` and `PeerNodes__*` so each node knows itself + its peer.
* Adds a service-level **healthcheck** to hit `http://localhost:5000/healthz` inside each container.

**Build & run:**

```bash
docker compose up -d --build
docker compose ps
curl -s http://localhost:5001/healthz
curl -s http://localhost:5002/info
```

---

## 8) Remote demos with ngrok (multi-device / internet)

* Configure ngrok **agent v3** with two **endpoints** (node1→5001, node2→5002) in your YAML, then:

```bash
ngrok start --all
# Then inspect tunnels:
curl -s http://127.0.0.1:4040/api/tunnels | jq
```

* Register peers across public URLs:

```bash
export A="https://<node1>.ngrok-free.app"
export B="https://<node2>.ngrok-free.app"

curl -s -X POST "$A/peers/register?url=$B"
curl -s -X POST "$B/peers/register?url=$A"
```

* Create & mine on A; verify balances on B:

```bash
curl -s -H 'Content-Type: application/json' \
  -d '{"from":"Alice","to":"Bob","amount":5}' \
  -X POST "$A/transactions/new"
curl -s -X POST "$A/mine" | jq '.block.index, .block.hash'
curl -s "$B/nodes/resolve" | jq '.message'
curl -s "$B/wallets/Bob/balance" | jq
```

> The agent v3 configuration uses a YAML “endpoints” list that maps endpoint names to upstream URLs; this is the supported approach to run multiple tunnels from one agent. 

**More than two nodes?**
Yes. Add more containers (or processes), expose unique host ports (e.g., 5003, 5004…), and register them as peers via `/peers/register`. Nodes discover only what you tell them, so you control the graph (mesh vs. star).

---

## 9) Security model (explicitly simplified for learning)

* No **signatures/keys**: we trust the `From` field and only enforce **balance** & **amount > 0**.
* No **network encryption** or **auth**: run behind trusted tunnels/environments for demos.
* No **chain pruning** or **UTXO** model: balances are account-based and reconstructed from full chain replay.

For production-grade systems you’d add ECDSA signatures, do block + tx verification including nonces, timestamps and mempool policy, enable TLS/auth, and design sybil/DoS protections.

---

## 10) Performance knobs & correctness notes

* **Difficulty**: set via config (`Blockchain:Difficulty`). `3` works well for demos; `1` in test.
* **Genesis determinism**: fixed epoch and allocations ensure identical genesis across nodes.
* **JSON hashing**: We hash a stable JSON shape (consistent options, same property set). If you ever change the block structure, treat it as a **consensus change** and bump a version or add a migration path.
* **ReplaceChain**: “longest chain” is used here (block count). You could switch to “highest cumulative work” (sum of difficulties) if you extend the model.

---

## 11) Extending the system

* **Wallet signatures**: add ECDSA keys & signature verification on `POST /transactions/new`.
* **P2P handshake**: add `/hello` & `/peers` exchange to auto-discover graph, with backoff/retries.
* **Persistence**: persist **mempool** and rotate chain state files.
* **Rewards & halving**: move coinbase amount to config; add schedule.
* **Chain weight**: resolve by cumulative difficulty instead of length.

---

## 12) Local dev workflow

```bash
# 1) Restore & build
dotnet build Project.sln

# 2) Run tests (unit + integration)
dotnet test Project.sln

# 3) Run locally on ports 5001/5002 via Docker
docker compose up -d --build
curl -s http://localhost:5001/healthz | jq

# 4) Manual demo (browser)
open http://localhost:5001/           # or your OS equivalent
open http://localhost:5001/swagger
open http://localhost:5001/metrics

# 5) Remote demo (two devices)
ngrok start --all
# then use /peers/register across the public URLs
```

---

## 13) Code reading guide (file-by-file)

* **`Core/Blockchain.cs`**
  `MinePendingTransactions`, `MineBlock`, `AddTransaction`, `IsChainValid`, `ReplaceChain`, `RebuildWalletsFromChain`, `TryAcceptExternalBlock`.
  *Tip*: Set a breakpoint in `MineBlock` to see nonce iteration; watch the `Hash` prefix.

* **`Models/*.cs`**

  * `Block`: `CalculateHash()` is the consensus hash.
  * `Transaction`: value object.
  * `Wallet`: mutable balance holder.
  * `BlockchainState`: persisted DTO.

* **`Services/WalletService.cs`**
  Centralizes balance logic; **never** mutate wallets directly elsewhere.

* **`Services/NodeService.cs`**

  * `Peers` property merges config and dynamic.
  * `BroadcastNewBlockAsync()` and `ResolveConflictsAsync()` call other nodes.

* **`Services/PersistenceService.cs`**
  Port-aware file naming; JSON read/write.

* **`Services/ChainHealthCheck.cs`**
  Health Check that reflects `IsChainValid()` and tip info.

* **`Program.cs`**
  Composition root: DI, endpoints, metrics, health, static UI, Swagger.

---

## 14) Toolset Choices

**Why Swagger UI?**
To **self-document** and manually exercise the API without extra tools; it’s generated from the endpoint metadata, which speeds up dev & debugging. 

**Why ngrok?**
To **expose local nodes** to the public internet for quick P2P demos across devices/classmates without deploying to a cloud or punching firewall holes. 

**Why the static HTML page?**
It’s a zero-dependency demo client: quick buttons for health, mining, resolving, and peers—great for live presentations.

**Why Docker/Compose?**
To run multiple nodes reliably and reproducibly, with health checks and port mapping, and ship a single image that runs anywhere a container runtime is available. The Dockerfile’s **multi-stage** pattern keeps the runtime image small and fast to start.

---

