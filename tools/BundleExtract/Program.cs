using System.IO.Compression;
using System.Text;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: BundleExtract <bundle> <output-directory>");
    return 2;
}

using var source = File.OpenRead(args[0]);
using var reader = new BinaryReader(source, Encoding.ASCII, leaveOpen: true);
if (Encoding.ASCII.GetString(reader.ReadBytes(8)) != "POTATO70") throw new InvalidDataException("Bundle signature mismatch.");
var fileSize = reader.ReadUInt32();
reader.ReadUInt32(); // Packed data size.
var metadataSize = reader.ReadUInt32();
if (fileSize != source.Length || metadataSize % 304 != 0) throw new InvalidDataException("Unsupported bundle layout.");
source.Position = 32;

var entries = new List<Entry>();
for (var i = 0; i < metadataSize / 304; i++)
{
    var name = Encoding.ASCII.GetString(reader.ReadBytes(256)).TrimEnd('\0');
    reader.ReadBytes(16); // Resource hash.
    var offset = reader.ReadUInt32();
    reader.ReadUInt32(); // Reserved.
    var unpackedSize = reader.ReadUInt32();
    var packedSize = reader.ReadUInt32();
    var crc = reader.ReadUInt32();
    var method = reader.ReadUInt32();
    reader.ReadUInt32(); // Date.
    reader.ReadUInt32(); // Time.
    if (name.Length == 0 || Path.IsPathRooted(name) || name.Split('\\', '/').Contains("..") || name.Contains(':')
        || offset + (ulong)packedSize > (ulong)source.Length || method > 1)
        throw new InvalidDataException($"Unsupported bundle entry: {name}");
    entries.Add(new Entry(name, offset, packedSize, unpackedSize, crc, method));
}

var destination = Path.GetFullPath(args[1]);
var extracted = 0;
foreach (var entry in entries)
{
    source.Position = entry.Offset;
    var packed = reader.ReadBytes(checked((int)entry.PackedSize));
    byte[] contents;
    if (entry.Method == 0) contents = packed;
    else
    {
        using var zipped = new MemoryStream(packed);
        using var inflater = new ZLibStream(zipped, CompressionMode.Decompress);
        using var unzipped = new MemoryStream();
        inflater.CopyTo(unzipped);
        contents = unzipped.ToArray();
    }
    if (contents.Length != entry.UnpackedSize || Crc32(contents) != entry.Crc)
        throw new InvalidDataException($"Size or CRC mismatch: {entry.Name}");
    var target = Path.GetFullPath(Path.Combine(destination, entry.Name));
    if (!target.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException($"Output path escapes directory: {entry.Name}");
    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    File.WriteAllBytes(target, contents);
    extracted++;
}
Console.WriteLine($"Extracted {extracted} verified entries to {destination}");
return 0;

static uint Crc32(byte[] bytes)
{
    uint crc = 0xFFFFFFFF;
    foreach (var value in bytes)
    {
        crc ^= value;
        for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0U : 0xEDB88320U);
    }
    return ~crc;
}

internal sealed record Entry(string Name, uint Offset, uint PackedSize, uint UnpackedSize, uint Crc, uint Method);
