using System.Collections.Generic;
using System.Globalization;
using CsharpBlockchainNode.Core;
using CsharpBlockchainNode.Models;
using CsharpBlockchainNode.Services;
using Microsoft.Extensions.Configuration;
using Xunit;


namespace CsharpBlockchainNode.Tests;

internal static class TestUtils
{
    public static IConfiguration MakeConfig(int difficulty = 2, decimal alice = 10m, decimal bob = 0m)
    {
        var dict = new Dictionary<string, string?>
        {
            ["Blockchain:Difficulty"] = difficulty.ToString(),
            ["Genesis:Allocations:0:to"] = "Alice",
            ["Genesis:Allocations:0:amount"] = alice.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Genesis:Allocations:1:to"] = "Bob",
            ["Genesis:Allocations:1:amount"] = bob.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        return new ConfigurationBuilder().AddInMemoryCollection(dict!).Build();
    }

    public static (Blockchain bc, WalletService ws) MakeBlockchain(int difficulty = 2, decimal alice = 10m, decimal bob = 0m)
    {
        var ws = new WalletService();
        var cfg = MakeConfig(difficulty, alice, bob);
        var bc = new Blockchain(difficulty, ws, null, cfg);
        return (bc, ws);
    }
}
