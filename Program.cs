using System.Text.Json;
using CsharpBlockchainNode.Core;
using CsharpBlockchainNode.Models;
using CsharpBlockchainNode.Services;

var builder = WebApplication.CreateBuilder(args);

// Dependency Injection (DI)
var difficulty = builder.Configuration.GetValue<int>("Blockchain:Difficulty");
builder.Services.AddSingleton(new Blockchain(difficulty));
builder.Services.AddSingleton<NodeService>(); 

var app = builder.Build();

// --- API Endpoint Definitions ---

// GET /chain
// Returns the entire blockchain.
app.MapGet("/chain", (Blockchain blockCoin) =>
{
    return Results.Ok(blockCoin.Chain);
});

// POST /transactions/new
// Allows adding a new transaction.
app.MapPost("/transactions/new", (Transaction transaction, Blockchain blockCoin) =>
{
    blockCoin.AddTransaction(transaction);
    return Results.Ok(new { message = $"Transaction will be added to the next block." });
});

// POST /mine
// Triggers the mining of a new block and broadcasts it.
app.MapPost("/mine", async (Blockchain blockCoin, NodeService nodeService) =>
{
    const string minerAddress = "my-miner-address"; 
    
    blockCoin.MinePendingTransactions(minerAddress);
    var newBlock = blockCoin.GetLatestBlock();

    // Broadcast the newly mined block to all peers
    await nodeService.BroadcastNewBlockAsync(newBlock);

    return Results.Ok(new { message = "New block mined and broadcast successfully.", block = newBlock });
});

// POST /announce-block
// This is a new endpoint for a node to receive a block from a peer.
app.MapPost("/announce-block", (Block block, Blockchain blockCoin) => {
    // Basic validation: check if the new block's previous hash matches our latest block's hash
    var latestBlock = blockCoin.GetLatestBlock();
    if (block.PreviousHash == latestBlock.Hash)
    {
        // Here we would do more validation before accepting
        blockCoin.Chain.Add(block);
        Console.WriteLine($"Accepted new block {block.Index} from peer.");
        return Results.Ok(new { message = "Block accepted." });
    }

    Console.WriteLine($"Rejected new block {block.Index} from peer.");
    return Results.BadRequest(new { message = "Block rejected." });
});


// Start the Application
app.Run();