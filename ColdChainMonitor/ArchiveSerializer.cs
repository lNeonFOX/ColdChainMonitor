using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColdChainMonitor;

public static class ArchiveSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void WriteArchive(Stream output, MonitoringArchive archive)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(archive);
        JsonSerializer.Serialize(output, archive, Options);
    }

    public static MonitoringArchive ReadArchive(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var archive = JsonSerializer.Deserialize<MonitoringArchive>(input, Options)
            ?? throw new InvalidDataException("Archive JSON must not be null.");

        if (archive.Readings.IsDefault || archive.Errors.IsDefault || archive.Alerts.IsDefault)
            throw new InvalidDataException("Archive JSON is missing required collections.");

        return archive;
    }

    public static bool AreEqual(MonitoringArchive a, MonitoringArchive b) =>
        a.CreatedAtUtc == b.CreatedAtUtc
        && a.Readings.SequenceEqual(b.Readings)
        && a.Errors.SequenceEqual(b.Errors)
        && a.Alerts.SequenceEqual(b.Alerts);
}