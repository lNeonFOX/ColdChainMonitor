# ColdChainMonitor

FP 3222, Assignment 3: Working with Files, Streams and Serialization

The app reads `readings.txt` through a Stream, parses and validates each line, finds out-of-range and abrupt
readings per sensor, writes `archive.json`, and reads it back to verify the values.

Run: `cd ColdChainMonitor` then `dotnet run`. Tests: `dotnet test`.

## Answers

1. **Stream ownership.** `Program.cs` creates and disposes every `FileStream` and `MemoryStream`. The helper
   methods only borrow a stream, so they leave it open: the caller may still need it, for example to rewind and
   read a `MemoryStream` after writing it. Any `StreamReader` I create uses `leaveOpen: true`.

2. **Lazy sequence and StreamReader.** A lazy sequence reads only when it is enumerated. If the method returns
   it, the `using` block has already disposed the reader, so later enumeration fails with
   `ObjectDisposedException`. `ReadReadings` reads everything into lists first and returns immutable arrays.

3. **MemoryStream.Position.** Writing leaves the position at the end of the stream. Deserialization starts at
   the current position, so it would find no data. I set `Position = 0` explicitly before reading.

4. **Grouping before pairing.** If all readings are sorted together, neighbours can belong to different sensors,
   and comparing their temperatures is meaningless. Grouping by `SensorId` first guarantees that each pair
   belongs to one sensor.

5. **Enum.TryParse and 99.** `Enum.TryParse` accepts any number, so `99` succeeds and gives an enum value that
   was never declared. I reject it with `Enum.IsDefined` and by checking that the text matches a declared name,
   which also rejects `0`, `1`, and `Cold,Frozen`.

6. **Pure vs I/O.** Pure: `TryParseReading`, `TemperatureRules`, `AlertAnalyzer`, and the archive comparison.
   I/O: `ReadReadings` (reads a stream), `WriteArchive` and `ReadArchive` (write and read a stream), and
   `Program.cs` (files and the clock).

7. **Values, not references.** Deserialization creates new objects, so references always differ. What matters
   is that the content is the same, so I compare `CreatedAtUtc` and the collection elements one by one.

8. **Deterministic order and injected CreatedAtUtc.** A fixed ordering gives the same alerts for the same input,
   so tests can assert exact results. Passing `CreatedAtUtc` in from `Program.cs` removes any dependence on
   the real clock, so tests can use a fixed time and compare whole archives.

## Tests

There are 14 automated tests in `ColdChainMonitor.Tests`.
