using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Xunit;
using CsharpBlockchainNode.Services;

namespace CsharpBlockchainNode.Tests
{
    public class PeerManagementTests
    {
        private static IConfiguration MakeConfig(params string[] peers)
        {
            var dict = new Dictionary<string, string?>();
            for (int i = 0; i < peers.Length; i++)
                dict[$"PeerNodes:{i}"] = peers[i];

            return new ConfigurationBuilder()
                .AddInMemoryCollection(dict)
                .Build();
        }

        [Fact]
        public void Peers_Combine_Config_And_Dynamic_Deduplicate()
        {
            var cfg = MakeConfig("http://a", "http://b/");
            var svc = new NodeService(cfg);

            Assert.True(svc.RegisterPeer("http://b")); // dup via trailing slash normalization
            Assert.True(svc.RegisterPeer("http://c"));
            Assert.False(svc.RegisterPeer("not a url")); // rejected

            var peers = svc.GetPeers().OrderBy(x => x).ToArray();
            Assert.Contains("http://a", peers);
            Assert.Contains("http://b", peers);
            Assert.Contains("http://c", peers);
            Assert.Equal(peers.Length, peers.Distinct().Count()); // unique
        }

        [Fact]
        public void Unregister_Removes_Only_Dynamic_Peers()
        {
            var cfg = MakeConfig("http://cfg");
            var svc = new NodeService(cfg);
            svc.RegisterPeer("http://dyn");

            Assert.True(svc.UnregisterPeer("http://dyn"));
            Assert.False(svc.UnregisterPeer("http://dyn")); // already gone

            // config peers can’t be removed
            Assert.False(svc.UnregisterPeer("http://cfg"));
            Assert.Contains("http://cfg", svc.GetPeers());
        }
    }
}
