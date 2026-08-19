#include "frameless_window.h"

#include <windowsx.h>

#include "app_log.h"

namespace {

// Win32 非客户区命中区域
constexpr long kHtCaption = 2;
constexpr long kHtLeft = 10;
constexpr long kHtRight = 11;
constexpr long kHtTop = 12;
constexpr long kHtTopLeft = 13;
constexpr long kHtTopRight = 14;
constexpr long kHtBottom = 15;
constexpr long kHtBottomLeft = 16;
constexpr long kHtBottomRight = 17;

constexpr int kResizeBorder = 6;

}  // namespace

FramelessWindow::FramelessWindow() {}

FramelessWindow::~FramelessWindow() {
    Destroy();
}

bool FramelessWindow::Create(int width, int height) {
    // 无边框窗口类
    static const wchar_t kClassName[] = L"HuoYinFramelessWindow";
    WNDCLASSEX wc = {};
    wc.cbSize = sizeof(wc);
    wc.lpfnWndProc = &FramelessWindow::WndProc;
    wc.hInstance = GetModuleHandle(nullptr);
    wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
    wc.hbrBackground = reinterpret_cast<HBRUSH>(GetStockObject(BLACK_BRUSH));
    wc.lpszClassName = kClassName;
    BOOL reg_ok = RegisterClassEx(&wc);
    DWORD reg_err = GetLastError();
    AppLog::Write("RegisterClassEx: %d (err=%lu, already=%d)",
                  reg_ok, reg_err, reg_err == ERROR_CLASS_ALREADY_EXISTS);
    if (!reg_ok && reg_err != ERROR_CLASS_ALREADY_EXISTS)
        return false;

    DWORD style = WS_POPUP;  // 无边框
    DWORD ex_style = WS_EX_APPWINDOW;

    hwnd_ = CreateWindowEx(
        ex_style, kClassName, L"火影忍者Online",
        style, CW_USEDEFAULT, CW_USEDEFAULT, width, height,
        nullptr, nullptr, GetModuleHandle(nullptr), this);
    DWORD cw_err = GetLastError();
    AppLog::Write("CreateWindowEx: hwnd=%p (err=%lu)", hwnd_, cw_err);
    if (!hwnd_)
        return false;

    ShowWindow(hwnd_, SW_SHOW);
    UpdateWindow(hwnd_);
    return true;
}

void FramelessWindow::Destroy() {
    if (hwnd_) {
        DestroyWindow(hwnd_);
        hwnd_ = nullptr;
    }
}

void FramelessWindow::ClientSize(int& w, int& h) const {
    RECT rc;
    GetClientRect(hwnd_, &rc);
    w = rc.right - rc.left;
    h = rc.bottom - rc.top;
}

void FramelessWindow::SetClientChild(HWND child) {
    child_ = child;
    if (child_)
        SetParent(child_, hwnd_);
    OnResize();
}

void FramelessWindow::OnResize() {
    if (!child_)
        return;
    RECT rc;
    GetClientRect(hwnd_, &rc);
    SetWindowPos(child_, nullptr, 0, 0, rc.right, rc.bottom,
                 SWP_NOZORDER);
}

void FramelessWindow::UpdateMaximizedState() {
    // 通过窗口位置/尺寸判断是否铺满工作区
    RECT rc;
    GetWindowRect(hwnd_, &rc);
    MONITORINFO mi = { sizeof(mi) };
    HMONITOR mon = MonitorFromWindow(hwnd_, MONITOR_DEFAULTTONEAREST);
    GetMonitorInfo(mon, &mi);
    maximized_ =
        (rc.left == mi.rcWork.left && rc.right == mi.rcWork.right &&
         rc.top == mi.rcWork.top && rc.bottom == mi.rcWork.bottom);
}

void FramelessWindow::ApplyFrameStyle(bool /*fullscreen*/) {
    // 无边框窗口无需切换样式；阴影边距逻辑由客户区处理。
}

void FramelessWindow::ToggleMaximize() {
    if (maximized_) {
        // 还原
        SetWindowPos(hwnd_, nullptr, normal_rect_.left, normal_rect_.top,
                     normal_rect_.right - normal_rect_.left,
                     normal_rect_.bottom - normal_rect_.top,
                     SWP_NOZORDER);
    } else {
        GetWindowRect(hwnd_, &normal_rect_);
        HMONITOR mon = MonitorFromWindow(hwnd_, MONITOR_DEFAULTTONEAREST);
        MONITORINFO mi = { sizeof(mi) };
        GetMonitorInfo(mon, &mi);
        RECT work = mi.rcWork;
        SetWindowPos(hwnd_, nullptr, work.left, work.top,
                     work.right - work.left, work.bottom - work.top,
                     SWP_NOZORDER);
    }
    UpdateMaximizedState();
    OnResize();
}

void FramelessWindow::ToggleFullscreen() {
    if (fullscreen_) {
        SetWindowPos(hwnd_, nullptr, normal_rect_.left, normal_rect_.top,
                     normal_rect_.right - normal_rect_.left,
                     normal_rect_.bottom - normal_rect_.top,
                     SWP_NOZORDER);
        fullscreen_ = false;
    } else {
        GetWindowRect(hwnd_, &normal_rect_);
        HMONITOR mon = MonitorFromWindow(hwnd_, MONITOR_DEFAULTTONEAREST);
        MONITORINFO mi = { sizeof(mi) };
        GetMonitorInfo(mon, &mi);
        RECT full = mi.rcMonitor;  // 全屏含任务栏
        SetWindowPos(hwnd_, nullptr, full.left, full.top,
                     full.right - full.left, full.bottom - full.top,
                     SWP_NOZORDER);
        fullscreen_ = true;
    }
    OnResize();
}

void FramelessWindow::Minimize() {
    ShowWindow(hwnd_, SW_MINIMIZE);
}

void FramelessWindow::Close() {
    PostMessage(hwnd_, WM_CLOSE, 0, 0);
}

LRESULT CALLBACK FramelessWindow::WndProc(HWND hwnd, UINT msg,
                                          WPARAM w, LPARAM l) {
    FramelessWindow* self = nullptr;
    if (msg == WM_NCCREATE) {
        auto* cs = reinterpret_cast<CREATESTRUCT*>(l);
        self = static_cast<FramelessWindow*>(cs->lpCreateParams);
        // 关键：WM_NCCREATE 在 CreateWindowEx 返回前同步发送，
        // 此时成员 hwnd_ 尚未赋值，先用真实句柄写入，后续消息才能用。
        self->hwnd_ = hwnd;
        SetWindowLongPtr(hwnd, GWLP_USERDATA,
                         reinterpret_cast<LONG_PTR>(self));
    } else {
        self = reinterpret_cast<FramelessWindow*>(
            GetWindowLongPtr(hwnd, GWLP_USERDATA));
    }
    if (self)
        return self->HandleMessage(msg, w, l);
    return DefWindowProc(hwnd, msg, w, l);
}

LRESULT FramelessWindow::HandleMessage(UINT msg, WPARAM w, LPARAM l) {
    switch (msg) {
        case WM_NCCREATE:
            AppLog::Write("WM_NCCREATE (hwnd=%p)", hwnd_);
            return DefWindowProc(hwnd_, msg, w, l);
        case WM_CREATE:
            AppLog::Write("WM_CREATE (hwnd=%p)", hwnd_);
            return 0;
        case WM_NCHITTEST: {
            // 全屏时不处理
            if (fullscreen_)
                break;
            POINT pt = { GET_X_LPARAM(l), GET_Y_LPARAM(l) };
            ScreenToClient(hwnd_, &pt);
            RECT rc;
            GetClientRect(hwnd_, &rc);
            const int b = kResizeBorder;
            bool left = pt.x < b;
            bool right = pt.x >= rc.right - b;
            bool top = pt.y < b;
            bool bottom = pt.y >= rc.bottom - b;

            if (maximized_) {
                // 最大化时禁止缩放，但标题栏仍可拖拽（拖动会自动还原）
                break;
            }

            // 优先级：先判四角，再判四边。
            if (left && top) return kHtTopLeft;
            if (right && top) return kHtTopRight;
            if (left && bottom) return kHtBottomLeft;
            if (right && bottom) return kHtBottomRight;
            if (left) return kHtLeft;
            if (right) return kHtRight;
            if (top) return kHtTop;
            if (bottom) return kHtBottom;

            // 标题栏区域（顶部 36px 内、非边缘缩放区）→ 返回 HTCAPTION 支持拖拽。
            // 排除右侧窗口控制按钮区域，保证按钮可点击。
            const int kTitleBarHeight = 36;
            const int kWinButtonsArea = 40 * 4 + 8;  // 4 个按钮 + 间距
            if (pt.y < kTitleBarHeight &&
                pt.x < rc.right - kWinButtonsArea) {
                return kHtCaption;
            }
            break;
        }
        case WM_SIZE:
            OnResize();
            UpdateMaximizedState();
            break;
        case WM_GETMINMAXINFO: {
            auto* mmi = reinterpret_cast<MINMAXINFO*>(l);
            mmi->ptMinTrackSize.x = 800;
            mmi->ptMinTrackSize.y = 560;
            return 0;
        }
        case WM_DESTROY:
            PostQuitMessage(0);
            break;
        case WM_CLOSE:
            // 隐藏到托盘语义由上层决定；此处直接关闭进程消息循环。
            DestroyWindow(hwnd_);
            return 0;
        default:
            break;
    }
    return DefWindowProc(hwnd_, msg, w, l);
}
