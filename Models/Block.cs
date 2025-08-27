using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;

namespace CsharpBlockchainNode.Models;

/// <summary>
/// Centralized serializer options to ensure deterministic hashing:
/// </summary>
static class HashingJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}

/// <summary>Represents a single block in the blockchain.</summary>
public class Block
{
    /// <summary>Position of the block in the chain (0 = genesis).</summary>
    public int Index { get; }

    /// <summary>Unix seconds when the block was created.</summary>
    public long Timestamp { get; }

    /// <summary>Transactions included in this block.</summary>
    public List<Transaction> Transactions { get; }

    /// <summary>Nonce used to search for a valid hash (Proof of Work).</summary>
    public int Nonce { get; set; } = 0;

    /// <summary>Hash of the previous block.</summary>
    public string PreviousHash { get; }

    /// <summary>Current block hash (computed from content + nonce).</summary>
    public string Hash { get; set; }

    public Block(int index, long timestamp, List<Transaction> transactions, string previousHash)
    {
        Index = index;
        Timestamp = timestamp;
        Transactions = transactions;
        PreviousHash = previousHash;
        Hash = CalculateHash(); // compute immediately with Nonce = 0
    }

    /// <summary>
    /// Compute SHA-256 over a compact JSON of {Index, Timestamp, Transactions, Nonce, PreviousHash}.
    /// This must be deterministic across nodes.
    /// </summary>
    public string CalculateHash()
    {
        using var sha256 = SHA256.Create();

        var dataToHash = new { Index, Timestamp, Transactions, Nonce, PreviousHash };
        var dataAsString = JsonSerializer.Serialize(dataToHash, HashingJson.Options);

        var bytes = Encoding.UTF8.GetBytes(dataAsString);
        var hashBytes = sha256.ComputeHash(bytes);

        // hex-encode lower-case
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}
