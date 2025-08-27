using System.Collections.Generic;
using CsharpBlockchainNode.Models;
using CsharpBlockchainNode.Services;
using Xunit;

public class WalletServiceTests
{
    [Fact]
    public void SetBalance_CreatesOrUpdates()
    {
        var ws = new WalletService();
        Assert.Equal(0m, ws.GetBalance("A"));

        ws.SetBalance("A", 10m);
        Assert.Equal(10m, ws.GetBalance("A"));

        ws.SetBalance("A", 3m);
        Assert.Equal(3m, ws.GetBalance("A"));
    }

    [Fact]
    public void Snapshot_And_LoadWallets_RoundTrip()
    {
        var ws = new WalletService();
        ws.SetBalance("A", 1m);
        ws.SetBalance("B", 2m);

        var snap = ws.Snapshot();
        var clone = new WalletService();
        clone.LoadWallets(snap);

        Assert.Equal(1m, clone.GetBalance("A"));
        Assert.Equal(2m, clone.GetBalance("B"));
    }
}
