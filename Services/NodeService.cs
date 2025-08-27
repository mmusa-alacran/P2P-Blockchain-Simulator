using System.Collections.Concurrent;
using System.Net.Http.Json;
using CsharpBlockchainNode.Core;
using CsharpBlockchainNode.Models;
using Microsoft.Extensions.Configuration;

namespace CsharpBlockchainNode.Services;

/// <summary>
/// Keeps a union of configured peers plus those registered at runtime,
/// and provides “gossip-lite” helpers (broadcast announce + longest-chain resolution).
/// </summary>
/// <remarks>
/// Peers are normalized to avoid trivial duplicates (e.g., trailing slash).
/// Only dynamic peers can be unregistered; configuration peers are treated as static.
/// Network calls are best-effort; failures are logged and ignored to keep the node responsive.
/// </remarks>

public class NodeService
{
    private readonly IConfiguration _config;
    private readonly HttpClient _http = new();
    // runtime peers: thread-safe set keyed by URL
    private readonly ConcurrentDictionary<string, byte> _dynamicPeers = new(StringComparer.OrdinalIgnoreCase);

    public NodeService(IConfiguration config) => _config = config;

    /// <summary>Peers from appsettings + any registered at runtime.</summary>
    public IEnumerable<string> Peers =>
        ((_config.GetSection("PeerNodes").Get<string[]>() ?? Array.Empty<string>())
        .Concat(_dynamicPeers.Keys))
        .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>Convenience for callers (Program.cs expects a method).</summary>
    public IEnumerable<string> GetPeers() => Peers;

    /// <summary>Register a peer URL at runtime.</summary>
    public bool RegisterPeer(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out _)) return false;
        _dynamicPeers.TryAdd(url.TrimEnd('/'), 0);
        Console.WriteLine($"[P2P] Registered peer: {url}");
        return true;
    }

    /// <summary>Unregister a dynamically added peer (config peers remain).</summary>
    public bool UnregisterPeer(string url)
    {
        var key = url.TrimEnd('/');
        var removed = _dynamicPeers.TryRemove(key, out _);
        if (removed) Console.WriteLine($"[P2P] Unregistered peer: {url}");
        else Console.WriteLine($"[P2P] Unregister ignored (not in dynamic set): {url}");
        return removed;
    }

    /// <summary>Broadcast a newly mined block to all peers (except self).</summary>
    public async Task BroadcastNewBlockAsync(Block block, string selfUrl)
    {
        foreach (var peer in Peers.Where(p => !string.Equals(p, selfUrl, StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var resp = await _http.PostAsJsonAsync($"{peer}/announce", block);
                if (!resp.IsSuccessStatusCode)
                    Console.WriteLine($"[P2P] Peer {peer} rejected block: {resp.StatusCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[P2P] Broadcast to {peer} failed: {ex.Message}");
            }
        }
    }

    // private async Task<bool> TryPostAsync<T>(string url, T payload)
    // {
    //     try
    //     {
    //         var resp = await _http.PostAsJsonAsync(url, payload);
    //         if (!resp.IsSuccessStatusCode)
    //         {
    //             Console.WriteLine($"[P2P] POST {url} -> {(int)resp.StatusCode}");
    //             return false;
    //         }
    //         return true;
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine($"[P2P] POST {url} failed: {ex.Message}");
    //         return false;
    //     }
    // }

    /// <summary>Classic "longest valid chain wins" resolution from peers.</summary>
    public async Task<bool> ResolveConflictsAsync(Blockchain bc, string selfUrl)
    {
        var longest = bc.Chain;

        foreach (var peer in Peers.Where(p => !string.Equals(p, selfUrl, StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var peerChain = await _http.GetFromJsonAsync<List<Block>>($"{peer}/chain");
                if (peerChain is null) continue;

                if (peerChain.Count > longest.Count && bc.IsChainValid(peerChain))
                    longest = peerChain;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[P2P] Failed to fetch chain from {peer}: {ex.Message}");
            }
        }

        if (!ReferenceEquals(longest, bc.Chain))
        {
            var replaced = bc.ReplaceChain(longest);
            Console.WriteLine(replaced
                ? "[P2P] Replaced chain with a longer valid one."
                : "[P2P] Longer chain failed validation.");
            return replaced;
        }

        return false;
    }
}
