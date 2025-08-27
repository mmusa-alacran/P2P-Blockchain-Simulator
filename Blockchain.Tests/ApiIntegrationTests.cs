using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        // Give each test run a unique "urls" so the persistence filename is unique.
        // (PersistenceService names files like blockchain_state_<port>.json)
        var randPort = Random.Shared.Next(20000, 65000);

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Blockchain:Difficulty", "1");               // fast PoW
            builder.UseSetting("urls", $"http://localhost:{randPort}");     // unique file
            builder.UseSetting("NodeUrl", $"http://localhost:{randPort}");  // used by /info & broadcast
            builder.UseSetting("PeerNodes:0", $"http://localhost:{randPort}"); // quiets broadcasts
        });
    }

    [Fact]
    public async Task Healthz_and_Info_return_ok_and_json()
    {
        var client = _factory.CreateClient();

        var health = await client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        var info = await client.GetFromJsonAsync<JsonElement>("/info");
        Assert.True(info.TryGetProperty("height", out _));
        Assert.True(info.TryGetProperty("difficulty", out _));
        Assert.True(info.TryGetProperty("tipHash", out _));
    }

    [Fact]
    public async Task PostingTx_and_Mining_updates_balances()
    {
        var client = _factory.CreateClient();

        // Alice -> Bob : 2
        var txResp = await client.PostAsJsonAsync("/transactions/new",
            new { from = "Alice", to = "Bob", amount = 2 });
        txResp.EnsureSuccessStatusCode();

        // Mine a block containing pending tx + reward
        var mine = await client.PostAsync("/mine", content: null);
        mine.EnsureSuccessStatusCode();

        // Check balances reflect tx + coinbase
        var alice = await client.GetFromJsonAsync<JsonElement>("/wallets/Alice/balance");
        var bob   = await client.GetFromJsonAsync<JsonElement>("/wallets/Bob/balance");
        var miner = await client.GetFromJsonAsync<JsonElement>("/wallets/my-miner-address/balance");

        Assert.Equal(98, alice.GetProperty("balance").GetDecimal());
        Assert.Equal(52, bob.GetProperty("balance").GetDecimal());
        Assert.Equal(1,  miner.GetProperty("balance").GetDecimal());
    }
}
