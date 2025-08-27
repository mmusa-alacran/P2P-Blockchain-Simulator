using System.Text.Json;
using CsharpBlockchainNode.Models;
using CsharpBlockchainNode.Services;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Collections.Generic;


namespace CsharpBlockchainNode.Core;

/// <summary>
/// A compact, readable blockchain simulator:
/// * Append-only chain with Proof-of-Work (uniform difficulty across the network).
/// * “system” is the only minting address; it never spends.
/// * Wallet balances are the result of replaying all transactions from genesis.
/// </summary>
/// <remarks>
/// Design goals:
/// 1) Determinism: block hashes are computed from a stable JSON
/// 2) Simplicity: no real cryptography, networking, or mempool fees—this is a learning tool.
/// 3) Correctness: every acceptance path (local mining or external announce) applies balances exactly once.
/// 
/// Thread-safety:
///   The current implementation isn’t concurrent; if you add background mining or gossip,
///   protect Chain and _pendingTransactions with a lock or a dedicated actor.
/// </remarks>

public class Blockchain
{
    private readonly List<Transaction> _pendingTransactions = new();
    private readonly int _difficulty;
    private readonly WalletService _walletService;
    private readonly IConfiguration _config;

    /// <summary>The canonical chain (ledger).</summary>
    public List<Block> Chain { get; private set; }

    public IReadOnlyList<Transaction> PendingTransactions => _pendingTransactions.AsReadOnly();


    /// <summary>Construct a blockchain from optional saved chain and config.</summary>
   public Blockchain(int difficulty, WalletService walletService, List<Block>? initialChain, IConfiguration config)
   {
      _difficulty = difficulty;
      _walletService = walletService;
      _config = config;

      Chain = (initialChain is not null && initialChain.Count > 0)
            ? initialChain
            : new List<Block> { CreateGenesisBlock() };

      // Make wallet balances consistent with the chain on startup.
      RebuildWalletsFromChain();
   }

    // ---- Genesis ----

    private sealed record GenesisAlloc(string To, decimal Amount);

    /// <summary>Create the first block from configured allocations.</summary>
    private Block CreateGenesisBlock()
    {
        // "Genesis": { "Allocations": [ { "to":"Alice","amount":100 }, ... ] }
        var allocs = _config.GetSection("Genesis:Allocations").Get<List<GenesisAlloc>>()
                    ?? new() { new("Alice", 100m), new("Bob", 50m) };

        var txs = allocs.Select(a => new Transaction("system", a.To, a.Amount)).ToList();
        const long genesisTimestamp = 1672531200L; // fixed for deterministic hash
        return new Block(0, genesisTimestamp, txs, "0");
    }

    // ---- Core Operations ----

    /// <summary>Return the tip (latest block).</summary>
    public Block GetLatestBlock() => Chain[^1];

    /// <summary>
    /// Mine a block from all pending transactions and one reward transaction,
    /// then append to the chain and update balances.
    /// </summary>
    public void MinePendingTransactions(string minerAddress)
    {
        // Mining reward (one per mined block).
        const decimal rewardAmount = 1m;
        var rewardTransaction = new Transaction("system", minerAddress, rewardAmount);

        // Include all current pending txs + reward.
        var blockTransactions = new List<Transaction>(_pendingTransactions) { rewardTransaction };

        var newBlock = new Block(
            index: GetLatestBlock().Index + 1,
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            transactions: blockTransactions,
            previousHash: GetLatestBlock().Hash
        );

        Console.WriteLine($"[MINER] Starting mining: {_pendingTransactions.Count} tx(s) + reward");

        MineBlock(newBlock);

        Console.WriteLine($"Block successfully mined: {newBlock.Hash}. Appending and applying balances.");

        // Append to chain then apply balances for included txs.
        Chain.Add(newBlock);
        ApplyTransactionsToWallets(blockTransactions);
        
        Console.WriteLine($"[MINER] Reward paid to {minerAddress}.");


        // Clear mempool for the next block.
      _pendingTransactions.Clear();
    }

    /// <summary>Proof-of-Work: find a hash with N leading zeros.</summary>
    private void MineBlock(Block block)
    {
        var prefix = new string('0', _difficulty);
        while (!block.Hash.StartsWith(prefix, StringComparison.Ordinal))
        {
            block.Nonce++;
            block.Hash = block.CalculateHash();
        }
        Console.WriteLine($"Block mined with difficulty {_difficulty}: {block.Hash}");
    }

    /// <summary>Validate and enqueue a transaction for inclusion in the next block.</summary>
    public bool AddTransaction(Transaction transaction)
    {
        if (string.IsNullOrWhiteSpace(transaction.From) || string.IsNullOrWhiteSpace(transaction.To))
        {
            Console.WriteLine("Transaction invalid: From/To must be non-empty.");
            return false;
        }

        if (transaction.Amount <= 0m)
        {
            Console.WriteLine("Transaction invalid: Amount must be > 0.");
            return false;
        }

        var senderBalance = _walletService.GetBalance(transaction.From);
        var pendingOut = _pendingTransactions
            .Where(t => t.From == transaction.From)
            .Sum(t => t.Amount);
        
        if ((senderBalance - pendingOut) < transaction.Amount)
        {
            Console.WriteLine($"Transaction invalid: Insufficient balance. Sender has {senderBalance}, needs {transaction.Amount}.");
            return false;
        }

        Console.WriteLine($"[TX] Pending: {transaction.From} → {transaction.To} : {transaction.Amount}");


        _pendingTransactions.Add(transaction);
        return true;
    }

    // ---- Validation ----

    /// <summary>Check the whole chain, including PoW difficulty.</summary>
    public bool IsChainValid()
    {
        // Validate genesis content too.
        if (Chain[0].Hash != Chain[0].CalculateHash()) return false;

        for (int i = 1; i < Chain.Count; i++)
        {
            var curr = Chain[i];
            var prev = Chain[i - 1];

            if (curr.Hash != curr.CalculateHash()) return false;
            if (curr.PreviousHash != prev.Hash) return false;
            if (!MeetsDifficulty(curr.Hash)) return false;
        }
        return true;
    }

    /// <summary>Validate a candidate chain (used during conflict resolution).</summary>
    public bool IsChainValid(List<Block> chainToValidate)
    {
        // Basic "same genesis" check (simple and effective for this simulator).
        if (JsonSerializer.Serialize(chainToValidate[0]) != JsonSerializer.Serialize(Chain[0]))
            return false;

        for (int i = 1; i < chainToValidate.Count; i++)
        {
            var curr = chainToValidate[i];
            var prev = chainToValidate[i - 1];

            if (curr.Hash != curr.CalculateHash()) return false;
            if (curr.PreviousHash != prev.Hash) return false;
            if (!curr.Hash.StartsWith(new string('0', _difficulty), StringComparison.Ordinal)) return false;
        }
        return true;
    }

    /// <summary>Adopt a longer valid chain and rebuild wallet balances.</summary>
    public bool ReplaceChain(List<Block> newChain)
    {
        if (newChain.Count > Chain.Count && IsChainValid(newChain))
        {
            Console.WriteLine("Received a longer valid chain. Replacing current chain.");
            Chain = newChain;
            RebuildWalletsFromChain();
            return true;
        }
        return false;
    }

    private bool MeetsDifficulty(string hash)
        => !string.IsNullOrEmpty(hash) && hash.StartsWith(new string('0', _difficulty), StringComparison.Ordinal);

    // ---- Balance Application & Replay ----

    /// <summary>Apply a list of transactions to wallet balances (system doesn't spend).</summary>
    private void ApplyTransactionsToWallets(IEnumerable<Transaction> txs)
    {
        foreach (var tx in txs)
        {
            if (tx.From != "system")
            {
                var fromBal = _walletService.GetBalance(tx.From);
                _walletService.SetBalance(tx.From, fromBal - tx.Amount);
            }

            var toBal = _walletService.GetBalance(tx.To);
            _walletService.SetBalance(tx.To, toBal + tx.Amount);
        }
    }

    /// <summary>Recompute all balances by replaying the entire chain from genesis.</summary>
    public void RebuildWalletsFromChain()
    {
        // Build up a fresh map
        var map = new Dictionary<string, Wallet>(StringComparer.Ordinal);

        foreach (var block in Chain)
        {
            foreach (var tx in block.Transactions)
            {
                if (tx.From != "system")
                {
                    if (!map.TryGetValue(tx.From, out var wf))
                        wf = new Wallet(tx.From, 0m);
                    wf.Balance -= tx.Amount;
                    map[tx.From] = wf;
                }

                if (!map.TryGetValue(tx.To, out var wt))
                    wt = new Wallet(tx.To, 0m);
                wt.Balance += tx.Amount;
                map[tx.To] = wt;
            }
        }

        // Atomically replace wallet state with the computed snapshot
        _walletService.LoadWallets(map);
    }

    /// <summary>
    /// Validate and append a peer-announced block that extends our tip; apply balances if accepted.
    /// </summary>
    public bool TryAcceptExternalBlock(Block block)
    {
        var tip = GetLatestBlock();
        if (block.PreviousHash != tip.Hash) return false;
        if (block.Hash != block.CalculateHash()) return false;
        if (!MeetsDifficulty(block.Hash)) return false;

        Chain.Add(block);
        ApplyTransactionsToWallets(block.Transactions);

        // Remove any included txs from our pending pool
        _pendingTransactions.RemoveAll(t => block.Transactions.Contains(t));

        Console.WriteLine($"[P2P] Accepted external block #{block.Index} ({block.Hash[..8]}...).");

        return true;
    }
}
