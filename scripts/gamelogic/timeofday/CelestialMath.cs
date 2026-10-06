using Godot;

namespace GameLogic;

public static class CelestialMath
{
    // 简化天球：春分式 06:00 升起、18:00 落下，不模拟经纬度、季节或食。
    public static Vector3 Direction(double hour, double phaseOffset, float noonElevation, float azimuth)
    {
        float angle = (float)(((hour - 6) / 24 - phaseOffset) * System.Math.Tau);
        float tilt = Mathf.DegToRad(Mathf.Clamp(noonElevation, 10, 89));
        return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * Mathf.Sin(tilt), Mathf.Sin(angle) * Mathf.Cos(tilt))
            .Rotated(Vector3.Up, Mathf.DegToRad(azimuth)).Normalized();
    }

    public static float Smooth(float from, float to, float value)
    {
        float t = Mathf.Clamp((value - from) / (to - from), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
