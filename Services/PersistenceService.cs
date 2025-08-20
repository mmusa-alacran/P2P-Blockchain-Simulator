using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CsharpBlockchainNode.Models;
using Microsoft.Extensions.Configuration;

namespace CsharpBlockchainNode.Services;

public class PersistenceService
{
    private readonly string _stateFilePath;

    public PersistenceService(IConfiguration configuration)
    {
        // We create a unique filename for each node based on its URL to avoid conflicts.
        var nodeUrl = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? configuration.GetValue<string>("NodeUrl")!;
        var port = new System.Uri(nodeUrl).Port;
        _stateFilePath = $"blockchain_state_{port}.json";
        Console.WriteLine($"State will be persisted to: {_stateFilePath}");
    }

    /// Saves the current blockchain state to a JSON file.
    public async Task SaveStateAsync(BlockchainState state)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(state, options);
        await File.WriteAllTextAsync(_stateFilePath, json);
    }

    /// Loads the blockchain state from a JSON file.
    /// returns: The loaded state, or null if no state file exists.
    public async Task<BlockchainState?> LoadStateAsync()
    {
        if (!File.Exists(_stateFilePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(_stateFilePath);
        return JsonSerializer.Deserialize<BlockchainState>(json);
    }
}