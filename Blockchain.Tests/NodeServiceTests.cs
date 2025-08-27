using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Xunit;

public class NodeServiceTests
{
    private static IConfiguration MakeConfig(params string[] peers)
    {
        var dict = new Dictionary<string,string?>
        {
            ["PeerNodes:0"] = peers.Length > 0 ? peers[0] : null,
            ["PeerNodes:1"] = peers.Length > 1 ? peers[1] : null,
            ["PeerNodes:2"] = peers.Length > 2 ? peers[2] : null
        };
        return new ConfigurationBuilder().AddInMemoryCollection(dict!).Build();
    }

    [Fact]
    public void Peers_Combine_Config_And_Dynamic()
    {
        var cfg = MakeConfig("http://a", "http://b");
        var svc = new CsharpBlockchainNode.Services.NodeService(cfg);

        Assert.Contains("http://a", svc.Peers);
        Assert.Contains("http://b", svc.Peers);

        Assert.True(svc.RegisterPeer("http://dyn"));
        Assert.Contains("http://dyn", svc.Peers);

        // duplicates are distinct-filtered
        Assert.False(svc.RegisterPeer("not-a-uri")); // invalid
    }

    [Fact]
    public void Unregister_Removes_Only_Dynamic()
    {
        var cfg = MakeConfig("http://cfg");
        var svc = new CsharpBlockchainNode.Services.NodeService(cfg);

        svc.RegisterPeer("http://dyn1");
        svc.RegisterPeer("http://dyn2");

        Assert.True(svc.UnregisterPeer("http://dyn1"));
        Assert.DoesNotContain("http://dyn1", svc.Peers);

        // config peers are not removable (method only touches dynamic set)
        Assert.False(svc.UnregisterPeer("http://cfg"));
        Assert.Contains("http://cfg", svc.Peers);
    }
}
