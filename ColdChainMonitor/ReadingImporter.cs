using System.Collections.Immutable;
using System.Text;
namespace ColdChainMonitor;

public static class ReadingImporter
{
    public static ImportResult ReadReadings(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var readings = new List<Reading>();
        var errors = new List<ImportError>();
        var classBySensor = new Dictionary<string, StorageClass>(StringComparer.OrdinalIgnoreCase);

        using var reader = new StreamReader(
            input,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024,
            leaveOpen: true);

        var lineNumber = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!ReadingParser.TryParseReading(line, out var reading, out var error))
            {
                errors.Add(new ImportError(lineNumber, line, error));
                continue;
            }

            if (classBySensor.TryGetValue(reading!.SensorId, out var knownClass))
            {
                if (knownClass != reading.StorageClass)
                {
                    errors.Add(new ImportError(
                        lineNumber, line,
                        $"StorageClass mismatch: sensor was {knownClass}, line says {reading.StorageClass}."));
                    continue;
                }
            }
            else
            {
                classBySensor.Add(reading.SensorId, reading.StorageClass);
            }

            readings.Add(reading);
        }

        return new ImportResult(readings.ToImmutableArray(), errors.ToImmutableArray());
    }
}