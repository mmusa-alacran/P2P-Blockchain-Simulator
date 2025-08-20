using System.Collections.Generic;
using CsharpBlockchainNode.Models;
using System.Text.Json; 
using CsharpBlockchainNode.Services;

namespace CsharpBlockchainNode.Core
{
   /// Manages the blockchain itself, including the chain of blocks, pending transactions,
   /// and the core logic for mining and validation.
   public class Blockchain
   {
      private readonly List<Transaction> _pendingTransactions = new();
      private readonly int _difficulty;
      private readonly WalletService _walletService;

      /// The list of all blocks in the chain. This is the ledger itself.
      public List<Block> Chain { get; private set; }

      // This constructor is used to initialize the blockchain from a saved state.
      public Blockchain(int difficulty, WalletService walletService, List<Block>? initialChain)
      {
         _difficulty = difficulty;
         _walletService = walletService;

         if (initialChain != null && initialChain.Any())
         {
            Chain = initialChain;
         }
         else
         { 
            // If no chain is provided, create the genesis block.
            Chain = new List<Block> { CreateGenesisBlock() };
         }
         
      }


      /// Creates the very first block in the chain, known as the Genesis Block.
      /// returns The genesis block.
      private Block CreateGenesisBlock()
      {
         var transactions = new List<Transaction> { new("system", "genesis", 0) };

         // We hardcode the timestamp to ensure the genesis block hash is always the same.
         // This timestamp is arbitrary (it's Jan 1, 2023).
         const long genesisTimestamp = 1672531200;
         var genesisBlock = new Block(0, genesisTimestamp, transactions, "0");

         // In a real blockchain, the genesis block's hash might also be hardcoded
         // after being calculated once, but calculating it here is fine for our purposes.
         return genesisBlock;
      }

      /// Gets the most recent block in the chain.
      public Block GetLatestBlock()
      {
         return Chain.Last();
      }

      /// Mines a new block, including all pending transactions.
      /// The miner who successfully mines the block receives a reward.
      /// The address of the miner who will receive the mining reward.
      public void MinePendingTransactions(string minerAddress)
      {
         // Create the single mining reward transaction for this block.
         const double rewardAmount = 1; // 1 coin reward
         var rewardTransaction = new Transaction("system", minerAddress, rewardAmount);

         // Create a new list containing all pending transactions plus the new reward transaction.
         // This ensures the reward is part of this block only.
         var blockTransactions = new List<Transaction>(_pendingTransactions)
         {
            rewardTransaction
         };

         // Create the new block with the complete transaction list.
         var newBlock = new Block(
            index: GetLatestBlock().Index + 1,
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            transactions: blockTransactions,
            previousHash: GetLatestBlock().Hash
         );

         // Mine the block (Proof of Work).
         MineBlock(newBlock);

         Console.WriteLine("Block successfully mined. Adding to chain and updating balances...");
         
         // Add the successfully mined block to the chain.
         Chain.Add(newBlock);

         // AFTER the block is confirmed on the chain, update all wallet balances.
         foreach (var tx in blockTransactions)
         {
            if (tx.From != "system") // Don't subtract from the "system" wallet
            {
                  var fromBalance = _walletService.GetBalance(tx.From);
                  _walletService.SetBalance(tx.From, fromBalance - tx.Amount);
            }
            
            var toBalance = _walletService.GetBalance(tx.To);
            _walletService.SetBalance(tx.To, toBalance + tx.Amount);
         }
         
         // Clear the original pending transactions pool for the next block.
         _pendingTransactions.Clear();
      }

      /// Implements the Proof of Work algorithm.
      /// It repeatedly calculates the hash of the block until it finds a hash
      /// with the required number of leading zeros (_difficulty).
      private void MineBlock(Block block)
      {
         var hashPrefix = new string('0', _difficulty);
         while (block.Hash[.._difficulty] != hashPrefix)
         {
            block.Nonce++;
            block.Hash = block.CalculateHash();
         }

         Console.WriteLine($"Block mined: {block.Hash}");
      }


      /// Adds a new transaction, but only if it's valid.
      /// param: "transaction", the transaction to validate and add.
      /// returns True if the transaction is valid, false otherwise.
      public bool AddTransaction(Transaction transaction)
      {
         if (string.IsNullOrEmpty(transaction.From) || string.IsNullOrEmpty(transaction.To))
         {
            Console.WriteLine("Transaction validation failed: From/To address cannot be empty.");
            return false;
         }

         if (transaction.Amount <= 0)
         {
            Console.WriteLine("Transaction validation failed: Amount must be greater than zero.");
            return false;
         }

         var senderBalance = _walletService.GetBalance(transaction.From);
         if (senderBalance < transaction.Amount)
         {
            Console.WriteLine($"Transaction validation failed: Insufficient balance. Sender has {senderBalance}, needs {transaction.Amount}.");
            return false;
         }

         _pendingTransactions.Add(transaction);
         return true;
      }

      /// Validates the integrity of the entire blockchain.
      /// True if the chain is valid, false otherwise.
      public bool IsChainValid()
      {
         for (int i = 1; i < Chain.Count; i++)
         {
            var currentBlock = Chain[i];
            var previousBlock = Chain[i - 1];

            // Check if the current block's hash is still valid
            if (currentBlock.Hash != currentBlock.CalculateHash())
            {
               return false;
            }

            // Check if the current block points to the correct previous block
            if (currentBlock.PreviousHash != previousBlock.Hash)
            {
               return false;
            }
         }

         // Optional: Check if the genesis block is unaltered
         // (not strictly necessary if the loop starts at 1)
         if (Chain[0].Hash != Chain[0].CalculateHash())
         {
            return false;
         }

         return true;
      }
      
      
      /// Validates the integrity of a given blockchain.
      /// This is an overload IsChainValid method used by the consensus algorithm.
      /// param: chainToValidate - The chain to validate.
      /// returns True if the chain is valid, false otherwise.
      public bool IsChainValid(List<Block> chainToValidate)
      {
         // Check if the genesis block is the same
         // For simplicity, we can serialize them to JSON and compare strings.
         if (JsonSerializer.Serialize(chainToValidate[0]) != JsonSerializer.Serialize(Chain[0]))
         {
            return false;
         }

         for (int i = 1; i < chainToValidate.Count; i++)
         {
            var currentBlock = chainToValidate[i];
            var previousBlock = chainToValidate[i - 1];

            if (currentBlock.Hash != currentBlock.CalculateHash())
            {
               return false;
            }
            if (currentBlock.PreviousHash != previousBlock.Hash)
            {
               return false;
            }
         }
         return true;
      }
      

      /// Replaces the current node's chain with a new one, but only if the new chain
      /// is longer and valid. This is the core of the consensus algorithm.
      /// param: newChain - The candidate chain to replace the current one.
      /// returns True if the chain was replaced, false otherwise.
      public bool ReplaceChain(List<Block> newChain)
      {
         // A new chain is only valid if it's longer than the current one
         // and if the chain itself is valid.
         if (newChain.Count > Chain.Count && IsChainValid(newChain)) // We'll create an overload for IsChainValid
         {
            Console.WriteLine("Received a longer valid chain. Replacing the current chain.");
            Chain = newChain;
            return true;
         }

         return false;
      }
   }
}