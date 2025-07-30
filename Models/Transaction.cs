namespace CsharpBlockchainNode.Models
{
/// Represents a transaction using a record type,
/// which provides immutable properties and value-based equality.
public record Transaction(string From, string To, double Amount);
}