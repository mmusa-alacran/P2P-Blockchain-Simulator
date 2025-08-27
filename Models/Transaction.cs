namespace CsharpBlockchainNode.Models;

/// <summary>
/// Immutable money transfer between two addresses. Amount is decimal for precision.
/// </summary>
public readonly record struct Transaction(string From, string To, decimal Amount);
