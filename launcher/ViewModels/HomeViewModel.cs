using System.Collections.ObjectModel;
using NarutoLauncher.Models;

namespace NarutoLauncher.ViewModels;

public class HomeViewModel
{
    public ObservableCollection<Account> Accounts { get; } = new();

    public HomeViewModel()
    {
        // 演示数据（后续由账号存储服务替换）
        Accounts.Add(new Account
        {
            Id = 1, QQ = "3026661111", Name = "大号·鸣人",
            Server = "火影一区", Level = 168, Power = "3.2亿",
            LoggedIn = true, Running = true, Seed = 0,
        });
        Accounts.Add(new Account
        {
            Id = 2, QQ = "3026662222", Name = "佐助小号",
            Server = "火影二区", Level = 152, Power = "2.8亿",
            LoggedIn = true, Running = true, Seed = 1,
        });
        Accounts.Add(new Account
        {
            Id = 3, QQ = "3026663333", Name = "未登录新号",
            LoggedIn = false, Running = false, Seed = 2,
        });
    }
}
