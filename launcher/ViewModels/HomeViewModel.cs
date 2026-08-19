using System.Collections.ObjectModel;
using NarutoLauncher.Models;
using NarutoLauncher.Services;

namespace NarutoLauncher.ViewModels;

public class HomeViewModel
{
    /// <summary>账号列表（来自全局服务，跨页面共享）。</summary>
    public ObservableCollection<Account> Accounts => App.CurrentApp.Accounts.Accounts;

    /// <summary>全局设置。</summary>
    public SettingsService Settings => App.CurrentApp.Settings;
}
