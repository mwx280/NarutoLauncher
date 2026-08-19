using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NarutoLauncher.Services;

/// <summary>一条公告。</summary>
public class NewsItem
{
    public required string Title { get; set; }
    public required string Category { get; set; }
    public required string Date { get; set; }
    public required string Url { get; set; }
}

/// <summary>一条公告详情。</summary>
public class NewsDetail
{
    public required string Title { get; set; }
    public required string HtmlBody { get; set; }
}

/// <summary>
/// 官方公告服务：抓取、解析并缓存公告列表与详情（huoying.qq.com，GB2312 编码）。
/// 启动时预加载缓存、刷新时增量合并、列表保留最多 10 条。
/// </summary>
public class NewsService
{
    private const string ListUrl = "https://huoying.qq.com/server/website/";
    private const string BaseUrl = "https://huoying.qq.com";
    private const int MaxNews = 10;

    private readonly HttpClient _http;
    private readonly Encoding _encoding;
    private readonly string _cachePath;

    /// <summary>当前公告列表（含缓存）。</summary>
    public List<NewsItem> Items { get; private set; } = new();

    /// <summary>已缓存的公告详情（Url → 正文）。</summary>
    private readonly Dictionary<string, string> _detailCache = new();

    public NewsService()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        _http.Timeout = TimeSpan.FromSeconds(15);
        // 官网公告页面为 GBK/GB2312 编码（代码页 936 覆盖完整）
        _encoding = Encoding.GetEncoding(936);

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NarutoLauncher");
        Directory.CreateDirectory(dir);
        _cachePath = Path.Combine(dir, "news_cache.json");
        LoadCache();
    }

    // ---------- 缓存持久化 ----------

    /// <summary>从本地缓存加载（启动时立即显示，无网络延迟）。</summary>
    private void LoadCache()
    {
        try
        {
            if (!File.Exists(_cachePath)) return;
            var json = File.ReadAllText(_cachePath, Encoding.UTF8);
            var data = JsonSerializer.Deserialize<NewsCache>(json);
            if (data == null) return;
            Items = data.Items ?? new();
            if (data.Details != null)
            {
                _detailCache.Clear();
                foreach (var (k, v) in data.Details)
                    _detailCache[k] = v;
            }
        }
        catch
        {
            Items = new();
            _detailCache.Clear();
        }
    }

    private void SaveCache()
    {
        try
        {
            var data = new NewsCache
            {
                Items = Items,
                Details = _detailCache,
            };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_cachePath, json, Encoding.UTF8);
        }
        catch { }
    }

    // ---------- 抓取与合并 ----------

    /// <summary>
    /// 拉取最新公告列表并增量合并：新公告插入头部，保留最多 10 条。
    /// 返回本次是否有新公告。
    /// </summary>
    public async Task<bool> RefreshAsync()
    {
        var html = await FetchStringAsync(ListUrl);
        var fresh = ParseList(html);
        if (fresh.Count == 0)
            return false;

        var merged = new List<NewsItem>();
        foreach (var item in fresh)
        {
            // 去重：已有相同 URL 的不重复添加
            if (!merged.Any(x => x.Url == item.Url))
                merged.Add(item);
        }
        // 补上缓存的旧公告（保持最多 10 条）
        foreach (var old in Items)
        {
            if (!merged.Any(x => x.Url == old.Url))
                merged.Add(old);
        }

        // 保留最多 10 条
        Items = merged.Take(MaxNews).ToList();
        SaveCache();
        return true;
    }

    /// <summary>立即返回当前列表（缓存优先，无网络）。</summary>
    public List<NewsItem> GetItems() => Items;

    /// <summary>
    /// 获取公告详情：优先用缓存，无缓存则网络抓取并缓存。
    /// </summary>
    public async Task<NewsDetail> FetchDetailAsync(string url)
    {
        // 缓存命中
        if (_detailCache.TryGetValue(url, out var cached))
        {
            var title = Items.FirstOrDefault(i => i.Url == url)?.Title ?? "公告";
            return new NewsDetail { Title = title, HtmlBody = cached };
        }

        var full = url.StartsWith("http") ? url : BaseUrl + url;
        var html = await FetchStringAsync(full);
        var detail = ParseDetail(html);

        // 缓存详情
        _detailCache[url] = detail.HtmlBody;
        SaveCache();
        return detail;
    }

    /// <summary>按 GBK 解码抓取页面。</summary>
    private async Task<string> FetchStringAsync(string url)
    {
        var bytes = await _http.GetByteArrayAsync(url);
        return _encoding.GetString(bytes);
    }

    // ---- 解析列表 ----
    private static List<NewsItem> ParseList(string html)
    {
        var items = new List<NewsItem>();
        // 匹配 ul.ser-news 中的 <li>
        var liRegex = new Regex(
            @"<li\s+class=""s-spr""[^>]*>([\s\S]*?)</li>",
            RegexOptions.IgnoreCase);
        foreach (Match li in liRegex.Matches(html))
        {
            var block = li.Groups[1].Value;
            var typeM = Regex.Match(block, @"class=""[^""]*news-type[^""]*""[^>]*>\s*\[?([^<\]]+)\]?\s*<");
            var titleM = Regex.Match(block, @"class=""[^""]*news-title[^""]*""[^>]*>\s*([^<]+)<");
            var urlM = Regex.Match(block, @"href=""([^""]+)""[^>]*class=""[^""]*news-title");
            var dateM = Regex.Match(block, @"class=""fr""[^>]*>\s*\[?([^<\]]+)\]?\s*<");

            var title = titleM.Success ? HtmlDecode(titleM.Groups[1].Value.Trim()) : "";
            if (string.IsNullOrEmpty(title))
                continue;
            items.Add(new NewsItem
            {
                Title = title,
                Category = typeM.Success ? HtmlDecode(typeM.Groups[1].Value.Trim()) : "公告",
                Date = dateM.Success ? dateM.Groups[1].Value.Trim() : "",
                Url = urlM.Success ? urlM.Groups[1].Value : "",
            });
        }
        return items;
    }

    // ---- 解析详情 ----
    private static NewsDetail ParseDetail(string html)
    {
        var titleM = Regex.Match(html, @"<title>([^<]+)</title>");
        var bodyM = Regex.Match(html,
            @"<div\s+class=""news-detail-con""\s+id=""detail_con"">([\s\S]*?)</div>",
            RegexOptions.IgnoreCase);
        var body = bodyM.Success ? bodyM.Groups[1].Value.Trim() : "";
        return new NewsDetail
        {
            Title = titleM.Success ? HtmlDecode(titleM.Groups[1].Value.Trim()) : "公告",
            HtmlBody = ResolveUrls(body),
        };
    }

    /// <summary>把正文中的相对/协议相对 URL 重写为完整 URL。</summary>
    private static string ResolveUrls(string html)
    {
        if (string.IsNullOrEmpty(html))
            return html;

        // 协议相对 src/href="//host/..." → src="https://host/..."
        html = Regex.Replace(html,
            @"(?<=(?:src|href))=""//",
            "=\"https://",
            RegexOptions.IgnoreCase);
        html = Regex.Replace(html,
            @"(?<=(?:src|href))='//",
            "='https://",
            RegexOptions.IgnoreCase);

        // 根相对 src/href="/..."（非 // 或 http）→ 补全域名
        html = Regex.Replace(html,
            @"(?<=(?:src|href))=""/(?!/)",
            "=\"https://huoying.qq.com/",
            RegexOptions.IgnoreCase);
        html = Regex.Replace(html,
            @"(?<=(?:src|href))='/",
            "='https://huoying.qq.com/",
            RegexOptions.IgnoreCase);

        return html;
    }

    private static string HtmlDecode(string s) => WebUtility.HtmlDecode(s);

    /// <summary>缓存数据（JSON 序列化）。</summary>
    private class NewsCache
    {
        public List<NewsItem>? Items { get; set; }
        public Dictionary<string, string>? Details { get; set; }
    }
}
