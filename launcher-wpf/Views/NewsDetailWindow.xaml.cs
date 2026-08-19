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
            "<style>body{font-family:\"Microsoft YaHei\",sans-serif;color:#333;" +
            "padding:24px 32px;line-height:1.8;}img{max-width:100%;height:auto;}</style>\n" +
            "</head>\n<body>" + htmlBody + "</body></html>";
        Browser.NavigateToString(fullHtml);
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
