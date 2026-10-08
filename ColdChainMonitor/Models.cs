using System.Collections.Immutable;

namespace ColdChainMonitor;

public enum StorageClass { Cold, Frozen }

public sealed record Reading(
    string SensorId,
    DateTimeOffset Timestamp,
    StorageClass StorageClass,
    decimal Temperature);

public sealed record ImportError(int LineNumber, string RawLine, string Message);

public sealed record Alert(
    string SensorId,
    DateTimeOffset Timestamp,
    StorageClass StorageClass,
    decimal Temperature,
    bool OutsideRange,
    bool AbruptChange);

public sealed record ImportResult(
    ImmutableArray<Reading> Readings,
    ImmutableArray<ImportError> Errors);

public sealed record MonitoringArchive(
    DateTimeOffset CreatedAtUtc,
    ImmutableArray<Reading> Readings,
    ImmutableArray<ImportError> Errors,
    ImmutableArray<Alert> Alerts);