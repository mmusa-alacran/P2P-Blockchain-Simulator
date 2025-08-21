using CsharpBlockchainNode.Core;
using CsharpBlockchainNode.Models;
using CsharpBlockchainNode.Services;

// --- Application Builder Setup ---
var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

// --- Service Registration & State Loading ---

// Register PersistenceService first, as we need it immediately.
services.AddSingleton<PersistenceService>();

// Manually build the service provider to get an instance of PersistenceService
var tempProvider = services.BuildServiceProvider();
var persistenceService = tempProvider.GetRequiredService<PersistenceService>();

// Try to load the state from disk
var loadedState = await persistenceService.LoadStateAsync();

// Register WalletService and Blockchain as singletons based on loaded state
var walletService = new WalletService();
if (loadedState != null)
{
    Console.WriteLine("Loaded state from disk.");
    walletService.LoadWallets(loadedState.Wallets);
}
else
{
    Console.WriteLine("No state file found. Creating default wallets.");
    walletService.GetOrCreateWallet("Alice").Balance = 100;
    walletService.GetOrCreateWallet("Bob").Balance = 50;
}
services.AddSingleton(walletService);

var difficulty = configuration.GetValue<int>("Blockchain:Difficulty");
var blockCoin = new Blockchain(difficulty, walletService, loadedState?.Chain); 
services.AddSingleton(blockCoin);

services.AddSingleton<NodeService>();


var app = builder.Build();

// --- API Endpoint Definitions ---


// GET /chain
app.MapGet("/chain", (Blockchain blockCoin) => Results.Ok(blockCoin.Chain));

// GET /wallets/{address}/balance
app.MapGet("/wallets/{address}/balance", (string address, WalletService walletSvc) => Results.Ok(new { address, balance = walletSvc.GetBalance(address) }));

// POST /transactions/new
app.MapPost("/transactions/new", (Transaction transaction, Blockchain blockCoin) =>
{
    return blockCoin.AddTransaction(transaction)
        ? Results.Ok(new { message = "Transaction added to pending pool." })
        : Results.BadRequest(new { message = "Transaction failed validation." });
});

// POST /mine
app.MapPost("/mine", async (Blockchain blockCoin, NodeService nodeService, PersistenceService persistenceSvc, WalletService walletSvc, IConfiguration config) =>
{
    const string minerAddress = "my-miner-address";
    blockCoin.MinePendingTransactions(minerAddress);
    
    await persistenceSvc.SaveStateAsync(new BlockchainState(blockCoin.Chain, new Dictionary<string, Wallet>(walletSvc._wallets)));
    
    var newBlock = blockCoin.GetLatestBlock();
    var selfUrl = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? config.GetValue<string>("NodeUrl")!;
    await nodeService.BroadcastNewBlockAsync(newBlock, selfUrl);
    return Results.Ok(new { message = "New block mined, state saved, and broadcast.", block = newBlock });
});

// POST /announce-block
app.MapPost("/announce-block", async (Block block, Blockchain blockCoin, PersistenceService persistenceSvc, WalletService walletSvc) => {
    var latestBlock = blockCoin.GetLatestBlock();
    if (block.PreviousHash == latestBlock.Hash)
    {
        blockCoin.Chain.Add(block);
        
        await persistenceSvc.SaveStateAsync(new BlockchainState(blockCoin.Chain, new Dictionary<string, Wallet>(walletSvc._wallets)));
        
        Console.WriteLine($"Accepted new block {block.Index} from peer and saved state.");
        return Results.Ok(new { message = "Block accepted." });
    }
    return Results.BadRequest(new { message = "Block rejected." });
});

// GET /nodes/resolve
app.MapGet("/nodes/resolve", async (Blockchain blockCoin, NodeService nodeService, PersistenceService persistenceSvc, WalletService walletSvc, IConfiguration config) =>
{
    var selfUrl = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? config.GetValue<string>("NodeUrl")!;
    bool chainReplaced = await nodeService.ResolveConflictsAsync(blockCoin, selfUrl);
    if (chainReplaced)
    {
        await persistenceSvc.SaveStateAsync(new BlockchainState(blockCoin.Chain, new Dictionary<string, Wallet>(walletSvc._wallets)));
        return Results.Ok(new { message = "Our chain was replaced and state saved.", chain = blockCoin.Chain });
    }
    return Results.Ok(new { message = "Our chain is authoritative.", chain = blockCoin.Chain });
});


// --- Start the Application ---
var appUrl = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? configuration.GetValue<string>("NodeUrl") ?? "http://localhost:5000";
Console.WriteLine($"Node starting on URL: {appUrl}");
app.Run(appUrl);