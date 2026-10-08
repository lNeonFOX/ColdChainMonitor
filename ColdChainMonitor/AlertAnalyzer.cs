using System.Collections.Immutable;
namespace ColdChainMonitor;

public static class AlertAnalyzer
{
    public static ImmutableArray<Alert> BuildAlerts(IEnumerable<Reading> readings)
    {
        ArgumentNullException.ThrowIfNull(readings);
        var all = readings.ToList();

        var canonicalId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in all)
            canonicalId.TryAdd(r.SensorId, r.SensorId);

        var alerts = new List<Alert>();

        foreach (var group in all.GroupBy(r => r.SensorId, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group.OrderBy(r => r.Timestamp).ToList();

            var firstAlert = Evaluate(ordered[0], previous: null, canonicalId);
            if (firstAlert is not null)
                alerts.Add(firstAlert);

            var pairs = ordered.Zip(
                ordered.Skip(1),
                (previous, current) => (previous, current));

            foreach (var (previous, current) in pairs)
            {
                var alert = Evaluate(current, previous, canonicalId);
                if (alert is not null)
                    alerts.Add(alert);
            }
        }

        return alerts
            .OrderBy(a => a.SensorId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Timestamp)
            .ToImmutableArray();
    }

    private static Alert? Evaluate(
        Reading current, Reading? previous, IReadOnlyDictionary<string, string> canonicalId)
    {
        var outside = TemperatureRules.IsOutsideRange(current.StorageClass, current.Temperature);
        var abrupt = previous is not null
            && TemperatureRules.IsAbrupt(previous.Temperature, current.Temperature);

        if (!outside && !abrupt)
            return null;

        return new Alert(
            canonicalId[current.SensorId],
            current.Timestamp,
            current.StorageClass,
            current.Temperature,
            outside,
            abrupt);
    }
}