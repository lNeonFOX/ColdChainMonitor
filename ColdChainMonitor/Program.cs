using ColdChainMonitor;

var inputPath = args.Length > 0 ? args[0] : "readings.txt";
var archivePath = args.Length > 1 ? args[1] : "archive.json";

ImportResult import;
using (var input = File.OpenRead(inputPath))
{
    import = ReadingImporter.ReadReadings(input);
}

var alerts = AlertAnalyzer.BuildAlerts(import.Readings);

var archive = new MonitoringArchive(DateTimeOffset.UtcNow, import.Readings, import.Errors, alerts);

using (var output = File.Create(archivePath))
{
    ArchiveSerializer.WriteArchive(output, archive);
}

MonitoringArchive fromFile;
using (var fileInput = File.OpenRead(archivePath))
{
    fromFile = ArchiveSerializer.ReadArchive(fileInput);
}

using var memory = new MemoryStream();
ArchiveSerializer.WriteArchive(memory, archive);
memory.Position = 0;
var fromMemory = ArchiveSerializer.ReadArchive(memory);

Console.WriteLine($"Valid readings: {archive.Readings.Length}");
Console.WriteLine($"Import errors:  {archive.Errors.Length}");
foreach (var e in archive.Errors)
    Console.WriteLine($"  line {e.LineNumber}: {e.Message}");

Console.WriteLine($"Alerts:         {archive.Alerts.Length}");
foreach (var a in archive.Alerts)
{
    var reasons = new List<string>();
    if (a.OutsideRange) reasons.Add("Outside range");
    if (a.AbruptChange) reasons.Add("abrupt change");
    Console.WriteLine($"  {a.SensorId} {a.Timestamp:HH:mm} {a.Temperature} {string.Join("; ", reasons)}");
}

Console.WriteLine($"File round trip OK:   {ArchiveSerializer.AreEqual(archive, fromFile)}");
Console.WriteLine($"Memory round trip OK: {ArchiveSerializer.AreEqual(archive, fromMemory)}");