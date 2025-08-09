using System.Text.Json;
using CsharpBlockchainNode.Core;
using CsharpBlockchainNode.Models;
using CsharpBlockchainNode.Services;

var builder = WebApplication.CreateBuilder(args);

// Dependency Injection (DI)
var difficulty = builder.Configuration.GetValue<int>("Blockchain:Difficulty");

// Create and register WalletService as a Singleton
var walletService = new WalletService();
builder.Services.AddSingleton(walletService);

// Create some initial wallets for testing
walletService.GetOrCreateWallet("Alice").Balance = 100;
walletService.GetOrCreateWallet("Bob").Balance = 50;

// Pass the WalletService instance to the Blockchain constructor
builder.Services.AddSingleton(new Blockchain(difficulty, walletService));
builder.Services.AddSingleton<NodeService>();

var app = builder.Build();

// --- API Endpoint Definitions ---

// GET /chain
// Returns the entire blockchain.
app.MapGet("/chain", (Blockchain blockCoin) => Results.Ok(blockCoin.Chain));

// GET /wallets/{address}/balance - NEW endpoint to check a wallet's balance.
app.MapGet("/wallets/{address}/balance", (string address, WalletService walletSvc) =>
{
    var balance = walletSvc.GetBalance(address);
    return Results.Ok(new { address, balance });
});

// POST /transactions/new - Updated to use validation.
app.MapPost("/transactions/new", (Transaction transaction, Blockchain blockCoin) =>
{
    var transactionAdded = blockCoin.AddTransaction(transaction);
    if (transactionAdded)
    {
        return Results.Ok(new { message = "Transaction added to pending pool." });
    }
    return Results.BadRequest(new { message = "Transaction failed validation and was rejected." });
});

// POST /mine - No changes to the signature, but its internal behavior is now different.
app.MapPost("/mine", async (Blockchain blockCoin, NodeService nodeService) =>
{
    const string minerAddress = "my-miner-address"; 
    blockCoin.MinePendingTransactions(minerAddress);
    var newBlock = blockCoin.GetLatestBlock();
    await nodeService.BroadcastNewBlockAsync(newBlock);
    return Results.Ok(new { message = "New block mined and broadcast successfully.", block = newBlock });
});

// POST /announce-block
// This is a endpoint for a node to receive a block from a peer.
app.MapPost("/announce-block", (Block block, Blockchain blockCoin) =>
{
    // Basic validation: check if the new block's previous hash matches our latest block's hash
    var latestBlock = blockCoin.GetLatestBlock();
    if (block.PreviousHash == latestBlock.Hash)
    {
        // Here we would do more validation before accepting
        blockCoin.Chain.Add(block);
        Console.WriteLine($"Accepted new block {block.Index} from peer.");
        return Results.Ok(new { message = "Block accepted." });
    }
    return Results.BadRequest(new { message = "Block rejected." });
});

// GET /nodes/resolve - No changes needed here.
app.MapGet("/nodes/resolve", async (Blockchain blockCoin, NodeService nodeService) => { /* ... */ });


// Start the Application
app.Run();