using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace ProCommVR.Hospital.AiTalk
{
    /// <summary>Kleine wav-hulp: opname naar 16 kHz mono PCM16, en wav-bytes naar een AudioClip.</summary>
    public static class WavUtility
    {
        public const int ServerSampleRate = 16000;

        /// <summary>Samples (interleaved) naar een wav van 16 kHz mono, 16 bit.</summary>
        public static byte[] ToWav16kMono(float[] samples, int channels, int sampleRate)
        {
            var mono = ToMono(samples, channels);
            var resampled = Resample(mono, sampleRate, ServerSampleRate);
            return Encode(resampled, ServerSampleRate);
        }

        public static float[] ToMono(float[] samples, int channels)
        {
            if (channels <= 1)
                return samples;
            var frames = samples.Length / channels;
            var mono = new float[frames];
            for (var f = 0; f < frames; f++)
            {
                var sum = 0f;
                for (var c = 0; c < channels; c++)
                    sum += samples[f * channels + c];
                mono[f] = sum / channels;
            }
            return mono;
        }

        /// <summary>Lineaire interpolatie; ruim genoeg voor spraakherkenning.</summary>
        public static float[] Resample(float[] input, int fromRate, int toRate)
        {
            if (fromRate == toRate || input.Length == 0)
                return input;
            var length = (int)((long)input.Length * toRate / fromRate);
            var output = new float[length];
            var step = (double)fromRate / toRate;
            for (var i = 0; i < length; i++)
            {
                var pos = i * step;
                var index = (int)pos;
                var frac = (float)(pos - index);
                var a = input[Math.Min(index, input.Length - 1)];
                var b = input[Math.Min(index + 1, input.Length - 1)];
                output[i] = a + (b - a) * frac;
            }
            return output;
        }

        public static byte[] Encode(float[] mono, int sampleRate)
        {
            using var stream = new MemoryStream(44 + mono.Length * 2);
            using var w = new BinaryWriter(stream);
            w.Write(Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + mono.Length * 2);
            w.Write(Encoding.ASCII.GetBytes("WAVE"));
            w.Write(Encoding.ASCII.GetBytes("fmt "));
            w.Write(16);
            w.Write((short)1);              // PCM
            w.Write((short)1);              // mono
            w.Write(sampleRate);
            w.Write(sampleRate * 2);        // bytes per seconde
            w.Write((short)2);              // block align
            w.Write((short)16);             // bits
            w.Write(Encoding.ASCII.GetBytes("data"));
            w.Write(mono.Length * 2);
            foreach (var s in mono)
                w.Write((short)Mathf.RoundToInt(Mathf.Clamp(s, -1f, 1f) * 32767f));
            return stream.ToArray();
        }

        /// <summary>PCM16-wav (zoals Piper ze maakt) naar een AudioClip. Null als het geen bruikbare wav is.</summary>
        public static AudioClip ToAudioClip(byte[] wav, string name = "reply")
        {
            if (wav == null || wav.Length < 44 || Encoding.ASCII.GetString(wav, 0, 4) != "RIFF")
                return null;
            int channels = 1, sampleRate = 22050, bits = 16;
            var pos = 12;
            while (pos + 8 <= wav.Length)
            {
                var id = Encoding.ASCII.GetString(wav, pos, 4);
                var size = BitConverter.ToInt32(wav, pos + 4);
                var body = pos + 8;
                if (id == "fmt ")
                {
                    channels = BitConverter.ToInt16(wav, body + 2);
                    sampleRate = BitConverter.ToInt32(wav, body + 4);
                    bits = BitConverter.ToInt16(wav, body + 14);
                }
                else if (id == "data")
                {
                    if (bits != 16 || channels < 1)
                        return null;
                    size = Math.Min(size, wav.Length - body);
                    var count = size / 2;
                    var data = new float[count];
                    for (var i = 0; i < count; i++)
                        data[i] = BitConverter.ToInt16(wav, body + i * 2) / 32768f;
                    var clip = AudioClip.Create(name, count / channels, channels, sampleRate, false);
                    clip.SetData(data, 0);
                    return clip;
                }
                pos = body + size + (size & 1);
            }
            return null;
        }
    }
}
