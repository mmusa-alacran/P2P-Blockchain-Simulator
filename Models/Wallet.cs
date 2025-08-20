namespace CsharpBlockchainNode.Models;

public class Wallet
{
    /// The public address of the wallet.
    /// 'init' allows this to be set during object creation/deserialization,
    /// but makes it read-only afterwards.
    public string PublicKey { get; init; }

    /// The current balance of the wallet.
    public double Balance { get; set; }

    /// This constructor is now compatible with the JSON deserializer because
    /// the parameter name 'balance' matches the property name 'Balance' (case-insensitive).
    public Wallet(string publicKey, double balance = 0)
    {
        PublicKey = publicKey;
        Balance = balance;
    }
}