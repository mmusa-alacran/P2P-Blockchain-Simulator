using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Xunit;
using CsharpBlockchainNode.Core;
using CsharpBlockchainNode.Services;

namespace CsharpBlockchainNode.Tests
{
    public class GenesisDeterminismTests
    {
        private static IConfiguration MakeConfig() =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Blockchain:Difficulty"] = "1",
                    ["Genesis:Allocations:0:to"] = "Alice",
                    ["Genesis:Allocations:0:amount"] = "100",
                    ["Genesis:Allocations:1:to"] = "Bob",
                    ["Genesis:Allocations:1:amount"] = "50",
                })
                .Build();

        [Fact]
        public void Same_Config_Yields_Stable_Genesis_Hash()
        {
            var cfg = MakeConfig();
            var bc1 = new Blockchain(1, new WalletService(), null, cfg);
            var bc2 = new Blockchain(1, new WalletService(), null, cfg);

            Assert.Equal(bc1.Chain[0].Hash, bc2.Chain[0].Hash);
        }
    }
}
