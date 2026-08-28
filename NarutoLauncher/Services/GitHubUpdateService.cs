using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NarutoLauncher.Services;

/// <summary>一次 GitHub Release 检查的结果（有版本大于本版且无需跳过时返回）。</summary>
public class GitHubUpdateResult
{
    public required string Version { get; init; }
    public required string Notes { get; init; }
    public required string ReleaseUrl { get; init; }
    public string? AssetUrl { get; init; }
}

/// <summary>
/// 开源更新检查服务：通过 GitHub Releases API 获取最新发布版本（无需服务器、无需签名/公钥）。
/// 请求 https://api.github.com/repos/&lt;owner&gt;/&lt;repo&gt;/releases/latest，比对语义化版本号；
/// 比本版新即返回结果，客户端据此弹窗并跳转到 GitHub 下载页。
/// </summary>
public class GitHubUpdateService
{
    // GitHub 仓库（owner/repo）——开源后请改成你的公开仓库地址。
    public const string Repo = "mwx280/NarutoLauncher";

    /// <summary>当前版本（发布新版本时同步修改，须与 GitHub tag 一致）。</summary>
    public static string CurrentVersion => "1.0.0";

    private readonly HttpClient _http;

    public GitHubUpdateService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        // GitHub API 要求非默认 UA 才能跳过部分限流；也便于排查。
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("NarutoLauncher/" + CurrentVersion);
        // 接受 JSON
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    /// <summary>
    /// 检查更新。返回 null 表示已是最新；返回 GitHubUpdateResult 表示有新版；抛异常表示检查失败。
    /// </summary>
    public async Task<GitHubUpdateResult?> CheckAsync()
    {
        using var resp = await _http.GetAsync(
            $"https://api.github.com/repos/{Repo}/releases/latest");
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        var tag = root.GetProperty("tag_name").GetString() ?? "";
        var tagVersion = ParseTagVersion(tag);
        if (tagVersion == null || !IsNewer(tagVersion, CurrentVersion))
            return null;

        var notes = root.TryGetProperty("body", out var n)
            ? n.GetString() ?? "" : "";
        var releaseUrl = root.TryGetProperty("html_url", out var h)
            ? h.GetString() ?? "" : "";
        var assetUrl = ExtractAssetUrl(root);

        return new GitHubUpdateResult
        {
            Version = tagVersion,
            Notes = notes,
            ReleaseUrl = releaseUrl,
            AssetUrl = assetUrl,
        };
    }

    /// <summary>从 release JSON 中提取第一个可安装的下载资产 URL（.exe / .zip / .msi）。</summary>
    private static string? ExtractAssetUrl(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) ||
            assets.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var a in assets.EnumerateArray())
        {
            var name = a.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "";
            if (!Regex.IsMatch(name, @"\.(exe|zip|msi)$", RegexOptions.IgnoreCase))
                continue;
            if (a.TryGetProperty("browser_download_url", out var u))
            {
                var url = u.GetString();
                if (!string.IsNullOrEmpty(url))
                    return url;
            }
        }
        return null;
    }

    /// <summary>把 GitHub tag（如 "v1.0.0" / "1.0.0"）解析为版本号，无法解析返回 null。</summary>
    private static string? ParseTagVersion(string tag)
    {
        var m = Regex.Match(tag, @"v?([0-9]+(?:\.[0-9]+){0,3})");
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>语义化版本比较：b 比 a 新返回 true（异常版本号一律视为不新）。</summary>
    private static bool IsNewer(string a, string b)
    {
        if (!Version.TryParse(a, out var va) || !Version.TryParse(b, out var vb))
            return false;
        return va > vb;
    }

    /// <summary>在系统浏览器中打开 GitHub 发布/下载链接。</summary>
    public static void OpenRelease(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;
        var target = url.StartsWith("http") ? url : $"https://github.com/{Repo}/releases";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target)
        {
            UseShellExecute = true,
        });
    }
}
