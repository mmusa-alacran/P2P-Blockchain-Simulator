using System.Collections.Generic;
using System.Globalization;
using CsharpBlockchainNode.Core;
using CsharpBlockchainNode.Models;
using CsharpBlockchainNode.Services;
using Microsoft.Extensions.Configuration;
using Xunit;


namespace CsharpBlockchainNode.Tests;

public class TransactionTests
{
    [Fact]
    public void Rejects_Negative_Or_Zero_Amounts()
    {
        var (bc, _) = TestUtils.MakeBlockchain();
        Assert.False(bc.AddTransaction(new Transaction("Alice", "Bob", 0m)));
        Assert.False(bc.AddTransaction(new Transaction("Alice", "Bob", -1m)));
    }

    [Fact]
    public void Rejects_When_Insufficient_Balance()
    {
        var (bc, _) = TestUtils.MakeBlockchain(alice: 1m, bob: 0m);
        Assert.False(bc.AddTransaction(new Transaction("Bob", "Alice", 10m)));
        Assert.False(bc.AddTransaction(new Transaction("Alice", "Bob", 2m)));
    }

    [Fact]
    public void Applies_Balances_On_Mined_Block()
    {
        var (bc, ws) = TestUtils.MakeBlockchain(alice: 5m, bob: 0m, difficulty: 1);
        bc.AddTransaction(new Transaction("Alice", "Bob", 2m));
        bc.MinePendingTransactions("Miner");
        Assert.Equal(3m, ws.GetBalance("Alice"));
        Assert.Equal(2m, ws.GetBalance("Bob"));
        Assert.Equal(1m, ws.GetBalance("Miner"));
    }
}
