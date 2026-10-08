using System.Text;
using System.Text.Json;

namespace ColdChainMonitor.Tests;

public class ColdChainTests
{
    private static readonly string[] SuppliedLines =
    {
        "S1|2026-09-22T08:10:00Z|Cold|5.2",
        "S2|2026-09-22T08:01:00Z|Frozen|-18.0",
        "S1|2026-09-22T08:00:00Z|Cold|4.0",
        "S1|2026-09-22T08:05:00Z|Cold|8.7",
        "S2|2026-09-22T08:06:00Z|Frozen|-12.0",
        "S1|2026-09-22T08:15:00Z|Cold|8.0",
        "S3|bad-date|Cold|3.5",
        "S2|2026-09-22T08:11:00Z|99|-17.0",
        "S1|2026-09-22T08:20:00Z|Cold|5,5",
        "S1|2026-09-22T08:25:00Z|Frozen|-17.0"
    };

    private static string Supplied => string.Join("\n", SuppliedLines);

    private static MemoryStream ToStream(string text) => new(Encoding.UTF8.GetBytes(text));

    private static ImportResult Import(string text)
    {
        using var stream = ToStream(text);
        return ReadingImporter.ReadReadings(stream);
    }

    private static MonitoringArchive BuildSuppliedArchive()
    {
        var import = Import(Supplied);
        var alerts = AlertAnalyzer.BuildAlerts(import.Readings);
        return new MonitoringArchive(
            new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero),
            import.Readings, import.Errors, alerts);
    }

    // 1
    [Fact]
    public void SuppliedFile_Produces6Readings_4Errors_2Alerts()
    {
        var import = Import(Supplied);
        var alerts = AlertAnalyzer.BuildAlerts(import.Readings);

        Assert.Equal(6, import.Readings.Length);
        Assert.Equal(4, import.Errors.Length);
        Assert.Equal(2, alerts.Length);
        Assert.Equal(new[] { 7, 8, 9, 10 }, import.Errors.Select(e => e.LineNumber).ToArray());
    }

    [Fact]
    public void SuppliedFile_AlertsAreInRequiredOrderWithBothReasons()
    {
        var import = Import(Supplied);
        var alerts = AlertAnalyzer.BuildAlerts(import.Readings);

        Assert.Equal("S1", alerts[0].SensorId);
        Assert.Equal(new DateTimeOffset(2026, 9, 22, 8, 5, 0, TimeSpan.Zero), alerts[0].Timestamp);
        Assert.Equal(8.7m, alerts[0].Temperature);
        Assert.True(alerts[0].OutsideRange);
        Assert.True(alerts[0].AbruptChange);

        Assert.Equal("S2", alerts[1].SensorId);
        Assert.Equal(new DateTimeOffset(2026, 9, 22, 8, 6, 0, TimeSpan.Zero), alerts[1].Timestamp);
        Assert.Equal(-12.0m, alerts[1].Temperature);
        Assert.True(alerts[1].OutsideRange);
        Assert.True(alerts[1].AbruptChange);
    }

    // 2
    [Theory]
    [InlineData("99")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("Cold,Frozen")]
    [InlineData("")]
    public void NumericOrInvalidStorageClass_IsRejected(string classText)
    {
        var ok = ReadingParser.TryParseReading(
            $"S1|2026-09-22T08:00:00Z|{classText}|4.0", out var reading, out var error);

        Assert.False(ok);
        Assert.Null(reading);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void EnumTryParse_AcceptsNinetyNine_ButParserStillRejectsIt()
    {
        Assert.True(Enum.TryParse<StorageClass>("99", out _));
        Assert.False(Enum.IsDefined((StorageClass)99));
        Assert.False(ReadingParser.TryParseReading(
            "S2|2026-09-22T08:11:00Z|99|-17.0", out _, out _));
    }

    // 3
    [Theory]
    [InlineData("5,5")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("abc")]
    [InlineData("")]
    public void InvalidTemperature_IsRejected(string temperatureText)
    {
        var ok = ReadingParser.TryParseReading(
            $"S1|2026-09-22T08:00:00Z|Cold|{temperatureText}", out _, out _);
        Assert.False(ok);
    }

    // 4
    [Fact]
    public void StorageClassChange_ForSameSensor_BecomesImportError()
    {
        var import = Import(
            "S1|2026-09-22T08:00:00Z|Cold|4.0\n" +
            "s1|2026-09-22T08:05:00Z|Frozen|-17.0");

        Assert.Single(import.Readings);
        var error = Assert.Single(import.Errors);
        Assert.Equal(2, error.LineNumber);
    }

    // 5
    [Theory]
    [InlineData("Cold", "8.0")]
    [InlineData("Cold", "2.0")]
    [InlineData("Frozen", "-15.0")]
    [InlineData("Frozen", "-22.0")]
    public void BoundaryValues_AreSafe(string storageClass, string temperature)
    {
        var import = Import($"S1|2026-09-22T08:00:00Z|{storageClass}|{temperature}");
        var alerts = AlertAnalyzer.BuildAlerts(import.Readings);

        Assert.Single(import.Readings);
        Assert.Empty(alerts);
    }

    // 6
    [Fact]
    public void AdjacentComparisons_NeverCrossSensorBoundaries()
    {
        var import = Import(
            "S1|2026-09-22T08:00:00Z|Cold|5.0\n" +
            "S2|2026-09-22T08:01:00Z|Frozen|-18.0\n" +
            "S1|2026-09-22T08:02:00Z|Cold|5.5\n" +
            "S2|2026-09-22T08:03:00Z|Frozen|-18.5");

        Assert.Empty(AlertAnalyzer.BuildAlerts(import.Readings));
    }

    // 7
    [Fact]
    public void InputOrder_MayDifferFromTimestampOrder_WithoutChangingAlerts()
    {
        var forward = SuppliedLines.Take(6).ToArray();
        var backward = forward.ToArray();
        Array.Reverse(backward);

        var alertsForward = AlertAnalyzer.BuildAlerts(Import(string.Join("\n", forward)).Readings);
        var alertsBackward = AlertAnalyzer.BuildAlerts(Import(string.Join("\n", backward)).Readings);

        Assert.Equal(alertsForward.ToArray(), alertsBackward.ToArray());
    }

    // 8
    [Fact]
    public void ReadReadings_LeavesCallerOwnedStreamOpen()
    {
        using var stream = ToStream(Supplied);
        ReadingImporter.ReadReadings(stream);
        Assert.True(stream.CanRead);
        _ = stream.Position;
    }

    // 9
    [Fact]
    public void WriteArchive_LeavesCallerOwnedStreamOpen()
    {
        using var stream = new MemoryStream();
        ArchiveSerializer.WriteArchive(stream, BuildSuppliedArchive());
        Assert.True(stream.CanWrite);
        Assert.True(stream.Length > 0);
    }

    // 10
    [Fact]
    public void MemoryStreamRoundTrip_PreservesValues_AndWritesEnumNames()
    {
        var archive = BuildSuppliedArchive();

        using var stream = new MemoryStream();
        ArchiveSerializer.WriteArchive(stream, archive);

        var json = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("\"storageClass\": \"Cold\"", json);
        Assert.Contains("\"storageClass\": \"Frozen\"", json);
        Assert.DoesNotMatch("\"storageClass\":\\s*\\d", json);

        stream.Position = 0;
        var restored = ArchiveSerializer.ReadArchive(stream);

        Assert.NotSame(archive, restored);
        Assert.Equal(archive.CreatedAtUtc, restored.CreatedAtUtc);
        Assert.Equal(archive.Readings.ToArray(), restored.Readings.ToArray());
        Assert.Equal(archive.Errors.ToArray(), restored.Errors.ToArray());
        Assert.Equal(archive.Alerts.ToArray(), restored.Alerts.ToArray());
        Assert.True(ArchiveSerializer.AreEqual(archive, restored));
    }

    // 11
    [Fact]
    public void StoredResults_CanBeEnumerated_AfterSourceStreamIsDisposed()
    {
        var stream = ToStream(Supplied);
        var import = ReadingImporter.ReadReadings(stream);
        stream.Dispose();

        Assert.Equal(6, import.Readings.Select(r => r.SensorId).ToList().Count);
        Assert.Equal(4, import.Errors.Select(e => e.Message).ToList().Count);
    }

    // 12
    [Fact]
    public void ReadArchive_LeavesCallerOwnedStreamOpen()
    {
        using var stream = new MemoryStream();
        ArchiveSerializer.WriteArchive(stream, BuildSuppliedArchive());
        stream.Position = 0;

        ArchiveSerializer.ReadArchive(stream);

        Assert.True(stream.CanRead);
    }

    // 13
    [Fact]
    public void ReadReadings_StartsFromCurrentPosition_WithoutResettingToZero()
    {
        var prefix = Encoding.UTF8.GetBytes("THIS IS NOT A READING\n");
        var body = Encoding.UTF8.GetBytes(Supplied);

        using var stream = new MemoryStream();
        stream.Write(prefix);
        stream.Write(body);
        stream.Position = prefix.Length;

        var import = ReadingImporter.ReadReadings(stream);

        Assert.Equal(6, import.Readings.Length);
        Assert.Equal(4, import.Errors.Length);
        Assert.Equal(7, import.Errors[0].LineNumber);
        Assert.True(stream.Position >= prefix.Length);
    }

    // 14 (additional): blank lines keep original line numbers
    [Fact]
    public void BlankLines_AreIgnored_ButStillCountedForLineNumbers()
    {
        var import = Import("\n   \nS1|bad|Cold|4.0\n");

        Assert.Empty(import.Readings);
        var error = Assert.Single(import.Errors);
        Assert.Equal(3, error.LineNumber);
        Assert.Equal("S1|bad|Cold|4.0", error.RawLine);
    }
}
