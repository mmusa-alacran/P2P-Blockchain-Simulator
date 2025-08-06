// This is a temporary file to test our core blockchain logic.
// It will be replaced by the ASP.NET Core API setup later.

using System.Text.Json;
using CsharpBlockchainNode.Core;
using CsharpBlockchainNode.Models;

Console.WriteLine("Starting C# Blockchain Node...");

// 1. Initialize the blockchain with a difficulty of 2
// This means hashes must start with "00"
var blockCoin = new Blockchain(difficulty: 2);
Console.WriteLine("Blockchain created. Genesis block hash: " + blockCoin.GetLatestBlock().Hash);
Console.WriteLine("----------------------------------\n");


// 2. Add some pending transactions
Console.WriteLine("Adding transactions...");
blockCoin.AddTransaction(new Transaction("Alice", "Bob", 50));
blockCoin.AddTransaction(new Transaction("Charlie", "David", 10));
Console.WriteLine("----------------------------------\n");


// 3. Mine a new block
Console.WriteLine("Mining block 1...");
// "MinerX" will receive the mining reward
blockCoin.MinePendingTransactions("MinerX"); 
Console.WriteLine("Block 1 successfully mined. New block hash: " + blockCoin.GetLatestBlock().Hash);
Console.WriteLine("----------------------------------\n");


// 4. Add more transactions
Console.WriteLine("Adding more transactions...");
blockCoin.AddTransaction(new Transaction("Eve", "Frank", 25));
Console.WriteLine("----------------------------------\n");


// 5. Mine another block
Console.WriteLine("Mining block 2...");
// "MinerY" will receive the reward this time
blockCoin.MinePendingTransactions("MinerY");
Console.WriteLine("Block 2 successfully mined. New block hash: " + blockCoin.GetLatestBlock().Hash);
Console.WriteLine("----------------------------------\n");


// 6. Print the entire blockchain
Console.WriteLine("Blockchain state:");
var options = new JsonSerializerOptions { WriteIndented = true };
Console.WriteLine(JsonSerializer.Serialize(blockCoin.Chain, options));
Console.WriteLine("----------------------------------\n");


// 7. Validate the chain
Console.WriteLine("Validating the chain...");
bool isChainValid = blockCoin.IsChainValid();
Console.WriteLine("Is chain valid? " + isChainValid);

// 8. Optional: Tamper with the chain to test validation
// Uncomment the lines below to see IsChainValid() return false
// Console.WriteLine("\nTampering with the chain...");
// blockCoin.Chain[1].Transactions[0] = new Transaction("Hacker", "Victim", 1000);
// blockCoin.Chain[1].Hash = blockCoin.Chain[1].CalculateHash(); // Recalculate hash after tampering
// Console.WriteLine("Is chain valid after tampering? " + blockCoin.IsChainValid());