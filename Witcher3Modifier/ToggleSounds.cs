using System.Media;

namespace Witcher3Modifier;

internal static class ToggleSounds
{
    private static readonly SoundPlayer On = new(new MemoryStream(Wave(1568)));
    private static readonly SoundPlayer Off = new(new MemoryStream(Wave(392)));
    private static readonly SoundPlayer Action = new(new MemoryStream(Wave(1568)));
    internal static void Preload()
    {
        try { On.Load(); Off.Load(); Action.Load(); }
        catch { /* Audio availability does not affect trainer functions. */ }
    }
    internal static void PlayAction()
    {
        try { Action.Play(); }
        catch { /* Audio availability does not change a confirmed operation. */ }
    }
    internal static void Play(bool enabled)
    {
        try { (enabled ? On : Off).Play(); }
        catch { /* Audio availability does not change a successfully applied setting. */ }
    }
    private static byte[] Wave(int frequency, int milliseconds = 90)
    {
        const int rate = 44100;
        int samples = rate * milliseconds / 1000;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8.ToArray()); writer.Write(36 + samples * 2);
        writer.Write("WAVEfmt "u8.ToArray()); writer.Write(16);
        writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
        writer.Write((short)2); writer.Write((short)16);
        writer.Write("data"u8.ToArray()); writer.Write(samples * 2);
        for (int i = 0; i < samples; i++)
        {
            double time = (double)i / rate;
            double envelope = Math.Min(1, i / 44.0) * Math.Exp(-time * 24) * Math.Min(1, (samples - 1 - i) / 220.0);
            double tone = .8 * Math.Sin(2 * Math.PI * frequency * time) + .2 * Math.Sin(2 * Math.PI * frequency * 2 * time);
            writer.Write((short)(6600 * envelope * tone));
        }
        return stream.ToArray();
    }
}
