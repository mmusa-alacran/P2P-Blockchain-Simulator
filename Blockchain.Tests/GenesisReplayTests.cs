using System.Collections.Generic;
using CsharpBlockchainNode.Core;
using CsharpBlockchainNode.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CsharpBlockchainNode.Tests;

public class GenesisReplayTests
{
    private static IConfiguration NewConfig(int difficulty = 1)
    {
        // config for the chain + empty genesis balances
        var dict = new Dictionary<string, string>
        {
            ["Blockchain:Difficulty"] = difficulty.ToString(),
            ["Genesis:Allocations:0:To"] = "Alice",
            ["Genesis:Allocations:0:Amount"] = "0",
            ["Genesis:Allocations:1:To"] = "Bob",
            ["Genesis:Allocations:1:Amount"] = "0",
        };
        return new ConfigurationBuilder().AddInMemoryCollection(dict!).Build();
    }

    [Fact]
    public void RebuildWalletsFromChain_Recomputes_After_Tamper_Replacement()
    {
        var cfg = NewConfig(1);
        var ws  = new WalletService();
        var bc  = new Blockchain(difficulty: 1, walletService: ws, initialChain: null, config: cfg);

        const string miner = "M";
        // Mine 3 blocks to THE SAME miner to make the expectation deterministic
        bc.MinePendingTransactions(miner);
        bc.MinePendingTransactions(miner);
        bc.MinePendingTransactions(miner);

        Assert.Equal(3m, ws.GetBalance(miner)); // sanity before tamper

        // Tamper wallet state to simulate corruption
        ws.SetBalance(miner, 0m);
        Assert.Equal(0m, ws.GetBalance(miner));

        // Replay from chain should restore 3
        bc.RebuildWalletsFromChain();
        Assert.Equal(3m, ws.GetBalance(miner));
    }
}
