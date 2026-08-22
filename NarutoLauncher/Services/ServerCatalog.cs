using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NarutoLauncher.Services;

/// <summary>
/// 区服 ID → 区名映射（选区页 hyol_select.js 的 JSON_server）。
/// 首次拉取后缓存到本地 server_catalog.json，后续离线可用。
/// 区名只保留「公测856区」，去掉后面的服务器名（如「光刃那都」）。
/// </summary>
public static class ServerCatalog
{
    private const string Url =
        "https://commwebgame.game.qq.com/webgame_login/data/js/hyol_select.js?sGameKind=hyol";

    private static Dictionary<int, string>? _map;
    private static readonly object Lock = new();

    private static string CachePath => Path.Combine(AppContext.BaseDirectory, "server_catalog.json");
    private static string LogPath => Path.Combine(AppContext.BaseDirectory, "server_catalog.log");

    /// <summary>按区服 ID 查区名（拉取失败返回 null）。</summary>
    public static async Task<string?> GetZoneNameAsync(int serverId)
    {
        var map = await LoadAsync();
        return map is not null && map.TryGetValue(serverId, out var name)
            ? name
            : null;
    }

    private static async Task<Dictionary<int, string>?> LoadAsync()
    {
        if (_map is not null)
            return _map;
        lock (Lock)
        {
            if (_map is not null)
                return _map;
            _map = new Dictionary<int, string>();
        }

        // 1) 优先网络拉取
        var net = await FetchAsync();
        if (net is { Count: > 0 })
        {
            try { File.WriteAllText(CachePath, JsonSerializer.Serialize(net)); } catch { }
            lock (Lock) _map = net;
            Log("网络拉取成功，共 " + net.Count + " 个区服");
            return net;
        }

        // 2) 失败读本地缓存
        try
        {
            if (File.Exists(CachePath))
            {
                var cached = JsonSerializer.Deserialize<Dictionary<int, string>>(
                    File.ReadAllText(CachePath));
                if (cached is { Count: > 0 })
                {
                    lock (Lock) _map = cached;
                    Log("使用本地缓存，共 " + cached.Count + " 个区服");
                    return cached;
                }
            }
        }
        catch { }

        Log("网络与缓存均不可用");
        return null;
    }

    private static async Task<Dictionary<int, string>?> FetchAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var bytes = await client.GetByteArrayAsync(Url);
            // 选区页 JS 为 GBK 编码
            var text = Encoding.GetEncoding("GBK").GetString(bytes);
            var block = Regex.Match(text, @"JSON_server=\{(.*?)\};",
                                    RegexOptions.Singleline).Groups[1].Value;
            var map = new Dictionary<int, string>();
            foreach (Match m in Regex.Matches(
                block, @"(\d+):\{""sServerName"":""([^""]*)"""))
            {
                if (int.TryParse(m.Groups[1].Value, out var id))
                    map[id] = TrimZoneName(m.Groups[2].Value);
            }
            return map;
        }
        catch (Exception ex)
        {
            Log("拉取失败: " + ex.Message);
            return null;
        }
    }

    private static void Log(string msg)
    {
        try
        {
            File.AppendAllText(LogPath,
                $"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
        }
        catch { }
    }

    private static string TrimZoneName(string name)
    {
        var i = name.IndexOf(' ');
        return i > 0 ? name[..i] : name;
    }
}