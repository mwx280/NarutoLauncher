using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NarutoLauncher.Views;

/// <summary>
/// 内嵌 GameHost 游戏窗口的 HwndHost。
/// 通过跨进程 SetParent 把 GameHost 的窗口作为子窗口嵌入本区域。
/// </summary>
public class GameHostView : HwndHost
{
    private nint _childHwnd;
    private nint _hostHwnd;
    private bool _attached;

    // ---- Win32 P/Invoke ----
    private const int GWL_STYLE = -16;
    private const long WS_CHILD = 0x40000000;
    private const long WS_VISIBLE = 0x10000000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetParent(nint hWndChild, nint hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(nint hWnd, int x, int y, int w, int h, bool repaint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowLong(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(nint hWnd);

    private const int SW_SHOW = 5;
    private const int SW_HIDE = 0;

    /// <summary>要嵌入的外部窗口句柄（由启动器设置）。</summary>
    public nint ChildWindowHandle
    {
        get => _childHwnd;
        set
        {
            _childHwnd = value;
            // 宿主占位窗口已就绪即可内嵌（AttachChild 幂等）
            if (_childHwnd != 0 && _hostHwnd != 0)
                AttachChild();
        }
    }

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        // 始终创建独立的占位窗口作为 HwndHost 宿主（不能把外部窗口直接当宿主，
        // 否则 SetParent 会变成对自身操作，导致子窗口停留在原位置）。
        _hostHwnd = CreateWindowEx(0, "static", "", (int)(WS_CHILD | WS_VISIBLE),
            0, 0, 1, 1, hwndParent.Handle, 0, IntPtr.Zero, IntPtr.Zero);
        if (_childHwnd != 0)
            AttachChild();
        return new HandleRef(this, _hostHwnd);
    }

    private void AttachChild()
    {
        if (_childHwnd == 0 || _hostHwnd == 0 || _attached)
            return;
        // 强制为子窗口 + 可见
        var style = GetWindowLong(_childHwnd, GWL_STYLE);
        style = new nint(style.ToInt64() | WS_CHILD | WS_VISIBLE);
        SetWindowLong(_childHwnd, GWL_STYLE, style);
        SetParent(_childHwnd, _hostHwnd);
        UpdateChildBounds();
        ShowWindow(_childHwnd, SW_SHOW);
        _attached = true;
    }

    protected override void OnWindowPositionChanged(Rect rcBoundingBox)
    {
        base.OnWindowPositionChanged(rcBoundingBox);
        UpdateChildBounds();
    }

    private void UpdateChildBounds()
    {
        if (_childHwnd == 0 || _hostHwnd == 0)
            return;
        // WPF 的 ActualWidth/Height 是 DIP，MoveWindow 需要物理像素
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        int pw = (int)Math.Round(ActualWidth * dpi.DpiScaleX);
        int ph = (int)Math.Round(ActualHeight * dpi.DpiScaleY);
        if (pw <= 0 || ph <= 0)
            return;
        MoveWindow(_childHwnd, 0, 0, pw, ph, true);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        // 不销毁外部窗口（属于 GameHost 进程），仅解除父子关系
        if (_childHwnd != 0 && IsWindow(_childHwnd))
        {
            SetParent(_childHwnd, IntPtr.Zero);
        }
        DestroyWindow(hwnd.Handle);
        _hostHwnd = 0;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowEx(int exStyle, string className, string windowName,
        int style, int x, int y, int width, int height, nint parent, nint menu,
        nint instance, nint param);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(nint hwnd);
}