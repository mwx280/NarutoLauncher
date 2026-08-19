using System.Windows;
using NarutoLauncher.Services;

namespace NarutoLauncher.Views;

public partial class NewsDetailWindow : HandyControl.Controls.Window
{
    public NewsDetailWindow(string title, string htmlBody)
    {
        InitializeComponent();
        TitleText.Text = title;

        // 根据主题设置默认背景（避免加载前闪烁白屏）
        var isDark = ThemeManager.IsDark;
        Browser.DefaultBackgroundColor = isDark
            ? System.Drawing.Color.FromArgb(0x1F, 0x1F, 0x1F)
            : System.Drawing.Color.White;

        // 初始化期间隐藏 WebView2，避免白屏闪烁；导航完成后再显示
        Browser.Visibility = Visibility.Hidden;

        Loaded += async (_, _) =>
        {
            await Browser.EnsureCoreWebView2Async();
            Browser.NavigationCompleted += OnNavigationCompleted;
            Browser.NavigateToString(BuildHtml(htmlBody, isDark));
        };
    }

    private void OnNavigationCompleted(object? sender,
        Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
    {
        // HTML 渲染完成后显示（UI 线程）
        Dispatcher.Invoke(() => Browser.Visibility = Visibility.Visible);
    }

    /// <summary>构建带主题样式与现代化滚动条的 HTML。</summary>
    private static string BuildHtml(string htmlBody, bool isDark)
    {
        var bg = isDark ? "#1F1F1F" : "#FFFFFF";
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
