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
            SeedDemoData();
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
            SeedDemoData();
        }
    }

    private void SeedDemoData()
    {
        if (Accounts.Count > 0) return;
        Accounts.Add(new Account { Id = 1, QQ = "3026661111", Name = "大号·鸣人", Server = "火影一区", Level = 168, Power = "3.2亿", LoggedIn = true, Running = true, Seed = 0 });
        Accounts.Add(new Account { Id = 2, QQ = "3026662222", Name = "佐助小号", Server = "火影二区", Level = 152, Power = "2.8亿", LoggedIn = true, Running = true, Seed = 1 });
        Accounts.Add(new Account { Id = 3, QQ = "3026663333", Name = "未登录新号", LoggedIn = false, Seed = 2 });
        Save();
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
        Accounts.Remove(acc);
        Save();
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
        public bool LoggedIn { get; set; }
        public bool ScanLogin { get; set; }
        public int Seed { get; set; }

        public static AccountDto FromAccount(Account a) => new()
        {
            Id = a.Id,
            QQ = a.QQ,
            Name = a.Name,
            Pwd = EncryptPassword(a.Password),
            Server = a.Server,
            Level = a.Level,
            Power = a.Power,
            LoggedIn = a.LoggedIn,
            ScanLogin = a.ScanLogin,
            Seed = a.Seed,
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
            LoggedIn = LoggedIn,
            ScanLogin = ScanLogin,
            Seed = Seed,
        };
    }
}
