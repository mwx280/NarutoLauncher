using System.Windows;
using NarutoLauncher.Services;

namespace NarutoLauncher.Views;

public partial class NewsDetailWindow : HandyControl.Controls.Window
{
    public NewsDetailWindow(string title, string htmlBody)
    {
        InitializeComponent();
        TitleText.Text = title;

        // 根据当前主题决定正文配色（深色/浅色）
        var isDark = ThemeManager.IsDark;
        var bg = isDark ? "#1F1F1F" : "#FFFFFF";
        var fg = isDark ? "#E0E0E0" : "#333333";
        var borderColor = isDark ? "#3A3A3A" : "#DDDDDD";

        // 用完整 HTML 外壳包裹正文，确保中文编码与样式
        var fullHtml =
            "<!DOCTYPE html>\n" +
            "<html lang=\"zh-CN\">\n<head>\n<meta charset=\"UTF-8\"/>\n" +
            "<style>\n" +
            "  html,body{font-family:\"Microsoft YaHei\",\"SimHei\",sans-serif;" +
            "background-color:" + bg + "!important;color:" + fg + "!important;" +
            "font-size:14px;padding:28px 36px;line-height:1.9;zoom:160%;}\n" +
            "  p{margin:0.8em 0;}\n" +
            "  span,div,td,li,p{font-size:inherit !important;line-height:inherit !important;" +
            (isDark ? "color:inherit !important;" : "") + "}\n" +
            "  img{max-width:100%;height:auto;display:block;margin:12px auto;}\n" +
            "  a{color:" + (isDark ? "#6CB4FF" : "#0066CC") + ";}\n" +
            "  table,tr,td{border-color:" + borderColor + ";}\n" +
            "</style>\n" +
            "</head>\n<body>" + htmlBody + "</body></html>";
        Browser.NavigateToString(fullHtml);
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
