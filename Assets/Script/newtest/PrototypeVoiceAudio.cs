using System;
using System.IO;
using System.Text;

// Pure PCM encoder: downmix microphone channels and upload only recorded frames.
public static class PrototypeVoiceAudio
{
    public static byte[] EncodeWav(float[] samples, int channels, int sampleRate, out float rms)
    {
        if (samples == null || channels < 1 || sampleRate < 1 || samples.Length == 0 ||
            samples.Length % channels != 0) throw new ArgumentException("Invalid microphone samples.");
        int frames = samples.Length / channels;
        double energy = 0;
        using (var memory = new MemoryStream(44 + frames * 2))
        using (var writer = new BinaryWriter(memory, Encoding.ASCII))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + frames * 2);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)1); // Mono
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(frames * 2);
            for (int frame = 0; frame < frames; frame++)
            {
                double mono = 0;
                for (int channel = 0; channel < channels; channel++)
                    mono += samples[frame * channels + channel];
                mono = Math.Max(-1.0, Math.Min(1.0, mono / channels));
                energy += mono * mono;
                writer.Write((short)Math.Round(mono * short.MaxValue));
            }
            rms = (float)Math.Sqrt(energy / frames);
            writer.Flush();
            return memory.ToArray();
        }
    }
}
