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
        // Get the list of peer nodes from our configuration
        var peerNodes = _configuration.GetSection("PeerNodes").Get<string[]>();
        if (peerNodes == null)
        {
            Console.WriteLine("No peer nodes configured.");
            return;
        }

        var json = JsonSerializer.Serialize(block);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        foreach (var peer in peerNodes)
        {
            try
            {
                Console.WriteLine($"Broadcasting new block to {peer}...");
                // Send the block to the peer's '/announce-block' endpoint
                var response = await _httpClient.PostAsync($"{peer}/announce-block", content);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                // If we can't connect to a peer, just log it and continue.
                // In a real blockchain, we would have more robust retry/disconnect logic.
                Console.WriteLine($"Failed to broadcast to {peer}. Reason: {ex.Message}");
            }
        }
    }
}