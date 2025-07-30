using System.Collections.Generic;
using CsharpBlockchainNode.Models;

namespace CsharpBlockchainNode.Core
{
   /// Manages the blockchain itself, including the chain of blocks, pending transactions,
   /// and the core logic for mining and validation.
   public class Blockchain
   {
      private readonly List<Transaction> _pendingTransactions = new();
      private readonly int _difficulty;

      /// The list of all blocks in the chain. This is the ledger itself.
      public List<Block> Chain { get; private set; }

      /// Initializes a new blockchain, setting the difficulty and creating the genesis block.
      /// The number of leading zeros required for a valid hash.
      public Blockchain(int difficulty)
      {
         _difficulty = difficulty;
         Chain = new List<Block> { CreateGenesisBlock() };
      }


      /// Creates the very first block in the chain, known as the Genesis Block.
      /// returns The genesis block.
      private Block CreateGenesisBlock()
      {
         var transactions = new List<Transaction> { new("system", "genesis", 0) };
         return new Block(0, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), transactions, "0");
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
         // Add the mining reward transaction. The miner gets rewarded for their work.
         var rewardTransaction = new Transaction("system", minerAddress, 1); // coin reward
         _pendingTransactions.Add(rewardTransaction);

         var newBlock = new Block(
             index: GetLatestBlock().Index + 1,
             timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
             // Add all pending transactions
             transactions: new List<Transaction>(_pendingTransactions),
             previousHash: GetLatestBlock().Hash
         );

         MineBlock(newBlock);

         Chain.Add(newBlock);

         // Clear the pending transactions list as they are now included in the chain.
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


      /// Adds a new transaction to the list of pending transactions.
      /// These transactions will be included in the next mined block.
      public void AddTransaction(Transaction transaction)
      {
         // Here you would typically add validation for the transaction,
         // e.g., checking if the sender has enough balance.
         // For this project, we'll keep it simple.
         _pendingTransactions.Add(transaction);
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
   }
}