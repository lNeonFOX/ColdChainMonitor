namespace ColdChainMonitor;

public static class TemperatureRules
{
    public const decimal AbruptThreshold = 4.0m;

    public static (decimal Min, decimal Max) SafeRange(StorageClass storageClass) => storageClass switch
    {
        StorageClass.Cold => (2.0m, 8.0m),
        StorageClass.Frozen => (-22.0m, -15.0m),
        _ => throw new ArgumentOutOfRangeException(nameof(storageClass))
    };
    
    public static bool IsOutsideRange(StorageClass storageClass, decimal temperature)
    {
        var (min, max) = SafeRange(storageClass);
        return temperature < min || temperature > max;
    }

    public static bool IsAbrupt(decimal previous, decimal current)
        => Math.Abs(current - previous) > AbruptThreshold;
}