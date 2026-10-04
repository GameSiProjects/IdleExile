using System;
using Godot;

namespace IdleExile.UI;

/// <summary>
/// Synthesizes procedural acoustic waveforms for audio alerts, including the iconic
/// metallic "Ting!" drop chime for Tier 1 items (Task 6.2).
/// Pure procedural synthesis generates high-definition PCM audio in-memory, eliminating
/// external asset dependencies and guaranteeing 100% offline portability.
/// </summary>
public static class DropSoundSynthesizer
{
    public const int DefaultSampleRate = 44100;
    public const float Tier1ChimeDuration = 0.65f;

    private static AudioStreamWav? _cachedTier1Chime;

    /// <summary>
    /// Singleton cached AudioStreamWav instance for the Tier 1 metallic chime ("Ting!").
    /// </summary>
    public static AudioStreamWav Tier1DropChime => _cachedTier1Chime ??= CreateTier1DropChime();

    /// <summary>
    /// Generates a new AudioStreamWav configured with the metallic drop chime.
    /// </summary>
    public static AudioStreamWav CreateTier1DropChime(int sampleRate = DefaultSampleRate, float duration = Tier1ChimeDuration)
    {
        byte[] pcmData = GenerateTier1ChimePcm(sampleRate, duration);

        var stream = new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = sampleRate,
            Stereo = false,
            LoopMode = AudioStreamWav.LoopModeEnum.Disabled,
            Data = pcmData
        };

        return stream;
    }

    /// <summary>
    /// Synthesizes the raw 16-bit signed PCM mono audio bytes for the crisp metallic chime.
    /// Harmonics modeled on high-grade bronze/silver bell strike with high-frequency crystalline resonance.
    /// </summary>
    public static byte[] GenerateTier1ChimePcm(int sampleRate = DefaultSampleRate, float duration = Tier1ChimeDuration)
    {
        int totalSamples = (int)(sampleRate * duration);
        byte[] pcmBuffer = new byte[totalSamples * 2];

        // Fundamental and crystalline overtone frequencies (Hz)
        const float f0 = 2093.00f; // C7 (High fundamental chime)
        const float f1 = 1760.00f; // A6 (Secondary metallic resonance)
        const float f2 = 3520.00f; // A7 (Sparkle overtone)
        const float f3 = 2637.02f; // E7 (Bell metal shimmer)
        const float f4 = 4186.01f; // C8 (Ultra crystalline edge)

        // Exponential decay time constants (seconds)
        const float tau0 = 0.22f;
        const float tau1 = 0.20f;
        const float tau2 = 0.11f;
        const float tau3 = 0.14f;
        const float tau4 = 0.07f;

        var rng = new Random(1337); // Deterministic pseudo-random seed for hammer strike click

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;

            // 1. Hammer strike transient (first 8ms)
            float strike = 0f;
            if (t < 0.008f)
            {
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float strikeEnv = MathF.Exp(-t / 0.0025f);
                strike = noise * strikeEnv * 0.25f;
            }

            // 2. Harmonic bell resonance
            float chime0 = MathF.Sin(2f * MathF.PI * f0 * t) * MathF.Exp(-t / tau0) * 0.45f;
            float chime1 = MathF.Sin(2f * MathF.PI * f1 * t) * MathF.Exp(-t / tau1) * 0.25f;
            float chime2 = MathF.Sin(2f * MathF.PI * f2 * t) * MathF.Exp(-t / tau2) * 0.20f;
            float chime3 = MathF.Sin(2f * MathF.PI * f3 * t) * MathF.Exp(-t / tau3) * 0.12f;
            float chime4 = MathF.Sin(2f * MathF.PI * f4 * t) * MathF.Exp(-t / tau4) * 0.08f;

            float combined = chime0 + chime1 + chime2 + chime3 + chime4 + strike;

            // 3. Anti-click 2ms attack ramp
            if (t < 0.002f)
            {
                combined *= t / 0.002f;
            }

            // 4. Smooth tail decay to silence
            if (t > duration - 0.05f)
            {
                float tail = (duration - t) / 0.05f;
                combined *= MathF.Max(0f, tail);
            }

            // Clamp sample to [-1.0, 1.0]
            float clamped = Math.Clamp(combined, -1.0f, 1.0f);

            // Scale to 16-bit signed integer [-32767, 32767]
            short pcmSample = (short)(clamped * 32767f);

            // Little-endian byte packing
            pcmBuffer[i * 2] = (byte)(pcmSample & 0xFF);
            pcmBuffer[i * 2 + 1] = (byte)((pcmSample >> 8) & 0xFF);
        }

        return pcmBuffer;
    }
}
