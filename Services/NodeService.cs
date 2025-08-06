using System.Text;
using System.Text.Json;
using CsharpBlockchainNode.Models;
using CsharpBlockchainNode.Core;

namespace CsharpBlockchainNode.Services;


/// Handles all peer-to-peer (P2P) networking logic.
public class NodeService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public NodeService(IConfiguration configuration)
    {
        _httpClient = new HttpClient();
        _configuration = configuration;
    }

    /// Broadcasts a newly mined block to all known peers in the network.
    public async Task BroadcastNewBlockAsync(Block block)
    {
        // Get the list of peer nodes and this node's own URL from configuration
        var peerNodes = _configuration.GetSection("PeerNodes").Get<string[]>();
        var selfUrl = _configuration.GetValue<string>("NodeUrl");

        if (peerNodes == null)
        {
            Console.WriteLine("No peer nodes configured.");
            return;
        }

        var json = JsonSerializer.Serialize(block);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        foreach (var peer in peerNodes)
        {
            // Don't broadcast to yourself
            if (peer.Equals(selfUrl, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                Console.WriteLine($"Broadcasting new block to {peer}...");
                var response = await _httpClient.PostAsync($"{peer}/announce-block", content);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to broadcast to {peer}. Reason: {ex.Message}");
            }
        }
    }
    

    /// Queries all peer nodes for their chains and replaces the local chain
    /// if a longer, valid chain is found.
    /// param: blockCoin - The local blockchain instance.
    /// returns True if the local chain was replaced, false otherwise.
    public async Task<bool> ResolveConflictsAsync(Blockchain blockCoin)
    {
        var peerNodes = _configuration.GetSection("PeerNodes").Get<string[]>();
        if (peerNodes == null) return false;

        List<Block>? longestChain = null;
        int maxLength = blockCoin.Chain.Count;

        foreach (var peer in peerNodes)
        {
            try
            {
                Console.WriteLine($"Querying chain from peer: {peer}...");
                // Get the chain from the peer
                var response = await _httpClient.GetAsync($"{peer}/chain");
                response.EnsureSuccessStatusCode();

                var peerChain = await response.Content.ReadFromJsonAsync<List<Block>>();

                // Check if the peer's chain is longer and valid
                if (peerChain != null && peerChain.Count > maxLength && blockCoin.IsChainValid(peerChain))
                {
                    maxLength = peerChain.Count;
                    longestChain = peerChain;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to query chain from {peer}. Reason: {ex.Message}");
            }
        }

        // If we found a valid chain longer than our own, replace ours
        if (longestChain != null)
        {
            return blockCoin.ReplaceChain(longestChain);
        }

        return false;
    }
}