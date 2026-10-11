using System.IO;
using Media = System.Windows.Media;

namespace Hangline.Shell;

/// <summary>
/// The two sounds the line makes, synthesised at startup rather than shipped as
/// assets — the original draws every one of its images in code too.
/// <para>
/// "Tink" when a photo hangs, "Pop" when one is taken down, and the system's own
/// Recycle Bin sound for a discard, because that one already means something.
/// </para>
/// </summary>
public static class SoundKit
{
    private const int Rate = 22050;

    private static readonly Dictionary<string, (string Name, double At)[]> Partials = new()
    {
        // A bright two-note ping: enough to notice, short enough to ignore.
        ["Tink"] = [("1975.5", 0.0), ("2637.0", 0.09)],
        // A duller, shorter one for something leaving.
        ["Pop"] = [("320.0", 0.0)],
    };

    private static readonly Dictionary<string, double> Decay = new() { ["Tink"] = 11, ["Pop"] = 34 };
    private static readonly Dictionary<string, double> BaseGain = new() { ["Tink"] = 0.30, ["Pop"] = 0.55 };

    private static readonly Dictionary<string, byte[]> Cache = [];

    public static void Play(string name, double volume)
    {
        try
        {
            if (!Partials.TryGetValue(name, out (string Name, double At)[]? tone))
            {
                // A discard already has the shell's own meaning attached to it.
                if (name == "Trash") System.Media.SystemSounds.Hand.Play();
                return;
            }

            // SoundPlayer has no volume, so the level is mixed in at synthesis.
            string key = $"{name}:{volume:F2}";
            if (!Cache.TryGetValue(key, out byte[]? wav))
            {
                wav = Tone(
                    tone.Select(p => (double.Parse(p.Name, System.Globalization.CultureInfo.InvariantCulture), p.At)).ToArray(),
                    Decay[name], BaseGain[name] * Math.Clamp(volume, 0, 1));
                Cache[key] = wav;
            }

            using var player = new System.Media.SoundPlayer(new MemoryStream(wav));
            player.Play();
        }
        catch (Exception ex)
        {
            Log.Write($"sound {name}: {ex.Message}");
        }
    }

    /// A few decaying partials stacked into a 16-bit mono WAV.
    private static byte[] Tone((double Freq, double At)[] partials, double decay, double gain)
    {
        double seconds = partials.Max(p => p.At) + 0.28;
        int count = (int)(Rate * seconds);
        var samples = new short[count];

        foreach ((double freq, double at) in partials)
        {
            int offset = (int)(Rate * at);
            for (int i = 0; i + offset < count; i++)
            {
                double t = i / (double)Rate;
                double envelope = Math.Exp(-decay * t) * (1 - Math.Exp(-900 * t));
                samples[i + offset] += (short)(Math.Sin(2 * Math.PI * freq * t) * envelope * gain * short.MaxValue);
            }
        }

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        int bytes = count * 2;
        w.Write("RIFF"u8); w.Write(36 + bytes); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(bytes);
        foreach (short s in samples) w.Write(s);
        return ms.ToArray();
    }
}
