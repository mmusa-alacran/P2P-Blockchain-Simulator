using System.Collections.Generic;
using System.Globalization;
using CsharpBlockchainNode.Core;
using CsharpBlockchainNode.Models;
using CsharpBlockchainNode.Services;
using Microsoft.Extensions.Configuration;
using Xunit;


namespace CsharpBlockchainNode.Tests;

public class ConsensusTests
{
    [Fact]
    public void ReplaceChain_Accepts_Longer_Valid_Chain()
    {
        var (a, _) = TestUtils.MakeBlockchain(difficulty: 1, alice: 10m);
        var (b, _) = TestUtils.MakeBlockchain(difficulty: 1, alice: 10m);

        b.AddTransaction(new Transaction("Alice","Bob",1m));
        b.MinePendingTransactions("M1");
        b.MinePendingTransactions("M1");

        Assert.True(a.ReplaceChain(b.Chain));
        Assert.True(a.IsChainValid());
        Assert.Equal(b.Chain.Count, a.Chain.Count);
    }

    [Fact]
    public void ChainValidation_Fails_On_Tamper()
    {
        var (bc, _) = TestUtils.MakeBlockchain(difficulty: 1);
        bc.MinePendingTransactions("M");

        var tip = bc.GetLatestBlock();
        var bad = new CsharpBlockchainNode.Models.Block(tip.Index, tip.Timestamp, tip.Transactions, previousHash: "hack");
        bad.Nonce = tip.Nonce; bad.Hash = bad.CalculateHash();

        var cloned = new List<CsharpBlockchainNode.Models.Block>(bc.Chain);
        cloned[^1] = bad;
        Assert.False(bc.IsChainValid(cloned));
    }
}
