using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using CsharpBlockchainNode.Core;

namespace CsharpBlockchainNode.Services;

/// <summary>
/// Reports overall node health: is our chain valid and how tall is it.
/// </summary>
public sealed class ChainHealthCheck : IHealthCheck
{
    private readonly Blockchain _bc;

    public ChainHealthCheck(Blockchain bc) => _bc = bc;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var valid = _bc.IsChainValid();
        var tip = _bc.GetLatestBlock();

        // Use non-nullable object values for HealthCheckResult.* factory methods
        var data = new Dictionary<string, object>
        {
            ["height"] = tip.Index,
            ["tipHash"] = tip.Hash
        };

        return Task.FromResult(valid
            ? HealthCheckResult.Healthy("Chain is valid.", data)
            : HealthCheckResult.Unhealthy("Chain is invalid!", exception: null, data: data));
    }
}
