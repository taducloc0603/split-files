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

public sealed record HistoryRecord(
    ulong Ticket,
    string Symbol,
    int TradeType,
    double Volume,
    double OpenPrice,
    double ClosePrice,
    double Sl,
    double Tp,
    double Commission,
    double Profit,
    ulong OpenTimeMsc,
    ulong CloseTimeMsc,
    ulong CloseEaTimeLocal);