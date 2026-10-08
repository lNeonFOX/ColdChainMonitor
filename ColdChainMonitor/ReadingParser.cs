using System.Globalization;
namespace ColdChainMonitor;

public static class ReadingParser
{
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    public static bool TryParseReading(string line, out Reading? reading, out string error)
    {
        reading = null;
        error = string.Empty;

        var fields = line.Split('|');
        if (fields.Length != 4)
        {
            error = "Expected exactly 4 fields separated by '|'.";
            return false;
        }

        var sensorId = fields[0].Trim();
        var timestampText = fields[1].Trim();
        var classText = fields[2].Trim();
        var temperatureText = fields[3].Trim();

        if (sensorId.Length == 0)
        {
            error = "SensorId must not be empty.";
            return false;
        }

        if (!DateTimeOffset.TryParseExact(
                timestampText,
                TimestampFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var timestamp))
        {
            error = $"Invalid timestamp '{timestampText}'.";
            return false;
        }

        if (!Enum.TryParse<StorageClass>(classText, ignoreCase: true, out var storageClass)
            || !Enum.IsDefined(storageClass)
            || !Enum.GetNames<StorageClass>().Contains(classText, StringComparer.OrdinalIgnoreCase))
        {
            error = $"Invalid storage class '{classText}'.";
            return false;
        }

        if (!decimal.TryParse(
                temperatureText,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var temperature))
        {
            error = $"Invalid temperature '{temperatureText}'.";
            return false;
        }

        reading = new Reading(sensorId, timestamp, storageClass, temperature);
        return true;
    }
}