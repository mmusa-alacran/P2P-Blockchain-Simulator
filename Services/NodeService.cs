using System.Text;
using System.Text.Json;
using CsharpBlockchainNode.Models;

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
}