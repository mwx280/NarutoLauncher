using System.Collections.ObjectModel;
using NarutoLauncher.Models;

namespace NarutoLauncher.ViewModels;

public class GamesViewModel
{
    public ObservableCollection<ServerInfo> Servers { get; } = new();

    public GamesViewModel()
    {
        // 演示区服数据（后续由服务器查询接口填充）
        Servers.Add(new ServerInfo { Name = "火影一区", Region = "华东一区", Status = "流畅", Id = 1 });
        Servers.Add(new ServerInfo { Name = "火影二区", Region = "华东二区", Status = "流畅", Id = 2 });
        Servers.Add(new ServerInfo { Name = "火影三区", Region = "华南一区", Status = "拥挤", Id = 3 });
        Servers.Add(new ServerInfo { Name = "火影四区", Region = "华南二区", Status = "维护中", Id = 4 });
        Servers.Add(new ServerInfo { Name = "火影五区", Region = "华北一区", Status = "流畅", Id = 5 });
    }

    /// <summary>运行中的账号数。</summary>
    public int RunningCount =>
        App.CurrentApp.Accounts.Accounts.Count(a => a.Running);
}
