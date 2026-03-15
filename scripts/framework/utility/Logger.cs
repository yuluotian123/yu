using Godot;
namespace Framework.Utility
{
    public static class Logger
    {
        public static bool EnableInfo = true;
        public static bool EnableWarning = true;
        public static bool EnableError = true;
        public static void Info(string msg)
        {
            if (EnableInfo)
                GD.Print($"[INFO] {msg}");
        }
        public static void Warn(string msg)
        {
            if (EnableWarning)
                GD.PushWarning($"[WARN] {msg}");
        }
        public static void Error(string msg)
        {
            if (EnableError)
                GD.PushError($"[ERROR] {msg}");
        }
    }
}
