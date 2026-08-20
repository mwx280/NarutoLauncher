using System.Windows;
using System.Windows.Media.Imaging;
using NarutoLauncher.Models;
using NarutoLauncher.Services;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace NarutoLauncher.Views;

public partial class AddAccountWindow : FluentWindow
{
    /// <summary>添加成功后的账号（未成功为 null）。</summary>
    public Account? Result { get; private set; }

    /// <summary>编辑目标账号（非 null 时为编辑模式）。</summary>
    public Account? EditTarget { get; set; }

    private GameSession? _scanSession;
    private CancellationTokenSource? _pollCts;
    private string? _scannedQq;

    public AddAccountWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (EditTarget != null)
        {
            Title = "编辑账号";
            ConfirmBtn.Content = "保存";
            QqBox.Text = EditTarget.QQ;
            NameBox.Text = EditTarget.Name;
            PwdBox.Password = EditTarget.Password;
            ScanNameBox.Text = EditTarget.Name;
        }
    }

    // ---- 账号密码登录 ----
    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (MainTabs.SelectedIndex == 1)
        {
            AddScannedAccount();
            return;
        }

        var qq = QqBox.Text.Trim();
        if (string.IsNullOrEmpty(qq))
        {
            MessageBox.Show("请输入 QQ 号", "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var pwd = PwdBox.Password;

        if (EditTarget != null)
        {
            // 编辑模式：更新已有账号
            EditTarget.QQ = qq;
            EditTarget.Name = NameBox.Text.Trim();
            if (pwd.Length > 0)
                EditTarget.Password = pwd;
            App.CurrentApp.Accounts.Save();
            Result = EditTarget;
        }
        else
        {
            Result = App.CurrentApp.Accounts.AddAccount(
                qq, NameBox.Text.Trim(), pwd,
                scanLogin: false);
        }
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        StopScanLogin();
        base.OnClosed(e);
    }

    // ---- 扫码登录（GameHost 内嵌官网登录页） ----

    /// <summary>启动扫码登录：启动 GameHost 加载官网（弹出 QQ 二维码），内嵌到窗口。</summary>
    private async void OnRefreshQr(object sender, RoutedEventArgs e)
    {
        if (_scanSession != null && !_scanSession.Process.HasExited)
        {
            MessageBox.Show("扫码登录已在进行中，请等待或刷新", "提示");
            return;
        }

        QrPlaceholder.Visibility = Visibility.Visible;
        RefreshQrBtn.IsEnabled = false;
        _scannedQq = null;

        // 用主窗口句柄作为 GameHost 的 parent（与 GamesView 一致，跨进程 SetParent 可靠）
        var hostHwnd = App.CurrentApp.MainWindowHandle;

        var session = App.CurrentApp.Games.StartScanLogin(hostHwnd);
        if (session == null)
        {
            QrPlaceholder.Visibility = Visibility.Collapsed;
            RefreshQrBtn.IsEnabled = true;
            MessageBox.Show("启动扫码登录失败，请确认 GameHost 已就位", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        _scanSession = session;

        // 等待 GameHost 主窗口句柄（window_hwnd.txt）并内嵌
        await Task.Run(async () =>
        {
            nint childHwnd = 0;
            for (int i = 0; i < 100; i++)
            {
                childHwnd = session.ReadWindowHandle();
                if (childHwnd != 0) break;
                if (session.Process.HasExited) break;
                await Task.Delay(200);
            }
            if (childHwnd != 0)
                Dispatcher.Invoke(() =>
                {
                    QrHost.ChildWindowHandle = childHwnd;
                    QrPlaceholder.Visibility = Visibility.Collapsed;
                    // 内嵌完成即允许复制二维码（页面加载后按钮已可用）
                    CopyQrBtn.IsEnabled = true;
                });
        });

        // 轮询登录结果
        _pollCts?.Cancel();
        _pollCts = new CancellationTokenSource();
        _ = Task.Run(() => PollLoginResultAsync(session, _pollCts.Token));
    }

    /// <summary>轮询 userdata/login_result.txt，检测登录成功。</summary>
    private async Task PollLoginResultAsync(GameSession session, CancellationToken ct)
    {
        var resultFile = Path.Combine(session.UserdataDir, "login_result.txt");
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (File.Exists(resultFile))
                {
                    var qq = File.ReadAllText(resultFile).Trim();
                    if (qq.Length > 0)
                    {
                        _scannedQq = qq == "0" ? "" : qq;
                        Dispatcher.Invoke(() => OnScanSuccess());
                        return;
                    }
                }
                if (session.Process.HasExited)
                    return;
                await Task.Delay(1000, ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    private void OnScanSuccess()
    {
        QrPlaceholder.Visibility = Visibility.Collapsed;
        RefreshQrBtn.IsEnabled = true;
        ShowToast(string.IsNullOrEmpty(_scannedQq)
            ? "登录成功，点击「添加账号」完成添加"
            : $"登录成功（QQ：{_scannedQq}），点击「添加账号」完成添加");
    }

    /// <summary>截取二维码 CEF 窗口并复制到剪贴板。</summary>
    private void OnCopyQr(object sender, RoutedEventArgs e)
    {
        try
        {
            if (QrHost.Handle == 0)
            {
                ShowToast("二维码尚未加载完成");
                return;
            }
            // 用 PrintWindow 捕获 CEF 子窗口内容（即使被遮挡）
            var hwnd = QrHost.Handle;
            var rc = new System.Drawing.Rectangle(0, 0,
                Math.Max(1, (int)QrHost.ActualWidth),
                Math.Max(1, (int)QrHost.ActualHeight));
            // DPI 缩放
            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(QrHost);
            int pw = (int)Math.Round(QrHost.ActualWidth * dpi.DpiScaleX);
            int ph = (int)Math.Round(QrHost.ActualHeight * dpi.DpiScaleY);
            rc = new System.Drawing.Rectangle(0, 0, pw, ph);

            var bmp = new System.Drawing.Bitmap(rc.Width, rc.Height);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                var hdc = g.GetHdc();
                PrintWindow(hwnd, hdc, 2);  // PW_RENDERFULLCONTENT
                g.ReleaseHdc(hdc);
            }

            // 转 WPF BitmapSource 复制剪贴板
            using (var ms = new System.IO.MemoryStream())
            {
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Seek(0, System.IO.SeekOrigin.Begin);
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = ms;
                bi.EndInit();
                bi.Freeze();
                Clipboard.SetImage(bi);
            }
            bmp.Dispose();
            ShowToast("二维码已复制到剪贴板");
        }
        catch (Exception ex)
        {
            ShowToast("复制二维码失败：" + ex.Message);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool PrintWindow(nint hwnd, nint hdc, uint nFlags);

    private async void AddScannedAccount()
    {
        if (string.IsNullOrEmpty(_scannedQq))
        {
            ShowToast("请先使用手机 QQ 扫码登录");
            return;
        }

        var userdata = _scanSession?.UserdataDir ?? "";
        Result = App.CurrentApp.Accounts.AddAccount(
            _scannedQq, ScanNameBox.Text.Trim(), "",
            scanLogin: true);
        if (Result != null)
            Result.ScanUserDataDir = userdata;

        StopScanLogin();
        DialogResult = true;
    }

    /// <summary>右上角提示，3 秒后自动消失。</summary>
    private async void ShowToast(string message)
    {
        ToastText.Text = message;
        ToastBox.Visibility = Visibility.Visible;
        ToastBox.Opacity = 1;
        await Task.Delay(3000);
        // 若窗口已关闭或提示被新消息替换则不再隐藏
        if (IsLoaded && ToastText.Text == message)
        {
            ToastBox.Opacity = 0;
            ToastBox.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>停止扫码登录进程。</summary>
    private void StopScanLogin()
    {
        _pollCts?.Cancel();
        if (_scanSession != null && !_scanSession.Process.HasExited)
        {
            App.CurrentApp.Games.StopScanGracefully(_scanSession);
        }
        _scanSession = null;
        if (QrHost != null)
            QrHost.ChildWindowHandle = 0;
    }

}
