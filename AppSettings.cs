using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;

namespace InputBridge;

public sealed class AppSettings
{
    private const string AppDataFolder = "InputBridge";
    private const string LegacyAppDataFolder = "InputSyncHelper.WinForms";

    public string Ip { get; set; } = "";
    public int Port { get; set; } = 5505;
    public int BackspaceLimit { get; set; } = 100;
    public bool SmartDetection { get; set; } = true;
    public bool AutoClear { get; set; }
    public int AutoClearTime { get; set; } = 15;

    public string EffectiveIp => string.IsNullOrWhiteSpace(Ip) ? GetPreferredLocalIp() : Ip.Trim();

    public string Url => $"http://{EffectiveIp}:{Port}";

    public static string SettingsPath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppDataFolder);
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }
    }

    private static string LegacySettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        LegacyAppDataFolder,
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                if (File.Exists(LegacySettingsPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                    File.Copy(LegacySettingsPath, SettingsPath, overwrite: false);
                }
                else
                {
                    var defaults = new AppSettings();
                    defaults.Save();
                    return defaults;
                }
            }

            if (!File.Exists(SettingsPath))
            {
                var defaults = new AppSettings();
                defaults.Save();
                return defaults;
            }

            var settings = JsonSerializer.Deserialize(File.ReadAllText(SettingsPath), AppJsonContext.Default.AppSettings) ?? new AppSettings();
            if (settings.Port == 5000)
            {
                settings.Port = 5505;
                settings.Save();
            }

            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, AppJsonContext.Default.AppSettings);
        File.WriteAllText(SettingsPath, json);
    }

    public static IReadOnlyList<string> GetLocalIpCandidates()
    {
        var candidates = new List<(string Address, int Score)>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            var name = $"{nic.Name} {nic.Description}".ToLowerInvariant();
            var isVirtual = name.Contains("loopback")
                || name.Contains("virtual")
                || name.Contains("vmware")
                || name.Contains("hyper-v")
                || name.Contains("docker")
                || name.Contains("wsl")
                || name.Contains("vpn")
                || name.Contains("tap")
                || name.Contains("tunnel");

            foreach (var address in nic.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                {
                    continue;
                }

                var ip = address.Address.ToString();
                if (IPAddress.IsLoopback(address.Address) || ip.StartsWith("169.254.", StringComparison.Ordinal))
                {
                    continue;
                }

                var score = GetPrivateIpScore(ip);
                if (nic.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet)
                {
                    score += 30;
                }

                if (isVirtual)
                {
                    score -= 80;
                }

                candidates.Add((ip, score));
            }
        }

        return candidates
            .GroupBy(x => x.Address)
            .Select(g => g.OrderByDescending(x => x.Score).First())
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Address)
            .Select(x => x.Address)
            .ToArray();
    }

    private static string GetPreferredLocalIp()
    {
        var candidate = GetLocalIpCandidates().FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            return candidate;
        }

        return GetRouteBasedLocalIp();
    }

    private static int GetPrivateIpScore(string ip)
    {
        if (ip.StartsWith("192.168.", StringComparison.Ordinal))
        {
            return 100;
        }

        if (ip.StartsWith("10.", StringComparison.Ordinal))
        {
            return 90;
        }

        var parts = ip.Split('.');
        if (parts.Length == 4 && int.TryParse(parts[1], out var second) && second is >= 16 and <= 31)
        {
            return 80;
        }

        return 10;
    }

    private static string GetRouteBasedLocalIp()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 80);
            return ((IPEndPoint)socket.LocalEndPoint!).Address.ToString();
        }
        catch
        {
            return "127.0.0.1";
        }
    }
}

