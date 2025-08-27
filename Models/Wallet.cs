namespace CsharpBlockchainNode.Models;

/// <summary>Simple wallet model: address + decimal balance.</summary>
public class Wallet
{
    /// <summary>Public address (identifier) of the wallet.</summary>
    public string PublicKey { get; init; }

    /// <summary>Current balance (decimal to avoid floating rounding).</summary>
    public decimal Balance { get; set; }

    public Wallet(string publicKey, decimal balance = 0m)
    {
        PublicKey = publicKey;
        Balance = balance;
    }
}
