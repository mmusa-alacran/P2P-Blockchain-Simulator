using System.Text;
using System.Text.Json;
using CsharpBlockchainNode.Models;
using CsharpBlockchainNode.Core;

namespace CsharpBlockchainNode.Services;

/// Handles all peer-to-peer (P2P) networking logic.
public class NodeService
{
    private readonly HttpClient _httpClient = new();
    private readonly IConfiguration _configuration;

    public NodeService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// Broadcasts a newly mined block to all known peers in the network.
    public async Task BroadcastNewBlockAsync(Block block, string selfUrl)
    {
        // Get the list of peer nodes and this node's own URL from configuration
        var peerNodes = _configuration.GetSection("PeerNodes").Get<string[]>();
        if (peerNodes == null) return;

        var json = JsonSerializer.Serialize(block);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        foreach (var peer in peerNodes)
        {
            if (peer.Equals(selfUrl, StringComparison.OrdinalIgnoreCase)) continue;
            
            try
            {
                Console.WriteLine($"Broadcasting new block to {peer}...");
                await _httpClient.PostAsync($"{peer}/announce-block", content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to broadcast to {peer}. Reason: {ex.Message}");
            }
        }
    }


    /// Queries all peer nodes for their chains and replaces the local chain
    /// if a longer, valid chain is found.
    public async Task<bool> ResolveConflictsAsync(Blockchain blockCoin, string selfUrl)
    {
        Console.WriteLine("--- Starting Conflict Resolution ---");
        var peerNodes = _configuration.GetSection("PeerNodes").Get<string[]>();
        if (peerNodes == null || !peerNodes.Any()) return false;

        Console.WriteLine($"Found {peerNodes.Length} peer(s) to query.");
        List<Block>? longestChain = null;
        int maxLength = blockCoin.Chain.Count;
        Console.WriteLine($"Current chain length is {maxLength}.");

        foreach (var peer in peerNodes)
        {
                if (peer.Equals(selfUrl, StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                Console.WriteLine($"Querying chain from peer: {peer}...");
                var response = await _httpClient.GetAsync($"{peer}/chain");
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to get chain from {peer}. Status: {response.StatusCode}");
                    continue;
                }
                var peerChain = await response.Content.ReadFromJsonAsync<List<Block>>();
                if (peerChain != null && peerChain.Count > maxLength && blockCoin.IsChainValid(peerChain))
                {
                    Console.WriteLine($"Found a longer, valid chain from {peer}.");
                    maxLength = peerChain.Count;
                    longestChain = peerChain;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"!! EXCEPTION while querying {peer}. Reason: {ex.Message}");
            }
        }

        if (longestChain != null)
        {
            return blockCoin.ReplaceChain(longestChain);
        }
        Console.WriteLine("--- Conflict Resolution Finished ---");
        return false;
    }
}