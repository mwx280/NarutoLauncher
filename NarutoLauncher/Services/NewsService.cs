using System.Net;
using System.Net.Http;
using System.Text;
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
/// 官方公告服务：网络抓取并解析公告列表与详情（huoying.qq.com，GB2312 编码）。
/// </summary>
public class NewsService
{
    private const string ListUrl = "https://huoying.qq.com/server/website/";
    private const string BaseUrl = "https://huoying.qq.com";
    private const int MaxNews = 10;

    private readonly HttpClient _http;
    private readonly Encoding _encoding;

    /// <summary>当前公告列表。</summary>
    public List<NewsItem> Items { get; private set; } = new();

    public NewsService()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        _http.Timeout = TimeSpan.FromSeconds(15);
        // 官网公告页面为 GBK/GB2312 编码（代码页 936 覆盖完整）
        _encoding = Encoding.GetEncoding(936);
    }

    /// <summary>立即返回当前列表（仅内存，无网络）。</summary>
    public List<NewsItem> GetItems() => Items;

    /// <summary>拉取最新公告列表，保留最多 10 条。</summary>
    public async Task<List<NewsItem>> FetchListAsync()
    {
        var html = await FetchStringAsync(ListUrl);
        Items = ParseList(html).Take(MaxNews).ToList();
        return Items;
    }

    /// <summary>抓取公告详情（返回 HTML 正文）。</summary>
    public async Task<NewsDetail> FetchDetailAsync(string url)
    {
        var full = url.StartsWith("http") ? url : BaseUrl + url;
        var html = await FetchStringAsync(full);
        return ParseDetail(html);
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
}
