using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace NarutoLauncher.Services;

/// <summary>
/// 账号登录 cookie 解析：从 GameHost userdata 的 Cookies 库解密出区服信息。
/// 原理：CEF/Chromium 87 把 cookie 用 AES-GCM(v10) 加密存入 profile 的 Cookies 库，
/// 加密 key 由 DPAPI 保护后存于 LocalPrefs.json / Local State 的 os_crypt.encrypted_key。
/// 全部使用系统 API（ProtectedData + AesGcm）解密，不依赖第三方解密库。
/// </summary>
public static class CookieParser
{
    /// <summary>解析出的区服信息。</summary>
    public sealed record ServerInfo(string Uin, string ServerId, string ServerName, bool HasLogin);

    /// <summary>读取账号 userdata 目录中的区服信息（无有效登录态返回 null）。</summary>
    public static ServerInfo? ReadServerInfo(string userdataDir)
    {
        try
        {
            var key = LoadKey(userdataDir);
            if (key is null)
                return null;
            var cookies = ReadCookies(userdataDir, key);
            if (cookies.Count == 0)
                return null;

            cookies.TryGetValue("uin", out var uin);
            cookies.TryGetValue("sServerID", out var sid);
            cookies.TryGetValue("sServerName", out var sname);

            // sServerName 含服务器名（如「公测856区 光刃那都」），只取区名部分
            var serverName = TrimZoneName(DecodeJsUnicode(sname ?? ""));
            // sServerID 缺失时回退到 tmpLastLoginInfo 的 zonelist
            if (string.IsNullOrEmpty(sid))
                sid = ReadZoneId(cookies.GetValueOrDefault("tmpLastLoginInfo"));

            var hasLogin = cookies.ContainsKey("skey") ||
                           cookies.ContainsKey("p_skey") ||
                           !string.IsNullOrEmpty(uin);
            return new ServerInfo(uin ?? "", sid ?? "", serverName, hasLogin);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>从 LocalPrefs.json / Local State 读取并解密 AES key（DPAPI）。</summary>
    private static byte[]? LoadKey(string userdataDir)
    {
        string? prefs = null;
        foreach (var name in new[] { "LocalPrefs.json", "Local State" })
        {
            var p = Path.Combine(userdataDir, name);
            if (File.Exists(p))
            {
                prefs = p;
                break;
            }
        }
        if (prefs is null)
            return null;

        using var doc = JsonDocument.Parse(File.ReadAllText(prefs));
        if (!doc.RootElement.TryGetProperty("os_crypt", out var osCrypt) ||
            !osCrypt.TryGetProperty("encrypted_key", out var encKey))
            return null;
        var b64 = encKey.GetString();
        if (string.IsNullOrEmpty(b64))
            return null;

        var blob = Convert.FromBase64String(b64);
        if (blob.Length > 5 && Encoding.ASCII.GetString(blob, 0, 5) == "DPAPI")
            blob = blob[5..];
        return ProtectedData.Unprotect(blob, null, DataProtectionScope.CurrentUser);
    }

    /// <summary>读取并解密 Cookies 库中的所有 cookie 值（name -> value）。</summary>
    private static Dictionary<string, string> ReadCookies(string userdataDir, byte[] key)
    {
        var result = new Dictionary<string, string>();
        var db = Path.Combine(userdataDir, "Cookies");
        if (!File.Exists(db))
            return result;

        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = db,
            Mode = SqliteOpenMode.ReadOnly,
        };
        using var con = new SqliteConnection(csb.ToString());
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT name, encrypted_value FROM cookies";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(0);
            var enc = reader.IsDBNull(1) ? null : (byte[])reader[1];
            if (enc is null || enc.Length == 0)
                continue;
            try
            {
                result[name] = Encoding.UTF8.GetString(DecryptV10(enc, key));
            }
            catch
            {
                // 个别 cookie 非 v10 格式，忽略
            }
        }
        return result;
    }

    /// <summary>解密 Chromium v10 cookie：'v10' + nonce(12) + AES-GCM 密文 + tag(16)。</summary>
    private static byte[] DecryptV10(byte[] enc, byte[] key)
    {
        const int nonceLen = 12;
        const int tagLen = 16;
        if (enc.Length < 3 + nonceLen + tagLen + 1 ||
            enc[0] != (byte)'v' || enc[1] != (byte)'1' || enc[2] != (byte)'0')
            throw new InvalidOperationException("非 v10 加密格式");

        var nonce = enc.AsSpan(3, nonceLen).ToArray();
        var ciphertext = enc.AsSpan(3 + nonceLen, enc.Length - 3 - nonceLen - tagLen).ToArray();
        var tag = enc.AsSpan(enc.Length - tagLen, tagLen).ToArray();

        var plain = new byte[ciphertext.Length];
        using var gcm = new AesGcm(key, tagLen);
        gcm.Decrypt(nonce, ciphertext, tag, plain, null);
        return plain;
    }

    /// <summary>解码区服名：支持 %uXXXX（JS Unicode）与 %xx（GBK 字节百分号编码）。</summary>
    private static string DecodeJsUnicode(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;
        // %uXXXX（JS Unicode 转义）
        s = System.Text.RegularExpressions.Regex.Replace(
            s, "%u([0-9a-fA-F]{4})",
            m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
        // %xx 百分号编码（GBK 字节），如「公测856区 光刃那都」
        if (s.Contains('%'))
        {
            try
            {
                var bytes = new List<byte>();
                for (int i = 0; i < s.Length; i++)
                {
                    if (s[i] == '%' && i + 2 < s.Length &&
                        Uri.IsHexDigit(s[i + 1]) && Uri.IsHexDigit(s[i + 2]))
                    {
                        bytes.Add(Convert.ToByte(s.Substring(i + 1, 2), 16));
                        i += 2;
                    }
                    else
                    {
                        bytes.AddRange(Encoding.GetEncoding("GBK").GetBytes(s[i].ToString()));
                    }
                }
                return Encoding.GetEncoding("GBK").GetString(bytes.ToArray());
            }
            catch
            {
            }
        }
        return s;
    }

    /// <summary>截取区名（去掉服务器名）：支持「公测856区 光刃那都」「联盟1区 九尾妖狐」「801区极·沙时雨」等。</summary>
    private static string TrimZoneName(string name)
    {
        var m = System.Text.RegularExpressions.Regex.Match(name, @"^(.*?\d+区)");
        return m.Success ? m.Groups[1].Value : name;
    }

    /// <summary>从 tmpLastLoginInfo（URL 编码 JSON）提取上次登录区服 ID。</summary>
    private static string? ReadZoneId(string? tmpLastLoginInfo)
    {
        if (string.IsNullOrEmpty(tmpLastLoginInfo))
            return null;
        try
        {
            var obj = JsonDocument.Parse(Uri.UnescapeDataString(tmpLastLoginInfo)).RootElement;
            if (!obj.TryGetProperty("playerlist", out var list))
                return null;
            foreach (var p in list.EnumerateArray())
            {
                if (p.TryGetProperty("zonelist", out var zones))
                {
                    foreach (var z in zones.EnumerateArray())
                    {
                        if (z.TryGetInt32(out var zone))
                            return zone.ToString();
                    }
                }
            }
        }
        catch
        {
        }
        return null;
    }
}