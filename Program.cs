using CsharpBlockchainNode.Core;
using CsharpBlockchainNode.Models;
using CsharpBlockchainNode.Services;
using System.Net.Http.Json;
using Prometheus;
using Serilog;


var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
var services = builder.Services;


// ---------- Serilog (structured console logging) ----------
builder.Host.UseSerilog((ctx, log) =>
{
    log.ReadFrom.Configuration(ctx.Configuration)
       .WriteTo.Console();
});


// ---------- IHttpClientFactory + health checks ----------
services.AddHttpClient();
services.AddHealthChecks().AddCheck<ChainHealthCheck>("chain");

// ---------- Load persistence + core services ----------
var persistenceService = new PersistenceService(configuration);
services.AddSingleton(persistenceService);

var loadedState = await persistenceService.LoadStateAsync();

var walletService = new WalletService();
if (loadedState is not null)
{
    Console.WriteLine("Loaded state from disk.");
    walletService.LoadWallets(loadedState.Wallets);
}
else
{
    Console.WriteLine("No state file found. Wallet balances will be derived from the chain (genesis).");
}
services.AddSingleton(walletService);

var difficulty = configuration.GetValue<int>("Blockchain:Difficulty");
var blockchain = new Blockchain(difficulty, walletService, loadedState?.Chain, configuration);
services.AddSingleton(blockchain);

services.AddSingleton<NodeService>();

// ---------- Swagger/OpenAPI ----------
services.AddEndpointsApiExplorer();
services.AddSwaggerGen();

services.AddCors(opts =>
{
    // Demo policy: wide-open for quick demos. Lock down in production.
    opts.AddPolicy("demo", b => b
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader());
});

// --------------------------

var app = builder.Build();

// ---------- Swagger UI ----------
app.UseSwagger();                   // OpenAPI JSON
app.UseSwaggerUI();                 // Interactive UI at /swagger

// ---------- Prometheus metrics ----------
app.UseMetricServer("/metrics");   // scrape endpoint
app.UseHttpMetrics();              // Prometheus request metrics (/metrics)
app.UseCors("demo");               // enable CORS for the demo UI

// Serve a tiny static UI from wwwroot 
app.UseDefaultFiles();
app.UseStaticFiles();

// Some custom counters for demo
var txAccepted = Metrics.CreateCounter("node_tx_accepted_total", "Transactions accepted into mempool.");
var txRejected = Metrics.CreateCounter("node_tx_rejected_total", "Transactions rejected by validation.");
var blocksMined = Metrics.CreateCounter("node_blocks_mined_total", "Blocks mined by this node.");
var blocksAcceptedFromPeers = Metrics.CreateCounter("node_blocks_external_accepted_total", "External blocks accepted.");

// ---------- Helpers ----------


// Helper: choose a single canonical self URL for logs/broadcasts.
// Prefer NodeUrl (public URL), then profile/ASPNETCORE_URLS, then default.
static string ResolveSelfUrl(IConfiguration cfg)
{
    var urls = cfg.GetValue<string>("NodeUrl")
               ?? cfg["urls"]
               ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
               ?? "http://localhost:5000";

    return urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
}

// ---------- API ----------
app.MapGet("/", () => Results.Redirect("/swagger"));


// Built-in health checks (chain validity etc.)
app.MapHealthChecks("/health");

// Mempool & balances for transparency
app.MapGet("/mempool", (Blockchain bc) => Results.Ok(bc.PendingTransactions));
app.MapGet("/transactions/pending", (Blockchain bc) => Results.Ok(bc.PendingTransactions));
app.MapGet("/wallets", (WalletService ws) => Results.Ok(ws.GetAllBalances()));

app.MapGet("/chain", (Blockchain bc) => Results.Ok(bc.Chain));

app.MapGet("/wallets/{address}/balance", (string address, WalletService ws)
    => Results.Ok(new { address, balance = ws.GetBalance(address) })
);

// Add a transaction to the mempool
app.MapPost("/transactions/new", (Transaction transaction, Blockchain bc) =>
{
    var ok = bc.AddTransaction(transaction);
    if (ok) txAccepted.Inc();
    else txRejected.Inc();

    return ok
        ? Results.Ok(new { message = "Transaction added to pending pool." })
        : Results.BadRequest(new { message = "Transaction failed validation." });
});

// Mine everything currently pending + coinbase reward
app.MapPost("/mine", async (Blockchain bc, NodeService nodeService, PersistenceService ps, WalletService ws, IConfiguration cfg) =>
{
    const string minerAddress = "my-miner-address";
    bc.MinePendingTransactions(minerAddress);
    blocksMined.Inc();

    await ps.SaveStateAsync(new BlockchainState(bc.Chain, ws.Snapshot()));

    var newBlock = bc.GetLatestBlock();
    var selfUrl = ResolveSelfUrl(cfg);
    await nodeService.BroadcastNewBlockAsync(newBlock, selfUrl);

    return Results.Ok(new { message = "New block mined, state saved, and broadcast.", block = newBlock });
});

// Accept a block from a peer
app.MapPost("/announce", async (Block block, Blockchain bc, PersistenceService ps, WalletService ws) =>
{
    if (!bc.TryAcceptExternalBlock(block))
        return Results.BadRequest(new { message = "Block rejected." });

    blocksAcceptedFromPeers.Inc();
    await ps.SaveStateAsync(new BlockchainState(bc.Chain, ws.Snapshot()));
    Console.WriteLine($"Accepted new block {block.Index} from peer and saved state.");
    return Results.Ok(new { message = "Block accepted." });
});

// Longest-chain resolution
app.MapGet("/nodes/resolve", async (Blockchain bc, NodeService nodeService, PersistenceService ps, WalletService ws, IConfiguration cfg) =>
{
    var selfUrl = ResolveSelfUrl(cfg);
    bool replaced = await nodeService.ResolveConflictsAsync(bc, selfUrl);

    if (replaced)
    {
        await ps.SaveStateAsync(new BlockchainState(bc.Chain, ws.Snapshot()));
        return Results.Ok(new { message = "Our chain was replaced and state saved.", chain = bc.Chain });
    }

    return Results.Ok(new { message = "Our chain is authoritative.", chain = bc.Chain });
});

// ---- dynamic peer management ----
app.MapGet("/peers", (NodeService nodes) => Results.Ok(nodes.GetPeers()));

app.MapPost("/peers/register", (NodeService nodes, string url) =>
{
    var added = nodes.RegisterPeer(url);
    return added
        ? Results.Ok(new { message = "Peer registered.", url })
        : Results.Ok(new { message = "Peer already present.", url });
});

app.MapPost("/peers/unregister", (NodeService nodes, string url) =>
{
    var removed = nodes.UnregisterPeer(url);
    return removed
        ? Results.Ok(new { message = "Peer unregistered.", url })
        : Results.NotFound(new { message = "Peer not found.", url });
});

app.MapGet("/info", (Blockchain bc, IConfiguration c, NodeService nodes) =>
{
    var tip = bc.GetLatestBlock();
    return Results.Ok(new
    {
        height = tip.Index,
        tipHash = tip.Hash,
        difficulty = c.GetValue<int>("Blockchain:Difficulty"),
        peers = nodes.GetPeers(),
        self = ResolveSelfUrl(c)
    });
});


// Health endpoints (already in the app; here for completeness)
app.MapGet("/healthz", (Blockchain bc) =>
{
    var tip = bc.GetLatestBlock();
    return Results.Ok(new { status = "healthy", height = tip.Index, tip = tip.Hash });
});

// Readiness: node can serve traffic AND has at least the genesis block.
app.MapGet("/ready", (Blockchain bc) =>
{
    var tip = bc.GetLatestBlock();
    return Results.Ok(new { status = "ready", height = tip.Index });
});

// POST /nodes/register : add a peer at runtime (e.g., an ngrok URL)
app.MapPost("/nodes/register", (NodeRegistration req, NodeService nodeService) =>
{
    if (nodeService.RegisterPeer(req.Url))
        return Results.Ok(new { message = "Peer registered", peer = req.Url, total = nodeService.Peers.Count() });

    return Results.BadRequest(new { message = "Invalid peer URL" });
});

// Back-compat alias for older peers that post to /announce-block
app.MapPost("/announce-block", async (Block block, Blockchain bc, PersistenceService ps, WalletService ws) =>
{
    if (!bc.TryAcceptExternalBlock(block))
        return Results.BadRequest(new { message = "Block rejected." });

    // uncomment to track metrics:
    // blocksAcceptedFromPeers.Inc();

    await ps.SaveStateAsync(new BlockchainState(bc.Chain, ws.Snapshot()));
    Console.WriteLine($"Accepted new block {block.Index} from peer and saved state.");
    return Results.Ok(new { message = "Block accepted." });
});


// ---------- Host startup ----------
var resolved = configuration["urls"]
               ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
               ?? configuration.GetValue<string>("NodeUrl")
               ?? "http://localhost:5000";

Console.WriteLine($"Node starting on URL(s): {resolved}");
app.Run();


public sealed record NodeRegistration(string Url);


public partial class Program { } // exposed for WebApplicationFactory tests
