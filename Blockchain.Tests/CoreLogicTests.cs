using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Xunit;
using CsharpBlockchainNode.Core;
using CsharpBlockchainNode.Models;
using CsharpBlockchainNode.Services;

public class CoreLogicTests
{
    private static IConfiguration TestConfig(int difficulty = 1) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Blockchain:Difficulty"] = difficulty.ToString(),
            ["Genesis:Allocations:0:to"] = "Alice",
            ["Genesis:Allocations:0:amount"] = "100",
            ["Genesis:Allocations:1:to"] = "Bob",
            ["Genesis:Allocations:1:amount"] = "50"
        }).Build();

    [Fact]
    public void Mining_applies_reward_and_txs_correctly()
    {
        var ws = new WalletService();
        var bc = new Blockchain(1, ws, null, TestConfig(1));

        Assert.Equal(100, ws.GetBalance("Alice"));
        Assert.Equal(50,  ws.GetBalance("Bob"));

        // Alice -> Bob 3
        Assert.True(bc.AddTransaction(new Transaction("Alice","Bob",3)));
        bc.MinePendingTransactions("M1");

        Assert.Equal(97, ws.GetBalance("Alice"));
        Assert.Equal(53, ws.GetBalance("Bob"));
        Assert.Equal(1,  ws.GetBalance("M1"));
    }

    [Fact]
    public void ReplaceChain_rebuilds_balances_from_longer_valid_chain()
    {
        var wsA = new WalletService();
        var a = new Blockchain(1, wsA, null, TestConfig(1));

        var wsB = new WalletService();
        var b = new Blockchain(1, wsB, null, TestConfig(1));

        // A mines 2 blocks, B mines 1
        a.MinePendingTransactions("MA");
        a.MinePendingTransactions("MA");
        b.MinePendingTransactions("MB");

        // Now B adopts A's longer chain
        Assert.True(b.ReplaceChain(a.Chain));
        Assert.Equal(a.Chain.Count, b.Chain.Count);
        Assert.Equal(wsA.GetBalance("MA"), wsB.GetBalance("MA"));
    }

    [Fact]
    public void TryAcceptExternalBlock_accepts_valid_rejects_invalid()
    {
        var ws = new WalletService();
        var bc = new Blockchain(1, ws, null, TestConfig(1));

        // Create a valid next block by mining in a throwaway chain and reusing it.
        var ws2 = new WalletService();
        var bc2 = new Blockchain(1, ws2, new List<Block>(bc.Chain), TestConfig(1));
        bc2.MinePendingTransactions("Mx");
        var valid = bc2.GetLatestBlock();

        // Accept valid
        Assert.True(bc.TryAcceptExternalBlock(valid));

        // Tamper with hash (invalid PoW)
        var tampered = new Block(valid.Index + 1,
                                 valid.Timestamp,
                                 valid.Transactions,
                                 valid.Hash)
        { Hash = "deadbeef" };
        Assert.False(bc.TryAcceptExternalBlock(tampered));
    }
}
