using CsharpBlockchainNode.Core;
using CsharpBlockchainNode.Models;
using CsharpBlockchainNode.Services;

var builder = WebApplication.CreateBuilder(args);
var difficulty = builder.Configuration.GetValue<int>("Blockchain:Difficulty");
var walletService = new WalletService();
walletService.GetOrCreateWallet("Alice").Balance = 100;
walletService.GetOrCreateWallet("Bob").Balance = 50;
builder.Services.AddSingleton(walletService);
builder.Services.AddSingleton(new Blockchain(difficulty, walletService));
builder.Services.AddSingleton<NodeService>();
var app = builder.Build();

app.MapGet("/chain", (Blockchain blockCoin) => Results.Ok(blockCoin.Chain));
app.MapGet("/wallets/{address}/balance", (string address, WalletService walletSvc) => Results.Ok(new { address, balance = walletSvc.GetBalance(address) }));
app.MapPost("/transactions/new", (Transaction transaction, Blockchain blockCoin) =>
{
    return blockCoin.AddTransaction(transaction)
        ? Results.Ok(new { message = "Transaction added to pending pool." })
        : Results.BadRequest(new { message = "Transaction failed validation." });
});

app.MapPost("/mine", async (Blockchain blockCoin, NodeService nodeService, IConfiguration config) =>
{
    const string minerAddress = "my-miner-address";
    blockCoin.MinePendingTransactions(minerAddress);
    var newBlock = blockCoin.GetLatestBlock();
    var selfUrl = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? config.GetValue<string>("NodeUrl")!;
    await nodeService.BroadcastNewBlockAsync(newBlock, selfUrl);
    return Results.Ok(new { message = "New block mined and broadcast.", block = newBlock });
});

app.MapPost("/announce-block", (Block block, Blockchain blockCoin) => {
    if (block.PreviousHash == blockCoin.GetLatestBlock().Hash)
    {
        blockCoin.Chain.Add(block);
        return Results.Ok(new { message = "Block accepted." });
    }
    return Results.BadRequest(new { message = "Block rejected." });
});

app.MapGet("/nodes/resolve", async (Blockchain blockCoin, NodeService nodeService, IConfiguration config) =>
{
    var selfUrl = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? config.GetValue<string>("NodeUrl")!;
    bool chainReplaced = await nodeService.ResolveConflictsAsync(blockCoin, selfUrl);
    if (chainReplaced)
    {
        return Results.Ok(new { message = "Our chain was replaced.", chain = blockCoin.Chain });
    }
    return Results.Ok(new { message = "Our chain is authoritative.", chain = blockCoin.Chain });
});

var appUrl = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? builder.Configuration.GetValue<string>("NodeUrl") ?? "http://localhost:5000";
Console.WriteLine($"Node starting on URL: {appUrl}");
app.Run(appUrl);