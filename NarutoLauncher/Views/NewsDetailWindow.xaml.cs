using System.Windows;
using System.Windows.Media;
using NarutoLauncher.Services;
using Wpf.Ui.Controls;

namespace NarutoLauncher.Views;

public partial class NewsDetailWindow : FluentWindow
{
    public NewsDetailWindow(string title, string htmlBody)
    {
        InitializeComponent();
        TitleText.Text = title;

        // 根据主题设置默认背景（避免加载前闪烁白屏），颜色取自 WPF-UI 主题资源
        // 注意：WebView2 的 DefaultBackgroundColor alpha 只能为 0 或 255，否则抛 E_INVALIDARG
        var isDark = ThemeManager.IsDark;
        var bgColor = ThemeBackgroundColor();
        Browser.DefaultBackgroundColor = System.Drawing.Color.FromArgb(
            255, bgColor.R, bgColor.G, bgColor.B);

        // 打开窗口即显示"加载中"，WebView2 渲染完成后再显示内容
        LoadingOverlay.Visibility = Visibility.Visible;

        Loaded += async (_, _) =>
        {
            await Browser.EnsureCoreWebView2Async();
            Browser.NavigationCompleted += OnNavigationCompleted;
            Browser.NavigateToString(BuildHtml(htmlBody, isDark, bgColor));
        };
    }

    /// <summary>读取 WPF-UI 当前主题的窗口背景色（不透明，避免 WebView2 alpha 限制）。</summary>
    private static Color ThemeBackgroundColor()
    {
        // 用不透明的窗口背景（SolidBackgroundFillColorBaseBrush），
        // 不能用 CardBackgroundFillColorDefaultBrush（深色下是半透明白，转出来成白底）。
        var brush = Application.Current.Resources["SolidBackgroundFillColorBaseBrush"] as SolidColorBrush;
        return brush?.Color ?? Color.FromRgb(0x20, 0x20, 0x20);
    }

    private void OnNavigationCompleted(object? sender,
        Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
    {
        // 渲染完成：隐藏加载中，显示内容（UI 线程）
        Dispatcher.Invoke(() =>
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            Browser.Visibility = Visibility.Visible;
        });
    }

    /// <summary>构建带主题样式与现代化滚动条的 HTML。</summary>
    private static string BuildHtml(string htmlBody, bool isDark, Color bgColor)
    {
        var bg = "#" + bgColor.R.ToString("X2") + bgColor.G.ToString("X2") + bgColor.B.ToString("X2");
        var fg = isDark ? "#E0E0E0" : "#333333";
        var linkColor = isDark ? "#6CB4FF" : "#0066CC";
        var borderColor = isDark ? "#3A3A3A" : "#DDDDDD";
        var thumbColor = isDark ? "#4A4A4A" : "#C0C0C0";
        var thumbHover = isDark ? "#5A5A5A" : "#A0A0A0";

        return
            "<!DOCTYPE html>\n" +
            "<html lang=\"zh-CN\">\n<head>\n<meta charset=\"UTF-8\"/>\n" +
            "<style>\n" +
            "  html,body{font-family:\"Microsoft YaHei\",\"SimHei\",sans-serif;" +
            "background-color:" + bg + "!important;color:" + fg + "!important;" +
            "font-size:15px;padding:24px 32px;line-height:1.8;zoom:100%;}\n" +
            "  p{margin:0.8em 0;}\n" +
            "  span,div,td,li,p{font-size:inherit !important;line-height:inherit !important;" +
            (isDark ? "color:inherit !important;" : "") + "}\n" +
            "  img{max-width:100%;height:auto;display:block;margin:12px auto;}\n" +
            "  a{color:" + linkColor + ";}\n" +
            "  table,tr,td{border-color:" + borderColor + ";}\n" +
            // 现代化滚动条
            "  ::-webkit-scrollbar{width:8px;height:8px;}\n" +
            "  ::-webkit-scrollbar-track{background:transparent;}\n" +
            "  ::-webkit-scrollbar-thumb{background:" + thumbColor + ";border-radius:4px;}\n" +
            "  ::-webkit-scrollbar-thumb:hover{background:" + thumbHover + ";}\n" +
            "  ::-webkit-scrollbar-corner{background:transparent;}\n" +
            "</style>\n" +
            "</head>\n<body>" + htmlBody + "</body></html>";
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
