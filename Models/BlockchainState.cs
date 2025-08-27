using System.Collections.Generic;

namespace CsharpBlockchainNode.Models;

/// <summary>
/// Serializable persistence payload: the full chain plus a wallet snapshot.
/// </summary>
public record BlockchainState(List<Block> Chain, Dictionary<string, Wallet> Wallets);
