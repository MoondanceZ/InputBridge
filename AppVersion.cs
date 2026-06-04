using System.Reflection;

namespace InputBridge;

public static class AppVersion
{
    public static string Current { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";
}

