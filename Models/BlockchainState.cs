using System.Collections.Generic;
using CsharpBlockchainNode.Models;

public record BlockchainState(List<Block> Chain, Dictionary<string, Wallet> Wallets);