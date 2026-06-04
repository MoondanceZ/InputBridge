using System.Text.Json.Serialization;

namespace InputBridge;

[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(ConfigMessage))]
[JsonSerializable(typeof(RebaseMessage))]
public partial class AppJsonContext : JsonSerializerContext;

public sealed record ConfigMessage(
    [property: JsonPropertyName("type")]
    string Type,
    [property: JsonPropertyName("appVersion")]
    string AppVersion,
    [property: JsonPropertyName("backspaceLimit")]
    int BackspaceLimit,
    [property: JsonPropertyName("autoClear")]
    bool AutoClear,
    [property: JsonPropertyName("autoClearTime")]
    int AutoClearTime,
    [property: JsonPropertyName("smartDetection")]
    bool SmartDetection);

public sealed record RebaseMessage(
    [property: JsonPropertyName("type")]
    string Type);

