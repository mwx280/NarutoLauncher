namespace NarutoLauncher.Models;

/// <summary>
/// 区服信息（列表展示用）。
/// </summary>
public class ServerInfo
{
    public string Name { get; set; } = "";
    public string Region { get; set; } = "";
    public string Status { get; set; } = "";
    public int Id { get; set; }
}
