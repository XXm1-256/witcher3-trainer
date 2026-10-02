using System.Text.Encodings.Web;
using System.Text.Json;

namespace Witcher3Modifier;

internal static class ErrorLog
{
    internal static readonly string Path = System.IO.Path.Combine(AppContext.BaseDirectory, "modifier.log.jsonl");
    private static readonly object Gate = new();
    internal static bool Write(string operation, Exception? error, object? context = null)
    {
        try
        {
            string line = JsonSerializer.Serialize(new
            {
                Time = DateTimeOffset.Now,
                Build = typeof(ErrorLog).Assembly.ManifestModule.ModuleVersionId,
                Operation = operation,
                Error = error?.ToString(),
                Context = context
            }, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            lock (Gate)
            {
                if (File.Exists(Path) && new FileInfo(Path).Length >= 1024 * 1024)
                {
                    string archive = System.IO.Path.Combine(AppContext.BaseDirectory, "Archives", "RuntimeLogs");
                    Directory.CreateDirectory(archive);
                    File.Move(Path, System.IO.Path.Combine(archive, $"modifier-{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}.jsonl"));
                }
                File.AppendAllText(Path, line + Environment.NewLine);
            }
            return true;
        }
        catch { return false; }
    }
}
