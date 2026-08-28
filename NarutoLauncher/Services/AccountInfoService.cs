using System.Net.Http;
using System.Text.Json;
using NarutoLauncher.Models;

namespace NarutoLauncher.Services;

/// <summary>
/// 账号信息同步：从 userdata 登录 cookie 解析区服（sServerName/sServerID），
/// 调用官方 getRoleList 接口获取游戏角色名/等级（登录后即可，无需进游戏）。
/// </summary>
public class AccountInfoService
{
    private readonly HttpClient _http;

    public AccountInfoService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36");
        _http.DefaultRequestHeaders.Referrer = new Uri("https://huoying.qq.com/");
    }

    /// <summary>账号对应的 userdata 目录（扫码用扫码目录，账密用 GameHost/userdata/&lt;QQ&gt;）。</summary>
    public static string? GetUserDataDir(Account account)
    {
        if (!string.IsNullOrEmpty(account.ScanUserDataDir) && Directory.Exists(account.ScanUserDataDir))
            return account.ScanUserDataDir;
        var qq = Path.Combine(AppContext.BaseDirectory, "GameHost", "userdata", account.QQ);
        return Directory.Exists(qq) ? qq : null;
    }

    /// <summary>
    /// 同步区服与角色名到账号（幂等：已有值不覆盖）。返回是否成功同步到信息。
    /// </summary>
    public async Task<bool> RefreshAsync(Account account)
    {
        try
        {
            var dir = GetUserDataDir(account);
            var cookies = dir is null ? null : CookieParser.ReadAllCookies(dir);
            if (cookies is null)
                return false;

            // 区服：始终同步 cookie 的最新登录区（账号可能在多区，切区后跟随最新）
            var sidStr = cookies.GetValueOrDefault("sServerID");
            var sname = CookieParser.DecodeJsUnicode(cookies.GetValueOrDefault("sServerName") ?? "");
            if (int.TryParse(sidStr, out var sid) && sid > 0)
                account.ServerId = sid;
            if (!string.IsNullOrEmpty(sname))
                account.Server = sname;

            // 角色名/等级（官方 getRoleList，登录后即可拿；按最新区服匹配）
            if (cookies.TryGetValue("openid", out var openid) && !string.IsNullOrEmpty(openid) &&
                cookies.TryGetValue("access_token", out var token) && !string.IsNullOrEmpty(token))
            {
                var (name, level) = await FetchRoleAsync(openid, token, account.ServerId);
                if (!string.IsNullOrEmpty(name))
                {
                    account.Character = name;
                    if (level > 0)
                        account.Level = level;
                }
            }

            return !string.IsNullOrEmpty(account.Server) || !string.IsNullOrEmpty(account.Character);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>调用 getRoleList 获取角色名与等级（匹配区服 ID；无匹配取第一个）。</summary>
    private async Task<(string Name, int Level)> FetchRoleAsync(string openid, string accessToken, int serverId)
    {
        var url = "https://web.huoying.qq.com/getRoleList?openid=" +
                  Uri.EscapeDataString(openid) +
                  "&appid=102045649&access_token=" + Uri.EscapeDataString(accessToken);
        using var resp = await _http.GetAsync(url);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        if (!doc.RootElement.TryGetProperty("role_list", out var list))
            return ("", 0);

        string? fallbackName = null;
        var fallbackLevel = 0;
        foreach (var role in list.EnumerateArray())
        {
            var isvrid = role.TryGetProperty("isvrid", out var iv) ? iv.GetString() ?? "" : "";
            var rname = role.TryGetProperty("vRoleName", out var rn) ? rn.GetString() ?? "" : "";
            var rlevel = role.TryGetProperty("iRoleLevel", out var rl) && int.TryParse(rl.GetString(), out var lv) ? lv : 0;
            if (isvrid == serverId.ToString())
                return (rname, rlevel);
            if (string.IsNullOrEmpty(fallbackName) && !string.IsNullOrEmpty(rname))
            {
                fallbackName = rname;
                fallbackLevel = rlevel;
            }
        }
        return (fallbackName ?? "", fallbackLevel);
    }
}
