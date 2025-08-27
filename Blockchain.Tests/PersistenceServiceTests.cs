using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Xunit;
using CsharpBlockchainNode.Services;
using CsharpBlockchainNode.Models;
using System;

namespace CsharpBlockchainNode.Tests
{
    public class PersistenceServiceTests
    {
        private static IConfiguration ConfigForPort(int port) =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["urls"] = $"http://localhost:{port}"
                })
                .Build();

        [Fact]
        public async Task Save_And_Load_Roundtrip_Works()
        {
            const int port = 8123;
            var path = $"blockchain_state_{port}.json";
            if (File.Exists(path)) File.Delete(path);

            var ps = new PersistenceService(ConfigForPort(port));

            var txs = new List<Transaction>();
            var genesis = new Block(index: 0, timestamp: 1672531200L, transactions: txs, previousHash: "0");
            var state = new BlockchainState(new List<Block> { genesis }, new Dictionary<string, Wallet>());

            try
            {
                await ps.SaveStateAsync(state);
                var loaded = await ps.LoadStateAsync();

                Assert.NotNull(loaded);
                Assert.Single(loaded!.Chain);
                Assert.Equal(state.Chain[0].Hash, loaded.Chain[0].Hash);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
