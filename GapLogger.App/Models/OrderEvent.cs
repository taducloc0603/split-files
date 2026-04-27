namespace GapLogger.Models;

public sealed record OrderEvent(string Side, string Kind, ulong Ticket, DateTime UtcAt);
// Side: "A" | "B"     Kind: "Open" | "Close"