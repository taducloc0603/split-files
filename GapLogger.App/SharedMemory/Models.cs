namespace GapLogger.SharedMemory;

public sealed record TickRecord(
    int Version,
    long TimestampMs,
    double Bid,
    double Ask,
    double Spread,
    long TickTimeMsc,
    string Symbol);

public sealed record TickSnapshot(
    TickRecord? A,
    TickRecord? B,
    string? ErrorA,
    string? ErrorB,
    DateTime UtcAt);

public sealed record TradeRecord(
    ulong Ticket,
    string Symbol,
    int TradeType,
    double Lot,
    double Price,
    double Sl,
    double Tp,
    double Profit,
    ulong TimeMsc,
    ulong OpenEaTimeLocal);