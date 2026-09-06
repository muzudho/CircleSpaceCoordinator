namespace CircleSpaceCoordinator.Desktop.Windows.Screenshots;

using Microsoft.Xna.Framework.Audio;

internal static class ScreenshotShutterSound
{
    public static SoundEffect Create()
    {
        const int sampleRate = 44100;
        const float duration = 0.19f;
        var buffer = new byte[(int)(sampleRate * duration) * sizeof(short)];
        uint noiseState = 0x4B1D5EED;

        for (var index = 0; index < buffer.Length / sizeof(short); index++)
        {
            var time = index / (float)sampleRate;
            noiseState = noiseState * 1664525u + 1013904223u;
            var noise = ((noiseState >> 8) / 8388607.5f) - 1f;
            var firstClick = ShutterPulse(time, 0f, 0.034f, 68f, noise);
            var secondClick = ShutterPulse(time, 0.072f, 0.052f, 52f, -noise);
            var mechanism = time >= 0.02f
                ? MathF.Sin(MathF.Tau * (118f - 240f * (time - 0.02f)) * (time - 0.02f))
                    * MathF.Exp(-24f * (time - 0.02f)) * 0.22f
                : 0f;
            var sample = (short)(Math.Clamp(firstClick + secondClick + mechanism, -1f, 1f) * short.MaxValue * 0.78f);
            buffer[index * 2] = (byte)(sample & 0xff);
            buffer[index * 2 + 1] = (byte)((sample >> 8) & 0xff);
        }

        return new SoundEffect(buffer, sampleRate, AudioChannels.Mono);
    }

    private static float ShutterPulse(float time, float start, float duration, float decay, float noise)
    {
        var localTime = time - start;
        if (localTime < 0f || localTime >= duration)
            return 0f;
        var envelope = Math.Clamp(localTime / 0.0015f, 0f, 1f) * MathF.Exp(-decay * localTime);
        var metal = MathF.Sin(MathF.Tau * 1850f * localTime) * 0.34f
            + MathF.Sin(MathF.Tau * 2730f * localTime) * 0.16f;
        return (noise * 0.72f + metal) * envelope;
    }
}
