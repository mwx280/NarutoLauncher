using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace NarutoLauncher.Services;

/// <summary>
/// 写入账号 cookie 的区服信息（sServerID / sServerName），用于切换区服。
/// 加密格式与 CEF/Chromium v10 一致：DPAPI key + AES-GCM。
/// </summary>
public static class CookieWriter
{
    /// <summary>更新账号 userdata 的区服 cookie，返回是否成功。</summary>
    public static bool WriteServerInfo(string userdataDir, int serverId, string serverName)
    {
        try
        {
            var key = LoadKey(userdataDir);
            if (key is null)
                return false;

            var db = Path.Combine(userdataDir, "Cookies");
            if (!File.Exists(db))
                return false;

            var csb = new SqliteConnectionStringBuilder
            {
                DataSource = db,
                Mode = SqliteOpenMode.ReadWrite,
            };
            using var con = new SqliteConnection(csb.ToString());
            con.Open();

            var sid = Encoding.UTF8.GetBytes(serverId.ToString());
            var sname = Encoding.UTF8.GetBytes(EscapeEncode(serverName));
            Upsert(con, key, ".huoying.qq.com", "sServerID", sid);
            Upsert(con, key, ".huoying.qq.com", "sServerName", sname);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void Upsert(SqliteConnection con, byte[] key,
                               string hostKey, string name, byte[] plain)
    {
        var enc = EncryptV10(key, plain);
        using var cmd = con.CreateCommand();
        cmd.CommandText =
            "UPDATE cookies SET encrypted_value=$enc, value='' " +
            "WHERE host_key=$hk AND name=$name";
        cmd.Parameters.AddWithValue("$enc", enc);
        cmd.Parameters.AddWithValue("$hk", hostKey);
        cmd.Parameters.AddWithValue("$name", name);
        if (cmd.ExecuteNonQuery() == 0)
        {
            using var ins = con.CreateCommand();
            ins.CommandText =
                "INSERT INTO cookies (creation_utc, host_key, name, value, path, " +
                "expires_utc, is_secure, is_httponly, last_access_utc, has_expires, " +
                "is_persistent, priority, encrypted_value, samesite, source_scheme) " +
                "VALUES (0, $hk, $name, '', '/', 0, 1, 1, 0, 1, 1, 0, $enc, -1, 2)";
            ins.Parameters.AddWithValue("$hk", hostKey);
            ins.Parameters.AddWithValue("$name", name);
            ins.Parameters.AddWithValue("$enc", enc);
            ins.ExecuteNonQuery();
        }
    }

    /// <summary>Chromium v10 加密：'v10' + nonce(12) + AES-GCM 密文 + tag(16)。</summary>
    private static byte[] EncryptV10(byte[] key, byte[] plain)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var gcm = new AesGcm(key, 16);
        gcm.Encrypt(nonce, plain, cipher, tag, null);

        var result = new byte[3 + 12 + plain.Length + 16];
        result[0] = (byte)'v';
        result[1] = (byte)'1';
        result[2] = (byte)'0';
        Buffer.BlockCopy(nonce, 0, result, 3, 12);
        Buffer.BlockCopy(cipher, 0, result, 15, plain.Length);
        Buffer.BlockCopy(tag, 0, result, 15 + plain.Length, 16);
        return result;
    }

    /// <summary>区服名编码为游戏使用的 escape 格式（%uXXXX 中文 + %XX 单字节）。</summary>
    private static string EscapeEncode(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s)
        {
            if (c < 0x100)
                sb.Append('%').Append(((int)c).ToString("X2"));
            else
                sb.Append("%u").Append(((int)c).ToString("X4"));
        }
        return sb.ToString();
    }

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
}