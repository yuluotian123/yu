using System;

namespace GameLogic;

public enum WeatherKind { Clear, Overcast, Rain, Snow }

/// <summary>按绝对游戏时间取样；快进、回退和读档不会消耗随机序列。</summary>
public static class WeatherSchedule
{
    public static WeatherKind Sample(long period, int seed, double clear, double overcast, double rain, double snow)
    {
        clear = Weight(clear); overcast = Weight(overcast); rain = Weight(rain); snow = Weight(snow);
        double sum = clear + overcast + rain + snow;
        if (sum == 0) return WeatherKind.Clear;
        // SplitMix64 的整数混合在各平台保持一致，不依赖 System.Random 的实现版本。
        ulong hash;
        unchecked
        {
            hash = (ulong)period + (ulong)(uint)seed + 0x9E3779B97F4A7C15UL;
            hash = (hash ^ (hash >> 30)) * 0xBF58476D1CE4E5B9UL;
            hash = (hash ^ (hash >> 27)) * 0x94D049BB133111EBUL;
            hash ^= hash >> 31;
        }
        double value = (hash >> 11) * (1.0 / (1UL << 53)) * sum;
        if (value < clear) return WeatherKind.Clear;
        if (value < clear + overcast) return WeatherKind.Overcast;
        if (value < clear + overcast + rain) return WeatherKind.Rain;
        return WeatherKind.Snow;
    }

    private static double Weight(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 100) : 0;
}
