namespace GapLogger.Models;

public sealed record OrderEvent(
    string Side,
    string Kind,
    ulong Ticket,
    DateTime UtcAt,
    string TradeType,     // "BUY" | "SELL"
    ulong ServerTimeMsc,  // MT5 server time in ms
    double? Profit);      // null for Open, value for Close
// Side: "A" | "B"     Kind: "Open" | "Close"