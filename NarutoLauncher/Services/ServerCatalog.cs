using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace NarutoLauncher.Services;

/// <summary>
/// 区服 ID → 区名映射（选区页 hyol_select.js 的 JSON_server，运行时拉取一次并缓存）。
/// 区名只保留「公测856区」，去掉后面的服务器名（如「光刃那都」）。
/// </summary>
public static class ServerCatalog
{
    private const string Url =
        "https://commwebgame.game.qq.com/webgame_login/data/js/hyol_select.js?sGameKind=hyol";

    private static Dictionary<int, string>? _map;

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
                block, @"(\d+):\{{""sServerName"":""([^""]*)"""))
            {
                if (int.TryParse(m.Groups[1].Value, out var id))
                    map[id] = TrimZoneName(m.Groups[2].Value);
            }
            _map = map;
            return map;
        }
        catch
        {
            return null;
        }
    }

    private static string TrimZoneName(string name)
    {
        var i = name.IndexOf(' ');
        return i > 0 ? name[..i] : name;
    }
}