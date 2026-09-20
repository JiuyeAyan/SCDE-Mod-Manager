using NVorbis;
using SHCDESE.Logging;
using System;
using System.IO;
using UnityEngine;

namespace SHCDESE.Extensions;

public static class AudioClipExtensions
{

    /// <summary>
    /// Identifies an audio format from a byte array and creates an AudioClip.
    /// NOTE: ONLY CALL THIS FROM THE UNITY MAIN THREAD.
    /// </summary>
    /// <param name="audioData">The byte array containing the audio file data (WAV or OGG).</param>
    /// <returns>An AudioClip, or null if the format is unsupported or decoding fails.</returns>
    public static AudioClip LoadAudioClipFromBytes(byte[] audioData)
    {
        if (audioData == null || audioData.Length < 4)
        {
            LogHelper.Error("Audio data is null or too short to identify.");
            return null;
        }

        string header = System.Text.Encoding.ASCII.GetString(audioData, 0, 4);
        if (header == "RIFF")
        {
            LogHelper.Debug("Identified WAV format.");
            try
            {
                return AudioClipExtensions.LoadWavFromBytes(audioData);
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, "Error loading WAV data.");
                return null;
            }
        }

        if (header == "OggS")
        {
            LogHelper.Debug("Identified OGG Vorbis format.");
            try
            {
                return AudioClipExtensions.LoadOggFromBytes(audioData);
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, "Error loading OGG data.");
                return null;
            }
        }

        LogHelper.Error($"Unknown or unsupported audio format with header: {header}");
        return null;
    }

    /// <summary>
    /// Decodes a byte array of OGG Vorbis audio data and creates an AudioClip.
    /// </summary>
    public static AudioClip LoadOggFromBytes(byte[] oggData)
    {
        try
        {
            using (MemoryStream memoryStream = new MemoryStream(oggData))
            using (VorbisReader vorbisReader = new VorbisReader(memoryStream))
            {
                int channels = vorbisReader.Channels;
                int sampleRate = vorbisReader.SampleRate;
                int totalSamples = (int)vorbisReader.TotalSamples;

                float[] pcmData = new float[totalSamples * channels];
                vorbisReader.ReadSamples(pcmData, 0, pcmData.Length);

                AudioClip audioClip = AudioClip.Create(
                    $"OggFromBytes-{Guid.NewGuid()}",
                    totalSamples,
                    channels,
                    sampleRate,
                    false
                );
                audioClip.SetData(pcmData, 0);
                return audioClip;
            }
        }
        catch (Exception e)
        {
            LogHelper.Error(e, "Failed to decode OGG data");
            return null;
        }
    }

    /// <summary>
    /// Loads a WAV byte array and returns an AudioClip representing it.
    /// </summary>
    /// <param name="wavFile">The wav file.</param>
    /// <returns>Created AudioClip</returns>
    /// <remarks>
    /// <para>Supports 16-bit PCM WAV files at 44100 Hz with either 1 (mono) or 2 (stereo) channels.</para>
    /// <para>
    /// Unity's AudioClip expects interleaved samples for multi-channel audio, which matches the
    /// standard WAV layout (L, R, L, R, ...), so no re-interleaving is required for stereo.
    /// </para>
    /// </remarks>
    public static AudioClip LoadWavFromBytes(byte[] wavFile)
    {
        // Basic sanity checks
        if (wavFile == null || wavFile.Length < 44)
            throw new ArgumentException("Invalid WAV data: too short to contain a valid header.");

        // Verify the RIFF/WAVE header
        string riff = System.Text.Encoding.ASCII.GetString(wavFile, 0, 4);
        string wave = System.Text.Encoding.ASCII.GetString(wavFile, 8, 4);
        if (riff != "RIFF" || wave != "WAVE")
            throw new FormatException("Invalid WAV file: missing RIFF/WAVE header.");

        // Parse header fields
        short audioFormat = BitConverter.ToInt16(wavFile, 20);  // PCM = 1
        short numChannels = BitConverter.ToInt16(wavFile, 22);
        int sampleRate = BitConverter.ToInt32(wavFile, 24);
        short bitsPerSample = BitConverter.ToInt16(wavFile, 34);

        // Validate format
        if (audioFormat != 1)
            throw new NotSupportedException($"Unsupported WAV format: {audioFormat}. Only PCM (1) is supported.");
        if (numChannels != 1 && numChannels != 2)
            throw new NotSupportedException($"Unsupported number of channels: {numChannels}. Only mono (1) and stereo (2) are supported.");
        if (sampleRate != 44100)
            throw new NotSupportedException($"Unsupported sample rate: {sampleRate}. Only 44100 Hz supported.");
        if (bitsPerSample != 16)
            throw new NotSupportedException($"Unsupported bit depth: {bitsPerSample}. Only 16-bit supported.");

        // Locate the 'data' chunk
        int pos = 12; // skip "RIFF" + size + "WAVE"
        while (pos + 8 < wavFile.Length)
        {
            string chunkId = System.Text.Encoding.ASCII.GetString(wavFile, pos, 4);
            int chunkSize = BitConverter.ToInt32(wavFile, pos + 4);

            if (chunkId == "data")
            {
                int dataOffset = pos + 8;
                int dataSize = chunkSize;

                if (dataOffset + dataSize > wavFile.Length)
                    throw new FormatException("Invalid WAV file: data chunk size exceeds file length.");

                // Each 16-bit sample is 2 bytes. For stereo the data is interleaved (L, R, L, R, ...)
                // which is exactly what Unity's AudioClip.SetData expects, so we decode all values
                // into a flat float array without any re-interleaving.
                int totalSampleValues = dataSize / 2;          // total floats across all channels
                int samplesPerChannel = totalSampleValues / numChannels; // frames for AudioClip.Create

                float[] samples = new float[totalSampleValues];
                int offset = dataOffset;
                for (int i = 0; i < totalSampleValues; i++)
                {
                    short sample = BitConverter.ToInt16(wavFile, offset);
                    samples[i] = sample / 32768f;
                    offset += 2;
                }

                AudioClip clip = AudioClip.Create(
                    $"WavFromBytes-{Guid.NewGuid()}",
                    samplesPerChannel,
                    numChannels,
                    sampleRate,
                    false
                );
                clip.SetData(samples, 0);
                return clip;
            }

            pos += 8 + chunkSize; // move to next chunk
        }

        throw new FormatException("Invalid WAV file: 'data' chunk not found.");
    }
}
