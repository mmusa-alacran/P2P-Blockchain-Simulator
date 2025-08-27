using System.Text.Json;
using CsharpBlockchainNode.Models;
using Microsoft.Extensions.Configuration;

namespace CsharpBlockchainNode.Services;

/// <summary>
/// Persists the node's chain and wallet snapshot to a JSON file.
/// Each node writes to a file unique to its bound HTTP port.
/// </summary>
public class PersistenceService
{
    private readonly string _stateFilePath;

    public PersistenceService(IConfiguration configuration)
    {
        // Respect command-line/host settings first: configuration["urls"] is set by --urls.
        // Fallback to ASPNETCORE_URLS, then NodeUrl from appsettings, then a default.
        var urls = configuration["urls"]
                   ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
                   ?? configuration.GetValue<string>("NodeUrl")
                   ?? "http://localhost:5000";

        // If multiple URLs are provided, use the first.
        var firstUrl = urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];

        int port;
        try
        {
            var uri = new Uri(firstUrl);
            port = uri.IsDefaultPort
                ? (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? 5001 : 5000)
                : uri.Port;
        }
        catch
        {
            port = 5000; // safe fallback if parsing fails
        }

        _stateFilePath = $"blockchain_state_{port}.json";
        Console.WriteLine($"State will be persisted to: {_stateFilePath}");
    }

    public async Task SaveStateAsync(BlockchainState state)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(state, options);
        await File.WriteAllTextAsync(_stateFilePath, json);
    }

    public async Task<BlockchainState?> LoadStateAsync()
    {
        if (!File.Exists(_stateFilePath)) return null;
        var json = await File.ReadAllTextAsync(_stateFilePath);
        return JsonSerializer.Deserialize<BlockchainState>(json);
    }
}
