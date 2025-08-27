using System.Collections.Concurrent;
using System.Linq;
using CsharpBlockchainNode.Models;

namespace CsharpBlockchainNode.Services;

/// <summary>
/// Central place to manage wallet state: create wallets, query balances,
/// and apply deterministic snapshots loaded from disk or replayed from the chain.
/// </summary>
public class WalletService
{
    // Keep this private to enforce encapsulation (no external mutation).
    private readonly ConcurrentDictionary<string, Wallet> _wallets = new();

    /// <summary>Create a new wallet or return the existing one.</summary>
    public Wallet GetOrCreateWallet(string publicKey)
        => _wallets.GetOrAdd(publicKey, key => new Wallet(key));

    /// <summary>Return the current balance for an address (0 if unknown).</summary>
    public decimal GetBalance(string publicKey)
        => _wallets.TryGetValue(publicKey, out var wallet) ? wallet.Balance : 0m;

    /// <summary>Returns a simple address -> balance view for inspection endpoints.</summary>
    public Dictionary<string, decimal> GetAllBalances()
        => _wallets.ToDictionary(kv => kv.Key, kv => kv.Value.Balance);


    /// <summary>
    /// Forcibly set a wallet's balance (creates the wallet if missing).
    /// Used by chain replay and transaction application.
    /// </summary>
    public void SetBalance(string address, decimal newBalance)
    {
        _wallets.AddOrUpdate(
            address,
            _ => new Wallet(address, newBalance),
            (_, w) => { w.Balance = newBalance; return w; }
        );
    }

    /// <summary>
    /// Immutable snapshot of all wallets for persistence.
    /// </summary>
    public Dictionary<string, Wallet> Snapshot()
        => _wallets.ToDictionary(kv => kv.Key, kv => new Wallet(kv.Value.PublicKey, kv.Value.Balance));

    /// <summary>
    /// Load a full wallet map, replacing the current state (used on startup).
    /// </summary>
    public void LoadWallets(IDictionary<string, Wallet> walletsToLoad)
    {
        _wallets.Clear();
        foreach (var (addr, wallet) in walletsToLoad)
            _wallets.TryAdd(addr, new Wallet(wallet.PublicKey, wallet.Balance));
    }
}
