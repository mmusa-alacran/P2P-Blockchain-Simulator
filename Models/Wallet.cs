namespace CsharpBlockchainNode.Models;

/// Represents a user's wallet, tracking their public address and balance.
public class Wallet
{
    /// The public address of the wallet, used as a unique identifier.
    public string PublicKey { get; }
    /// The current balance of the wallet.
    public double Balance { get; set; }

    public Wallet(string publicKey, double initialBalance = 0)
    {
        PublicKey = publicKey;
        Balance = initialBalance;
    }
}