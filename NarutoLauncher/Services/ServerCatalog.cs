using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NarutoLauncher.Services;

/// <summary>
/// 区服目录：选区页 hyol_select.js 的服务器列表 + 最新服务器 + 推荐服务器。
/// 首次拉取后缓存到本地 server_catalog.json，后续离线可用；每次启动网络优先刷新。
/// </summary>
public static class ServerCatalog
{
    private const string Url =
        "https://commwebgame.game.qq.com/webgame_login/data/js/hyol_select.js?sGameKind=hyol";

    private static Data? _data;
    private static readonly object Lock = new();

    private static string CachePath => Path.Combine(AppContext.BaseDirectory, "server_catalog.json");
    private static string LogPath => Path.Combine(AppContext.BaseDirectory, "server_catalog.log");

    private sealed class Data
    {
        public Dictionary<int, string> Servers { get; set; } = new();
        public List<int> NewServers { get; set; } = new();
        public List<int> Recommend { get; set; } = new();
    }

    /// <summary>按区服 ID 查区服名（不存在返回 null）。</summary>
    public static async Task<string?> GetServerNameAsync(int serverId)
    {
        var d = await LoadAsync();
        return d is not null && d.Servers.TryGetValue(serverId, out var name)
            ? name
            : null;
    }

    /// <summary>最新服务器（新开区服）ID 列表。</summary>
    public static async Task<List<int>> GetNewServersAsync()
    {
        var d = await LoadAsync();
        return d?.NewServers ?? new List<int>();
    }

    /// <summary>推荐服务器 ID 列表。</summary>
    public static async Task<List<int>> GetRecommendAsync()
    {
        var d = await LoadAsync();
        return d?.Recommend ?? new List<int>();
    }

    private static async Task<Data?> LoadAsync()
    {
        if (_data is not null)
            return _data;
        lock (Lock)
        {
            if (_data is not null)
                return _data;
            _data = new Data();
        }

        var net = await FetchAsync();
        if (net is { Servers.Count: > 0 })
        {
            try { File.WriteAllText(CachePath, JsonSerializer.Serialize(net)); } catch { }
            lock (Lock) _data = net;
            Log($"网络拉取成功，服务器 {net.Servers.Count} 个、最新 {net.NewServers.Count} 个");
            return net;
        }

        try
        {
            if (File.Exists(CachePath))
            {
                var cached = JsonSerializer.Deserialize<Data>(File.ReadAllText(CachePath));
                if (cached is { Servers.Count: > 0 })
                {
                    lock (Lock) _data = cached;
                    Log($"使用本地缓存，服务器 {cached.Servers.Count} 个");
                    return cached;
                }
            }
        }
        catch { }

        Log("网络与缓存均不可用");
        return null;
    }

    private static async Task<Data?> FetchAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var bytes = await client.GetByteArrayAsync(Url);
            var text = Encoding.GetEncoding("GBK").GetString(bytes);

            var data = new Data();
            // 服务器列表：id:{"sServerName":"..."}
            var block = Regex.Match(text, @"JSON_server=\{(.*?)\};",
                                    RegexOptions.Singleline).Groups[1].Value;
            foreach (Match m in Regex.Matches(
                block, @"(\d+):\{""sServerName"":""([^""]*)"""))
            {
                if (int.TryParse(m.Groups[1].Value, out var id))
                    data.Servers[id] = m.Groups[2].Value;
            }
            // 最新服务器：JSON_new=[...]
            var nm = Regex.Match(text, @"JSON_new=\[([\d,]*)\]");
            if (nm.Success)
                data.NewServers = ParseIds(nm.Groups[1].Value);
            // 推荐服务器：JSON_recommend=[...]
            var rm = Regex.Match(text, @"JSON_recommend=\[([\d,]*)\]");
            if (rm.Success)
                data.Recommend = ParseIds(rm.Groups[1].Value);

            return data.Servers.Count > 0 ? data : null;
        }
        catch (Exception ex)
        {
            Log("拉取失败: " + ex.Message);
            return null;
        }
    }

    private static List<int> ParseIds(string csv)
    {
        var list = new List<int>();
        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(part, out var id))
                list.Add(id);
        }
        return list;
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
}