using System;
using System.Collections.Generic;
using Xunit;

using CsharpBlockchainNode.Core;
using CsharpBlockchainNode.Models;

namespace CsharpBlockchainNode.Tests
{
    public class ValidationTests
    {
        [Fact]
        public void ReplaceChain_Rebuilds_Balances()
        {
            const int difficulty = 1;
            var (bc, ws) = TestUtils.MakeBlockchain(difficulty);

            // Snapshot balances before we adopt the longer chain
            var alice0 = ws.GetBalance("Alice");
            var bob0   = ws.GetBalance("Bob");

            // Build a longer valid chain that includes a tx Alice->Bob (1)
            var prefix = new string('0', difficulty);
            var newChain = new List<Block> { bc.Chain[0] };

            // Block #1: reward -> MinerX
            var b1 = new Block(
                index: 1,
                timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                transactions: new List<Transaction> {
                    new("system", "MinerX", 1m) // reward
                },
                previousHash: newChain[^1].Hash
            );
            while (!b1.Hash.StartsWith(prefix, StringComparison.Ordinal)) { b1.Nonce++; b1.Hash = b1.CalculateHash(); }
            newChain.Add(b1);

            // Block #2: Alice -> Bob (1) and a reward -> MinerX
            var b2 = new Block(
                index: 2,
                timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                transactions: new List<Transaction> {
                    new("Alice", "Bob", 1m),
                    new("system", "MinerX", 1m) // reward
                },
                previousHash: newChain[^1].Hash
            );
            while (!b2.Hash.StartsWith(prefix, StringComparison.Ordinal)) { b2.Nonce++; b2.Hash = b2.CalculateHash(); }
            newChain.Add(b2);

            // Replace and assert
            var replaced = bc.ReplaceChain(newChain);
            Assert.True(replaced);

            Assert.Equal(alice0 - 1m, ws.GetBalance("Alice")); // Alice paid 1
            Assert.Equal(bob0 + 1m,   ws.GetBalance("Bob"));   // Bob received 1
            Assert.Equal(2m,          ws.GetBalance("MinerX")); // two rewards
        }
    }
}
