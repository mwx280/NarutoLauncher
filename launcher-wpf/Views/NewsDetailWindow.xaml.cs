using System.Windows;

namespace NarutoLauncher.Views;

public partial class NewsDetailWindow : HandyControl.Controls.Window
{
    public NewsDetailWindow(string title, string htmlBody)
    {
        InitializeComponent();
        TitleText.Text = title;
        // 用完整 HTML 外壳包裹正文，确保中文编码与样式
        var fullHtml =
            "<!DOCTYPE html>\n" +
            "<html lang=\"zh-CN\">\n<head>\n<meta charset=\"UTF-8\"/>\n" +
            "<style>\n" +
            "  html,body{font-family:\"Microsoft YaHei\",\"SimHei\",sans-serif;color:#333;" +
            "font-size:14px;padding:28px 36px;line-height:1.9;zoom:160%;}\n" +
            "  p{margin:0.8em 0;}\n" +
            "  span,div,td,li{font-size:inherit !important;line-height:inherit !important;}\n" +
            "  img{max-width:100%;height:auto;display:block;margin:12px auto;}\n" +
            "</style>\n" +
            "</head>\n<body>" + htmlBody + "</body></html>";
        Browser.NavigateToString(fullHtml);
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
