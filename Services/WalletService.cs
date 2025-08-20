using System.Collections.Concurrent;
using CsharpBlockchainNode.Models;

namespace CsharpBlockchainNode.Services;

/// Manages the state of all wallets in the network, including creation and balance lookups.
/// This service acts as the "account book" of the blockchain.
public class WalletService
{
    // A thread-safe dictionary to store wallets, mapping public keys (addresses) to Wallet objects.
    internal readonly ConcurrentDictionary<string, Wallet> _wallets = new();

    /// Creates a new wallet with an initial balance or retrieves it if it already exists.
    /// param: "publicKey", the unique public address for the wallet.
    /// returns the created or retrieved wallet.</returns>
    public Wallet GetOrCreateWallet(string publicKey)
    {
        // This will add the wallet if it doesn't exist, or just return the existing one if it does.
        return _wallets.GetOrAdd(publicKey, key => new Wallet(key));
    }

    /// Gets the current balance for a given wallet address.
    /// param: "publicKey", the address of the wallet to check.
    /// returns the balance of the wallet, or 0 if the wallet doesn't exist.
    public double GetBalance(string publicKey)
    {
        return _wallets.TryGetValue(publicKey, out var wallet) ? wallet.Balance : 0;
    }

    /// Forcibly sets the balance of a wallet. Used for processing transactions.
    /// It creates the wallet if it doesn't exist.
    /// param: "publicKey", the address of the wallet to update.
    /// param: "newBalance", the new balance to set.
    public void SetBalance(string publicKey, double newBalance)
    {
        var wallet = GetOrCreateWallet(publicKey);
        wallet.Balance = newBalance;
    }


    /// Loads a dictionary of wallets into the service, overwriting any existing data.
    /// Used when loading state from disk.
    public void LoadWallets(IDictionary<string, Wallet> walletsToLoad)
    {
        _wallets.Clear();
        foreach (var walletEntry in walletsToLoad)
        {
            _wallets.TryAdd(walletEntry.Key, walletEntry.Value);
        }
    }
}