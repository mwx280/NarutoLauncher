using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NarutoLauncher.Models;

namespace NarutoLauncher.Services;

/// <summary>
/// 账号数据服务：本地 JSON 持久化，密码用 DPAPI 加密后存储。
/// </summary>
public class AccountService
{
    public const string AccountsFileName = "accounts.json";

    public ObservableCollection<Account> Accounts { get; } = new();

    private string StorageDir { get; }
    private string StoragePath => Path.Combine(StorageDir, AccountsFileName);

    public AccountService()
    {
        StorageDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NarutoLauncher");
        Directory.CreateDirectory(StorageDir);
        Load();
    }

    /// <summary>从磁盘加载账号。</summary>
    private void Load()
    {
        if (!File.Exists(StoragePath))
        {
            // 新装/无账号文件：保持空列表，不填充演示账号
            return;
        }
        try
        {
            var json = File.ReadAllText(StoragePath);
            var dtos = JsonSerializer.Deserialize<List<AccountDto>>(json)
                       ?? new List<AccountDto>();
            foreach (var dto in dtos)
                Accounts.Add(dto.ToAccount());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载账号失败: {ex.Message}");
        }
    }

    /// <summary>保存到磁盘（密码用 DPAPI 加密）。</summary>
    public void Save()
    {
        var dtos = Accounts.Select(a => AccountDto.FromAccount(a)).ToList();
        var json = JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(StoragePath, json);
    }

    public Account AddAccount(string qq, string name, string password,
                              bool scanLogin, string server = "", int level = 0,
                              string power = "")
    {
        var acc = new Account
        {
            Id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            QQ = qq,
            Name = name,
            Password = password,
            ScanLogin = scanLogin,
            Server = server,
            Level = level,
            Power = power,
            Seed = Random.Shared.Next(6),
        };
        Accounts.Add(acc);
        Save();
        return acc;
    }

    public void UpdateAccount(long id, Action<Account> mutate)
    {
        var acc = Accounts.FirstOrDefault(a => a.Id == id);
        if (acc == null) return;
        mutate(acc);
        Save();
    }

    public void RemoveAccount(long id)
    {
        var acc = Accounts.FirstOrDefault(a => a.Id == id);
        if (acc == null) return;
        // 连带删除该账号的 userdata 目录（cookie/登录态），避免残留
        TryDeleteUserData(acc);
        Accounts.Remove(acc);
        Save();
    }

    /// <summary>删除账号对应的 userdata 目录（扫码目录或 QQ 目录），cookie 一并清除。</summary>
    private static void TryDeleteUserData(Account acc)
    {
        try
        {
            var userdata = !string.IsNullOrEmpty(acc.ScanUserDataDir)
                ? acc.ScanUserDataDir
                : Path.Combine(AppContext.BaseDirectory, "CEFFlashGameHost", "userdata", acc.QQ);
            if (Directory.Exists(userdata))
                Directory.Delete(userdata, true);
        }
        catch
        {
            // 目录被占用（游戏运行中）时跳过，避免删除失败导致崩溃
        }
    }

    // ---------- DPAPI 密码加密 ----------

    public static string? EncryptPassword(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return null;
        var bytes = Encoding.UTF8.GetBytes(plain);
        var enc = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(enc);
    }

    public static string? DecryptPassword(string? encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return null;
        try
        {
            var enc = Convert.FromBase64String(encrypted);
            var bytes = ProtectedData.Unprotect(enc, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }

    // ---------- 序列化 DTO（密码字段加密） ----------

    private class AccountDto
    {
        public long Id { get; set; }
        public string QQ { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Pwd { get; set; }
        public string Server { get; set; } = "";
        public int Level { get; set; }
        public string Power { get; set; } = "";
        public string Character { get; set; } = "";
        public bool LoggedIn { get; set; }
        public bool ScanLogin { get; set; }
        public bool IsSelected { get; set; }
        public bool IsMuted { get; set; }
        public int Seed { get; set; }
        public string Cookies { get; set; } = "";
        public string ScanUserDataDir { get; set; } = "";
        public AvatarType AvatarType { get; set; } = AvatarType.NameChar;

        public static AccountDto FromAccount(Account a) => new()
        {
            Id = a.Id,
            QQ = a.QQ,
            Name = a.Name,
            Pwd = EncryptPassword(a.Password),
            Server = a.Server,
            Level = a.Level,
            Power = a.Power,
            Character = a.Character,
            LoggedIn = a.LoggedIn,
            ScanLogin = a.ScanLogin,
            IsSelected = a.IsSelected,
            IsMuted = a.IsMuted,
            Seed = a.Seed,
            Cookies = EncryptPassword(a.Cookies) ?? "",
            ScanUserDataDir = a.ScanUserDataDir,
            AvatarType = a.AvatarType,
        };

        public Account ToAccount() => new()
        {
            Id = Id,
            QQ = QQ,
            Name = Name,
            Password = DecryptPassword(Pwd) ?? "",
            Server = Server,
            Level = Level,
            Power = Power,
            Character = Character,
            LoggedIn = LoggedIn,
            ScanLogin = ScanLogin,
            IsSelected = IsSelected,
            IsMuted = IsMuted,
            Seed = Seed,
            Cookies = DecryptPassword(Cookies) ?? "",
            ScanUserDataDir = ScanUserDataDir,
            AvatarType = AvatarType,
        };
    }
}
