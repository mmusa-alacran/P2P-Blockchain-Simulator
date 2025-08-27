using System.Collections.Generic;
using System.Globalization;
using CsharpBlockchainNode.Core;
using CsharpBlockchainNode.Models;
using CsharpBlockchainNode.Services;
using Microsoft.Extensions.Configuration;
using Xunit;


namespace CsharpBlockchainNode.Tests;

public class MiningTests
{
    [Fact]
    public void Mine_Produces_Valid_Block_With_PoW()
    {
        var (bc, _) = TestUtils.MakeBlockchain(difficulty: 2, alice: 10m, bob: 0m);
        bc.AddTransaction(new Transaction("Alice", "Bob", 1m));
        bc.MinePendingTransactions("MinerX");
        var tip = bc.GetLatestBlock();
        Assert.StartsWith("00", tip.Hash);
        Assert.True(bc.IsChainValid());
    }

    [Fact]
    public void Mining_Pays_Reward_To_Miner()
    {
        var (bc, ws) = TestUtils.MakeBlockchain(difficulty: 1, alice: 0m, bob: 0m);
        bc.MinePendingTransactions("MinerY");
        Assert.Equal(1m, ws.GetBalance("MinerY"));
    }

    [Fact]
    public void Mining_Consumes_Mempool()
    {
        var (bc, _) = TestUtils.MakeBlockchain(difficulty: 1, alice: 3m, bob: 0m);
        bc.AddTransaction(new Transaction("Alice", "Bob", 2m));
        bc.MinePendingTransactions("MinerZ");
        Assert.Empty(bc.PendingTransactions);
        Assert.True(bc.IsChainValid());
    }
}
