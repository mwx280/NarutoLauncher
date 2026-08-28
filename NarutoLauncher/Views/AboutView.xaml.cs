using System.Windows;
using System.Windows.Controls;
using NarutoLauncher.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace NarutoLauncher.Views;

/// <summary>关于页面：版本信息、GitHub 更新检查、开源仓库入口。</summary>
public partial class AboutView : UserControl
{
    private readonly GitHubUpdateService _update = new();

    public AboutView()
    {
        InitializeComponent();
    }

    /// <summary>检查 GitHub Releases 是否有新版本。</summary>
    private async void OnCheckUpdate(object sender, RoutedEventArgs e)
    {
        UpdateButton.IsEnabled = false;
        try
        {
            var result = await _update.CheckAsync();
            if (result == null)
            {
                await App.CurrentApp.DialogService.ShowSimpleDialogAsync(
                    new SimpleContentDialogCreateOptions
                    {
                        Title = "检查更新",
                        Content = "已是最新版本。",
                        CloseButtonText = "知道了",
                    });
            }
            else
            {
                await PromptUpdateAsync(result);
            }
        }
        catch
        {
            await App.CurrentApp.DialogService.ShowSimpleDialogAsync(
                new SimpleContentDialogCreateOptions
                {
                    Title = "检查更新",
                    Content = "无法连接 GitHub，请检查网络后重试。",
                    CloseButtonText = "知道了",
                });
        }
        finally
        {
            UpdateButton.IsEnabled = true;
        }
    }

    /// <summary>弹出更新提示对话框，确认后跳转到 GitHub 下载。</summary>
    private async Task PromptUpdateAsync(GitHubUpdateResult result)
    {
        var notes = string.IsNullOrWhiteSpace(result.Notes)
            ? "更新内容见 GitHub 发布页。"
            : result.Notes;
        var options = new SimpleContentDialogCreateOptions
        {
            Title = "发现新版本",
            Content = $"新版本：v{result.Version}\n\n{notes}",
            PrimaryButtonText = "去下载",
            CloseButtonText = "稍后再说",
            DefaultButton = ContentDialogButton.Primary,
        };
        var r = await App.CurrentApp.DialogService.ShowSimpleDialogAsync(options);
        if (r != ContentDialogResult.Primary)
            return;
        TryOpenRelease(result);
    }

    private static void TryOpenRelease(GitHubUpdateResult result)
    {
        try
        {
            // 优先直达安装包资产，否则打开发布页
            GitHubUpdateService.OpenRelease(result.AssetUrl ?? result.ReleaseUrl);
        }
        catch
        {
            GitHubUpdateService.OpenRelease(result.ReleaseUrl);
        }
    }

    /// <summary>打开开源仓库页面。</summary>
    private void OnOpenRepo(object sender, RoutedEventArgs e)
    {
        try
        {
            GitHubUpdateService.OpenRelease("https://github.com/" + GitHubUpdateService.Repo);
        }
        catch
        {
            // 打开失败静默
        }
    }

    /// <summary>打开游戏内核（CEFFlashGameHost）开源仓库页面。</summary>
    private void OnOpenGameKernel(object sender, RoutedEventArgs e)
    {
        try
        {
            GitHubUpdateService.OpenRelease("https://github.com/mwx280/CEFFlashGameHost");
        }
        catch
        {
            // 打开失败静默
        }
    }
}
