using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CsharpBlockchainNode.Models;


/// Represents a single block in our blockchain.
public class Block
{
   /// The position of the block in the chain.
   public int Index { get; }
   /// The timestamp of when the block was created.
   public long Timestamp { get; }


    /// The list of transactions included in this block.
    public List<Transaction> Transactions { get; }


    /// A number used by miners to find a valid hash.
    public int Nonce { get; set; } = 0;


    /// The hash of the previous block in the chain.
    public string PreviousHash { get; }

  
    /// The hash of the current block.
    /// This is calculated based on all other properties.
    public string Hash { get; set; }

    public Block(int index, long timestamp, List<Transaction> transactions, string previousHash)
    {
        Index = index;
        Timestamp = timestamp;
        Transactions = transactions;
        PreviousHash = previousHash;
        Hash = CalculateHash(); // Calculate hash upon creation
    }

    /// Calculates the SHA256 hash of the current block.
    /// The hash is based on a JSON representation of the block's essential data.
    /// A SHA256 hash string
    public string CalculateHash()
    {
        using var sha256 = SHA256.Create();

        // We create a string representation of the data to be hashed.
        var dataToHash = new
        {
            Index,
            Timestamp,
            Transactions,
            Nonce,
            PreviousHash
        };

        var dataAsString = JsonSerializer.Serialize(dataToHash);
        var bytes = Encoding.UTF8.GetBytes(dataAsString);
        var hashBytes = sha256.ComputeHash(bytes);

        // Convert byte array to a hexadecimal string
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}