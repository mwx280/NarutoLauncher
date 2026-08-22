using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using WinForms = System.Windows.Forms;

namespace NarutoLauncher.Services;

/// <summary>
/// 自研系统托盘图标（参照 LenovoLegionToolkit 的 NotifyIcon 实现）：
/// 隐藏 NativeWindow 接收托盘回调消息，P/Invoke Shell_NotifyIcon 添加/更新/删除图标，
/// 右键弹出 WPF ContextMenu（可用 WPF-UI 主题样式）。不依赖第三方托盘库。
/// </summary>
public sealed class TrayIcon : WinForms.NativeWindow, IDisposable
{
    private const uint WM_APP = 0x8000;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_RBUTTONUP = 0x0205;
    private const uint WM_DESTROY = 0x0002;

    private const uint NIM_ADD = 0;
    private const uint NIM_MODIFY = 1;
    private const uint NIM_DELETE = 2;
    private const uint NIM_SETVERSION = 4;
    private const uint NOTIFYICON_VERSION_4 = 4;

    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;

    private static readonly uint TaskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");

    private readonly object _lock = new();
    private readonly uint _id = 1;
    private bool _added;
    private bool _visible;
    private Icon? _icon;
    private string? _text;

    /// <summary>右键托盘图标时显示的 WPF 菜单。</summary>
    public ContextMenu? ContextMenu { get; set; }

    /// <summary>左键单击托盘图标触发。</summary>
    public event EventHandler? OnClick;

    public TrayIcon()
    {
        UpdateIcon();
    }

    public bool Visible
    {
        set { _visible = value; UpdateIcon(); }
    }

    public Icon? Icon
    {
        set { _icon = value; UpdateIcon(); }
    }

    public string? Text
    {
        set { _text = value; UpdateIcon(); }
    }

    protected override void WndProc(ref WinForms.Message m)
    {
        switch ((uint)m.Msg)
        {
            case WM_APP + 1069:  // 托盘回调消息
                switch ((uint)m.LParam & 0xFFFF)
                {
                    case WM_LBUTTONUP:
                        HideContextMenu();
                        OnClick?.Invoke(this, EventArgs.Empty);
                        break;
                    case WM_RBUTTONUP:
                        HideContextMenu();
                        ShowContextMenu();
                        break;
                }
                break;
            case WM_DESTROY:
                _visible = false;
                UpdateIcon();
                break;
            default:
                // 资源管理器重启后重新注册托盘图标
                if (m.Msg == TaskbarCreatedMessage && _visible)
                {
                    _visible = true;
                    _added = false;
                    UpdateIcon();
                }
                base.WndProc(ref m);
                break;
        }
    }

    private void ShowContextMenu()
    {
        if (ContextMenu is null)
            return;
        ContextMenu.Placement = PlacementMode.Mouse;
        ContextMenu.PlacementRectangle = Rect.Empty;
        ContextMenu.PlacementTarget = null;
        ContextMenu.IsOpen = true;
        if (PresentationSource.FromVisual(ContextMenu) is HwndSource source &&
            source.Handle != IntPtr.Zero)
        {
            SetForegroundWindow(source.Handle);
        }
    }

    private void HideContextMenu()
    {
        if (ContextMenu is null || !ContextMenu.IsOpen)
            return;
        ContextMenu.IsOpen = false;
    }

    private void UpdateIcon()
    {
        lock (_lock)
        {
            var data = new NOTIFYICONDATAW
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
                hWnd = Handle,
                uID = _id,
                uFlags = NIF_MESSAGE | NIF_TIP,
                uCallbackMessage = WM_APP + 1069,
                szTip = " ",
            };

            if (_visible && Handle == IntPtr.Zero)
                CreateHandle(new WinForms.CreateParams());
            data.hWnd = Handle;

            if (_icon is not null)
            {
                data.uFlags |= NIF_ICON;
                data.hIcon = _icon.Handle;
            }
            if (_text is not null)
            {
                data.szTip = _text;
            }

            switch (_visible, _added)
            {
                case (true, false):
                    Shell_NotifyIcon(NIM_ADD, ref data);
                    data.uTimeoutOrVersion = NOTIFYICON_VERSION_4;
                    Shell_NotifyIcon(NIM_SETVERSION, ref data);
                    _added = true;
                    break;
                case (true, true):
                    Shell_NotifyIcon(NIM_MODIFY, ref data);
                    break;
                case (false, true):
                    Shell_NotifyIcon(NIM_DELETE, ref data);
                    _added = false;
                    break;
            }
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        HideContextMenu();
        _visible = false;
        UpdateIcon();
        _icon?.Dispose();
        _icon = null;
        _text = null;
        ContextMenu = null;
        ReleaseHandle();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATAW lpdata);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}