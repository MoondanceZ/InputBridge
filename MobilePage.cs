using System.Reflection;

namespace InputBridge;

public static class MobilePage
{
    private const string ResourceName = "InputBridge.Resources.MobilePage.html";

    public static string Render(AppSettings settings)
    {
        var html = ReadTemplate();
        return html
            .Replace("{{autoClearChecked}}", settings.AutoClear ? "checked" : "")
            .Replace("{{smartChecked}}", settings.SmartDetection ? "checked" : "")
            .Replace("{{autoClearMs}}", (settings.AutoClearTime * 1000).ToString())
            .Replace("{{appVersion}}", AppVersion.Current);
    }

    private static string ReadTemplate()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream == null)
        {
            throw new InvalidOperationException($"Missing embedded resource: {ResourceName}");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

