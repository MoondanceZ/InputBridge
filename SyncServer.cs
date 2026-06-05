using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace InputBridge;

public sealed class SyncServer : IDisposable
{
    private readonly InputSimulator _input;
    private readonly Action<bool, string?> _connectionChanged;
    private readonly Action<string>? _syncActivity;
    private readonly ConcurrentDictionary<WebSocket, ClientState> _clients = new();
    private readonly object _stateLock = new();
    private WebApplication? _app;
    private CancellationTokenSource? _cts;
    private AppSettings _settings;
    private string _syncedText = "";
    private bool _rebaseTriggered;
    private bool _pendingStripPunctuation;

    public SyncServer(AppSettings settings, InputSimulator input, Action<bool, string?> connectionChanged, Action<string>? syncActivity = null)
    {
        _settings = settings;
        _input = input;
        _connectionChanged = connectionChanged;
        _syncActivity = syncActivity;
    }

    public async Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://0.0.0.0:{_settings.Port}");
        _app = builder.Build();
        _app.UseWebSockets();
        _app.MapGet("/", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "0";
            return Results.Content(MobilePage.Render(_settings), "text/html; charset=utf-8");
        });
        _app.MapGet("/assets/app.ico", () => Results.File(ReadAppIcon(), "image/x-icon"));
        _app.Map("/ws", HandleWebSocketAsync);
        await _app.StartAsync(_cts.Token).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var app = _app;
        _app = null;

        _cts?.Cancel();
        AbortClients();

        if (app != null)
        {
            try
            {
                await app.StopAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await app.DisposeAsync().ConfigureAwait(false);
            }
        }

        _cts?.Dispose();
        _cts = null;
        _clients.Clear();
        _connectionChanged(false, null);
    }

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings;
        BroadcastConfig();
    }

    public void NotifyExternalInput()
    {
        if (_input.IsTyping || !_settings.SmartDetection || _clients.IsEmpty)
        {
            return;
        }

        lock (_stateLock)
        {
            if (string.IsNullOrEmpty(_syncedText) || !_clients.Values.Any(c => c.DetectKeyboard))
            {
                return;
            }

            _syncedText = "";
            _rebaseTriggered = true;
            _pendingStripPunctuation = true;
        }

        Broadcast(new RebaseMessage("rebase"));
    }

    public void BroadcastConfig()
    {
        Broadcast(new ConfigMessage(
            "config",
            AppVersion.Current,
            _settings.BackspaceLimit,
            _settings.AutoClear,
            _settings.AutoClearTime,
            _settings.SmartDetection));
    }

    private async Task HandleWebSocketAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var clientIp = FormatClientIp(context.Connection.RemoteIpAddress);
        _clients.TryAdd(socket, new ClientState { Ip = clientIp });
        _connectionChanged(true, clientIp);
        BroadcastConfig();

        try
        {
            var buffer = new byte[8192];
            while (socket.State == WebSocketState.Open)
            {
                var message = await ReceiveTextAsync(socket, buffer);
                if (message == null)
                {
                    break;
                }

                HandleMessage(socket, message);
            }
        }
        finally
        {
            _clients.TryRemove(socket, out _);
            _connectionChanged(!_clients.IsEmpty, GetActiveClientIp());
        }
    }

    private void HandleMessage(WebSocket socket, string message)
    {
        using var doc = JsonDocument.Parse(message);
        var root = doc.RootElement;
        var type = root.GetProperty("type").GetString();

        if (type == "config")
        {
            if (_clients.TryGetValue(socket, out var state))
            {
                state.DetectKeyboard = root.TryGetProperty("detectKeyboard", out var detect) && detect.GetBoolean();
            }
            return;
        }

        if (type == "reset")
        {
            lock (_stateLock)
            {
                _syncedText = "";
                _pendingStripPunctuation = true;
                _rebaseTriggered = false;
            }
            _syncActivity?.Invoke("");
            return;
        }

        if (type == "enter")
        {
            lock (_stateLock)
            {
                _syncedText = "";
                _pendingStripPunctuation = true;
                _rebaseTriggered = false;
            }

            _input.SendEnters(1);
            return;
        }

        if (type != "diff")
        {
            return;
        }

        var newText = root.TryGetProperty("newText", out var newTextElement)
            ? newTextElement.GetString() ?? ""
            : "";

        string addText;
        int deleteCount;

        lock (_stateLock)
        {
            (deleteCount, addText) = ComputeDiff(_syncedText, newText);

            if (_rebaseTriggered)
            {
                deleteCount = 0;
                _rebaseTriggered = false;
            }

            if (_pendingStripPunctuation && deleteCount == 0 && addText.Length > 0)
            {
                const string punctuation = "，。、；：？！“”‘’·…—～,.;:?!'\"";
                if (punctuation.Contains(addText[0]))
                {
                    addText = addText[1..];
                }
                _pendingStripPunctuation = false;
            }

            _syncedText = newText;
        }

        if (deleteCount > 0)
        {
            _input.SendBackspaces(deleteCount, _settings.BackspaceLimit);
            _syncActivity?.Invoke(string.IsNullOrEmpty(newText) ? $"删除 {deleteCount} 个字符" : newText);
        }

        if (!string.IsNullOrEmpty(addText))
        {
            TypeTextWithSoftEnters(addText);
            _syncActivity?.Invoke(newText);
        }
    }

    private void TypeTextWithSoftEnters(string text)
    {
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\r' && text[i] != '\n')
            {
                continue;
            }

            if (i > start)
            {
                _input.TypeText(text[start..i]);
            }

            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                i++;
            }

            _input.SendSoftEnters(1);
            start = i + 1;
        }

        if (start < text.Length)
        {
            _input.TypeText(text[start..]);
        }
    }

    private static (int DeleteCount, string AddText) ComputeDiff(string oldText, string newText)
    {
        var common = 0;
        var max = Math.Min(oldText.Length, newText.Length);
        while (common < max && oldText[common] == newText[common])
        {
            common++;
        }

        return (oldText.Length - common, newText[common..]);
    }

    private void Broadcast(ConfigMessage payload)
    {
        BroadcastJson(JsonSerializer.Serialize(payload, AppJsonContext.Default.ConfigMessage));
    }

    private void Broadcast(RebaseMessage payload)
    {
        BroadcastJson(JsonSerializer.Serialize(payload, AppJsonContext.Default.RebaseMessage));
    }

    private void BroadcastJson(string json)
    {
        var data = Encoding.UTF8.GetBytes(json);

        foreach (var socket in _clients.Keys.ToArray())
        {
            if (socket.State != WebSocketState.Open)
            {
                _clients.TryRemove(socket, out _);
                continue;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await socket.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _clients.TryRemove(socket, out _);
                    AppExceptionHandler.Log("SyncServer.Broadcast", ex);
                }
            });
        }
    }

    private static async Task<string?> ReceiveTextAsync(WebSocket socket, byte[] buffer)
    {
        using var stream = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }

    private void AbortClients()
    {
        foreach (var socket in _clients.Keys.ToArray())
        {
            try
            {
                socket.Abort();
            }
            catch
            {
            }

            _clients.TryRemove(socket, out _);
        }
    }

    private string? GetActiveClientIp()
    {
        return _clients.Values
            .Select(c => c.Ip)
            .FirstOrDefault(ip => !string.IsNullOrWhiteSpace(ip));
    }

    private static string? FormatClientIp(IPAddress? address)
    {
        if (address == null)
        {
            return null;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return address.MapToIPv4().ToString();
        }

        return address.ToString();
    }

    private static byte[] ReadAppIcon()
    {
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("InputBridge.Assets.app.ico")
            ?? throw new InvalidOperationException("Missing embedded app icon.");
        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);
        return buffer.ToArray();
    }

    public void Dispose()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        StopAsync(cts.Token).GetAwaiter().GetResult();
    }

    private sealed class ClientState
    {
        public string? Ip { get; init; }
        public bool DetectKeyboard { get; set; } = true;
    }
}

