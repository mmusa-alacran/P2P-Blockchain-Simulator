using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CsharpBlockchainNode.Core;
using CsharpBlockchainNode.Models;
using CsharpBlockchainNode.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

public class ChainHealthCheckTests
{
    private static IConfiguration Cfg() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["Genesis:Allocations:0:to"] = "Alice",
                ["Genesis:Allocations:0:amount"] = "1"
            })
            .Build();

    [Fact]
    public async Task Reports_Healthy_With_Height_And_Tip()
    {
        var wallets = new WalletService();
        var bc = new Blockchain(difficulty: 1, walletService: wallets, initialChain: null, config: Cfg());
        var hc = new ChainHealthCheck(bc);

        var result = await hc.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.True(result.Data.ContainsKey("height"));
        Assert.True(result.Data.ContainsKey("tipHash"));
    }
}
