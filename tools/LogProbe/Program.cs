using System.Text.Json;
using Witcher3Modifier;

string log = ErrorLog.Path;
string archive = System.IO.Path.Combine(AppContext.BaseDirectory, "Archives", "RuntimeLogs");
byte[]? prior = File.Exists(log) ? File.ReadAllBytes(log) : null;
HashSet<string> existing = Directory.Exists(archive) ? Directory.GetFiles(archive).ToHashSet() : [];
try
{
    byte[] history = System.Text.Encoding.UTF8.GetBytes(new string('x', 1024 * 1024) + "\nlast historical event\n");
    File.WriteAllBytes(log, history);
    if (!ErrorLog.Write("rollover-test", null, new { Value = 42 }))
        throw new Exception("Write failed");
    string[] created = Directory.GetFiles(archive).Where(path => !existing.Contains(path)).ToArray();
    if (created.Length != 1 || !File.ReadAllBytes(created[0]).SequenceEqual(history))
        throw new Exception("Historical bytes were lost or changed");
    using JsonDocument entry = JsonDocument.Parse(File.ReadAllText(log));
    if (entry.RootElement.GetProperty("Operation").GetString() != "rollover-test")
        throw new Exception("New event missing");
    if (!ErrorLog.Write("second-test", null) || File.ReadLines(log).Count() != 2)
        throw new Exception("Append failed");
    if (!File.ReadAllBytes(created[0]).SequenceEqual(history))
        throw new Exception("Later event changed archive");
    Console.WriteLine("Production ErrorLog passed: complete rollover preserved, new events append, previous archive unchanged.");
}
finally
{
    if (prior is null) File.Delete(log); else File.WriteAllBytes(log, prior);
    if (Directory.Exists(archive))
        foreach (string path in Directory.GetFiles(archive).Where(path => !existing.Contains(path)))
            File.Delete(path);
}
