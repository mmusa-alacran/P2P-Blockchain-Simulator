using Xunit;
using CsharpBlockchainNode.Services;

namespace CsharpBlockchainNode.Tests
{
    public class WalletServiceSnapshotTests
    {
        [Fact]
        public void Snapshot_Is_Deep_Copy()
        {
            var ws = new WalletService();
            ws.SetBalance("A", 10m);

            var snap = ws.Snapshot();
            snap["A"].Balance = 999m; // mutate copy

            Assert.Equal(10m, ws.GetBalance("A")); // original unaffected
        }
    }
}
