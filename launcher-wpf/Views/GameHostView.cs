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
            if (_attached && _childHwnd != 0 && Handle != 0)
                AttachChild();
        }
    }

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        // 占位窗口（HwndHost 要求一个宿主窗口）
        var hwnd = CreatePlaceholderWindow(hwndParent.Handle);
        if (_childHwnd != 0)
            AttachChild();
        return new HandleRef(this, hwnd);
    }

    private nint CreatePlaceholderWindow(nint parent)
    {
        // 用已有的子类化窗口：此处直接返回一个简单的占位窗口
        // 但更简单的方式：把 child SetParent 到 parent。
        // HwndHost 需要一个有效 HWND；我们用 child 本身（若已提供），否则建一个隐藏窗口。
        if (_childHwnd != 0 && IsWindow(_childHwnd))
            return _childHwnd;

        // 临时占位：直接创建一个普通窗口类
        var hwnd = CreateWindowEx(0, "static", "", WS_CHILD | WS_VISIBLE,
            0, 0, 100, 100, parent, 0, IntPtr.Zero, IntPtr.Zero);
        return hwnd;
    }

    private void AttachChild()
    {
        if (_childHwnd == 0 || Handle == 0)
            return;
        // 强制为子窗口 + 可见
        var style = GetWindowLong(_childHwnd, GWL_STYLE);
        style = new nint(style.ToInt64() | WS_CHILD | WS_VISIBLE);
        SetWindowLong(_childHwnd, GWL_STYLE, style);
        SetParent(_childHwnd, Handle);
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
        if (_childHwnd != 0 && Handle != 0)
        {
            MoveWindow(_childHwnd, 0, 0, Math.Max(1, (int)ActualWidth),
                       Math.Max(1, (int)ActualHeight), true);
        }
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        // 不销毁外部窗口（属于 GameHost 进程），仅解除父子关系
        if (_childHwnd != 0 && IsWindow(_childHwnd))
        {
            SetParent(_childHwnd, IntPtr.Zero);
        }
        DestroyWindow(hwnd.Handle);
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowEx(int exStyle, string className, string windowName,
        long style, int x, int y, int width, int height, nint parent, nint menu,
        nint instance, nint param);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(nint hwnd);
}
