using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using SharpVectors.Dom.Css;

namespace HITAPEX.Helpers;

public class TrayIcon : IDisposable
{
    private const int WM_TRAYICON = 0x8001;
    private const int WM_COMMAND = 0x0111;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_LBUTTONUP = 0x0202;
    private const int NIM_ADD = 0;
    private const int NIM_DELETE = 2;
    private const int NIF_MESSAGE = 1;
    private const int NIF_ICON = 2;
    private const int NIF_TIP = 4;
    private const int NIF_INFO = 0x10;

    // Win32 menu flags
    private const uint MF_STRING = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint TPM_BOTTOMALIGN = 0x0020;
    private const uint TPM_LEFTALIGN = 0x0000;

    private const uint CMD_SHOW = 1;
    private const uint CMD_EXIT = 2;

    private readonly Window _window;
    private readonly HwndSource _hwndSource;
    private bool _visible;
    private Icon _icon;
    private string _tooltip = "";

    public event Action? DoubleClick;
    public event Action? ExitRequested;

    public bool Visible
    {
        get => _visible;
        set
        {
            _visible = value;
            if (value)
                AddIcon();
            else
                DeleteIcon();
        }
    }

    public TrayIcon(Window window)
    {
        _window = window;
        _icon = GetDefaultIcon();

        var helper = new WindowInteropHelper(window);
        var handle = helper.EnsureHandle();
        _hwndSource = HwndSource.FromHwnd(handle)!;
        _hwndSource.AddHook(WndProc);
    }

    public void SetIcon(Icon icon)
    {
        _icon?.Dispose();
        _icon = icon;
        if (_visible)
        {
            DeleteIcon();
            AddIcon();
        }
    }

    public void SetTooltip(string tooltip)
    {
        _tooltip = tooltip;
        if (_visible)
        {
            DeleteIcon();
            AddIcon();
        }
    }

    public void ShowBalloonTip(string title, string text)
    {
        if (!_visible) return;

        var helper = new WindowInteropHelper(_window);
        var handle = helper.EnsureHandle();

        var nid = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
            hWnd = handle,
            uID = 1,
            uFlags = NIF_INFO,
            szInfoTitle = title,
            szInfo = text,
            dwInfoFlags = 1,
            uTimeoutOrVersion = 3000
        };

        Shell_NotifyIcon(NIM_MODIFY, ref nid);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAYICON)
        {
            var lParamVal = lParam.ToInt32();
            if (lParamVal == WM_LBUTTONUP || lParamVal == WM_LBUTTONDBLCLK)
            {
                DoubleClick?.Invoke();
                handled = true;
            }
            else if (lParamVal == WM_RBUTTONUP)
            {
                ShowNativeContextMenu();
                handled = true;
            }
        }
        else if (msg == WM_COMMAND)
        {
            var cmdId = (uint)wParam.ToInt32();
            if (cmdId == CMD_SHOW)
                DoubleClick?.Invoke();
            else if (cmdId == CMD_EXIT)
                ExitRequested?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void ShowNativeContextMenu()
    {
        var helper = new WindowInteropHelper(_window);
        var hWnd = helper.EnsureHandle();

        var hMenu = CreatePopupMenu();
        AppendMenu(hMenu, MF_STRING, CMD_SHOW, "显示主窗口");
        AppendMenu(hMenu, MF_SEPARATOR, 0, null);
        AppendMenu(hMenu, MF_STRING, CMD_EXIT, "退出");

        // Must set foreground window so the menu can receive clicks and dismiss properly
        SetForegroundWindow(hWnd);

        GetCursorPos(out var pt);
        TrackPopupMenu(hMenu, TPM_LEFTALIGN | TPM_BOTTOMALIGN, pt.X, pt.Y, 0, hWnd, IntPtr.Zero);

        DestroyMenu(hMenu);
    }

    private void AddIcon()
    {
        var helper = new WindowInteropHelper(_window);
        var handle = helper.EnsureHandle();

        var nid = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
            hWnd = handle,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _icon?.Handle ?? IntPtr.Zero,
            szTip = _tooltip
        };

        Shell_NotifyIcon(NIM_ADD, ref nid);
    }

    private void DeleteIcon()
    {
        var helper = new WindowInteropHelper(_window);
        var handle = helper.EnsureHandle();

        var nid = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
            hWnd = handle,
            uID = 1
        };

        Shell_NotifyIcon(NIM_DELETE, ref nid);
    }

    private static Icon GetDefaultIcon()
    {
        try
        {
            // 发布后从 exe 提取图标
            var exePath = Environment.ProcessPath;
            if (exePath != null)
            {
                var icon = Icon.ExtractAssociatedIcon(exePath);
                if (icon != null && icon.Handle != IntPtr.Zero) return icon;
            }
        }
        catch
        {
        }

        try
        {
            // 开发时从 Assets 目录加载
            var assetsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "AppIcon.ico");
            if (System.IO.File.Exists(assetsPath))
                return new Icon(assetsPath);
        }
        catch
        {
        }

        return SystemIcons.Application;
    }

    public void Dispose()
    {
        try { Visible = false; } catch { }
        try { _hwndSource.RemoveHook(WndProc); } catch { }
        _icon?.Dispose();
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    private const int NIM_MODIFY = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
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
    }
}
