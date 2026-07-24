using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using HITAPEX.Services;
using SharpVectors.Dom.Css;

namespace HITAPEX.Helpers;

/// <summary>
/// Windows 系统托盘图标管理类，支持图标显示、右键菜单、气泡提示等功能。
/// 通过 Win32 API (Shell_NotifyIcon) 与系统托盘交互，
/// 实现最小化到托盘、托盘菜单操作等桌面应用常见行为。
/// 实现 IDisposable 接口以正确释放非托管资源。
/// </summary>
public class TrayIcon : IDisposable
{
    // ═══════════════════════════════════════════════════════════════
    // Win32 常量 - 窗口消息
    // ═══════════════════════════════════════════════════════════════

    /// <summary>自定义托盘图标消息 ID</summary>
    private const int WM_TRAYICON = 0x8001;
    /// <summary>菜单命令消息</summary>
    private const int WM_COMMAND = 0x0111;
    /// <summary>鼠标左键双击消息</summary>
    private const int WM_LBUTTONDBLCLK = 0x0203;
    /// <summary>鼠标右键弹起消息</summary>
    private const int WM_RBUTTONUP = 0x0205;
    /// <summary>鼠标左键弹起消息</summary>
    private const int WM_LBUTTONUP = 0x0202;

    // ═══════════════════════════════════════════════════════════════
    // Win32 常量 - Shell_NotifyIcon 参数
    // ═══════════════════════════════════════════════════════════════

    /// <summary>添加托盘图标</summary>
    private const int NIM_ADD = 0;
    /// <summary>修改托盘图标</summary>
    private const int NIM_MODIFY = 1;
    /// <summary>删除托盘图标</summary>
    private const int NIM_DELETE = 2;

    // ═══════════════════════════════════════════════════════════════
    // Win32 常量 - NOTIFYICONDATA 标志位
    // ═══════════════════════════════════════════════════════════════

    /// <summary>使用 uCallbackMessage 回调</summary>
    private const int NIF_MESSAGE = 1;
    /// <summary>使用 hIcon 图标</summary>
    private const int NIF_ICON = 2;
    /// <summary>使用 szTip 提示文本</summary>
    private const int NIF_TIP = 4;
    /// <summary>使用气泡通知</summary>
    private const int NIF_INFO = 0x10;

    // ═══════════════════════════════════════════════════════════════
    // Win32 常量 - 菜单
    // ═══════════════════════════════════════════════════════════════

    /// <summary>菜单项：普通文本</summary>
    private const uint MF_STRING = 0x00000000;
    /// <summary>菜单项：分隔线</summary>
    private const uint MF_SEPARATOR = 0x00000800;
    /// <summary>弹出菜单：底部对齐</summary>
    private const uint TPM_BOTTOMALIGN = 0x0020;
    /// <summary>弹出菜单：左对齐</summary>
    private const uint TPM_LEFTALIGN = 0x0000;

    // ═══════════════════════════════════════════════════════════════
    // 菜单命令 ID
    // ═══════════════════════════════════════════════════════════════

    /// <summary>"显示窗口"菜单项命令 ID</summary>
    private const uint CMD_SHOW = 1;
    /// <summary>"退出"菜单项命令 ID</summary>
    private const uint CMD_EXIT = 2;

    // ═══════════════════════════════════════════════════════════════
    // 私有字段
    // ═══════════════════════════════════════════════════════════════

    /// <summary>关联的 WPF 窗口，用于获取 HWND 句柄</summary>
    private readonly Window _window;

    /// <summary>窗口消息源，用于挂接 WndProc 钩子处理托盘消息</summary>
    private readonly HwndSource _hwndSource;

    /// <summary>托盘图标是否显示</summary>
    private bool _visible;

    /// <summary>托盘图标对象（Windows Forms Icon）</summary>
    private Icon _icon;

    /// <summary>鼠标悬停时的提示文本</summary>
    private string _tooltip = "";

    // ═══════════════════════════════════════════════════════════════
    // 公共事件
    // ═══════════════════════════════════════════════════════════════

    /// <summary>双击托盘图标或点击"显示窗口"菜单时触发</summary>
    public event Action? DoubleClick;

    /// <summary>点击"退出"菜单项时触发</summary>
    public event Action? ExitRequested;

    // ═══════════════════════════════════════════════════════════════
    // 公共属性
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 获取或设置托盘图标的可见性。
    /// 设置为 true 时添加图标到系统托盘，false 时移除
    /// </summary>
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

    // ═══════════════════════════════════════════════════════════════
    // 构造函数与析构
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 初始化托盘图标，获取默认图标并挂接窗口消息钩子
    /// </summary>
    /// <param name="window">关联的 WPF 主窗口</param>
    public TrayIcon(Window window)
    {
        _window = window;
        _icon = GetDefaultIcon();

        var helper = new WindowInteropHelper(window);
        var handle = helper.EnsureHandle();
        _hwndSource = HwndSource.FromHwnd(handle)!;
        _hwndSource.AddHook(WndProc);
    }

    /// <summary>
    /// 释放托盘图标资源，移除图标、取消消息钩子并释放图标句柄
    /// </summary>
    public void Dispose()
    {
        try { Visible = false; } catch { }
        try { _hwndSource.RemoveHook(WndProc); } catch { }
        _icon?.Dispose();
    }

    // ═══════════════════════════════════════════════════════════════
    // 公共方法 - 图标与提示
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 更换托盘图标。如果当前图标可见，会先删除再重新添加
    /// </summary>
    /// <param name="icon">新的图标对象</param>
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

    /// <summary>
    /// 设置鼠标悬停提示文本。如果当前图标可见，会刷新以应用新文本
    /// </summary>
    /// <param name="tooltip">提示文本</param>
    public void SetTooltip(string tooltip)
    {
        _tooltip = tooltip;
        if (_visible)
        {
            DeleteIcon();
            AddIcon();
        }
    }

    /// <summary>
    /// 在托盘图标上方显示气泡通知。
    /// 常用于提示更新、后台任务完成等场景
    /// </summary>
    /// <param name="title">气泡标题</param>
    /// <param name="text">气泡正文</param>
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

    // ═══════════════════════════════════════════════════════════════
    // 窗口消息处理
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// WPF 窗口消息钩子，拦截托盘相关的 Windows 消息。
    /// 处理左键单击/双击（显示窗口）和右键单击（弹出上下文菜单）
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="msg">消息 ID</param>
    /// <param name="wParam">消息参数</param>
    /// <param name="lParam">消息参数</param>
    /// <param name="handled">是否已处理该消息</param>
    /// <returns>消息处理结果</returns>
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

    // ═══════════════════════════════════════════════════════════════
    // 右键菜单
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 创建并显示原生 Win32 右键弹出菜单。
    /// 菜单项文本通过本地化服务获取，支持多语言
    /// </summary>
    private void ShowNativeContextMenu()
    {
        var helper = new WindowInteropHelper(_window);
        var hWnd = helper.EnsureHandle();

        var hMenu = CreatePopupMenu();
        AppendMenu(hMenu, MF_STRING, CMD_SHOW, LocalizationService.Instance["Tray.ShowWindow"]);
        AppendMenu(hMenu, MF_SEPARATOR, 0, null);
        AppendMenu(hMenu, MF_STRING, CMD_EXIT, LocalizationService.Instance["Tray.Exit"]);

        // 必须设置前台窗口，否则菜单无法正确接收点击和关闭
        SetForegroundWindow(hWnd);

        GetCursorPos(out var pt);
        TrackPopupMenu(hMenu, TPM_LEFTALIGN | TPM_BOTTOMALIGN, pt.X, pt.Y, 0, hWnd, IntPtr.Zero);

        DestroyMenu(hMenu);
    }

    // ═══════════════════════════════════════════════════════════════
    // 托盘图标操作
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 通过 Shell_NotifyIcon(NIM_ADD) 将图标添加到系统托盘
    /// </summary>
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

    /// <summary>
    /// 通过 Shell_NotifyIcon(NIM_DELETE) 从系统托盘移除图标
    /// </summary>
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

    // ═══════════════════════════════════════════════════════════════
    // 图标获取
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 获取默认托盘图标。优先级：
    /// 1. 发布后从 exe 提取图标
    /// 2. 开发时从 Assets/AppIcon.ico 加载
    /// 3. 使用系统默认 Application 图标作为兜底
    /// </summary>
    /// <returns>托盘图标对象</returns>
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

    // ═══════════════════════════════════════════════════════════════
    // Win32 API P/Invoke 声明
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Shell32 托盘图标操作 API（添加、修改、删除）
    /// </summary>
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    /// <summary>
    /// 创建空的弹出菜单
    /// </summary>
    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    /// <summary>
    /// 向弹出菜单追加菜单项或分隔线
    /// </summary>
    [DllImport("user32.dll")]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string? lpNewItem);

    /// <summary>
    /// 在屏幕指定位置显示弹出菜单
    /// </summary>
    [DllImport("user32.dll")]
    private static extern bool TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    /// <summary>
    /// 销毁菜单并释放资源
    /// </summary>
    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    /// <summary>
    /// 将指定窗口设置为前台窗口（菜单交互需要）
    /// </summary>
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>
    /// 获取鼠标光标的屏幕坐标
    /// </summary>
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    // ═══════════════════════════════════════════════════════════════
    // Win32 结构体定义
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Win32 POINT 结构，表示屏幕坐标
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        /// <summary>X 坐标</summary>
        public int X;
        /// <summary>Y 坐标</summary>
        public int Y;
    }

    /// <summary>
    /// Win32 NOTIFYICONDATA 结构，Shell_NotifyIcon 的核心数据载体。
    /// 包含图标句柄、回调消息、提示文本和气泡通知信息
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        /// <summary>结构体大小</summary>
        public uint cbSize;
        /// <summary>接收消息的窗口句柄</summary>
        public IntPtr hWnd;
        /// <summary>托盘图标唯一 ID</summary>
        public uint uID;
        /// <summary>有效标志位（决定哪些字段生效）</summary>
        public uint uFlags;
        /// <summary>回调消息 ID</summary>
        public uint uCallbackMessage;
        /// <summary>图标句柄</summary>
        public IntPtr hIcon;
        /// <summary>提示文本（最大 128 字符）</summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        /// <summary>图标状态</summary>
        public uint dwState;
        /// <summary>状态掩码</summary>
        public uint dwStateMask;
        /// <summary>气泡通知正文（最大 256 字符）</summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        /// <summary>超时或版本号</summary>
        public uint uTimeoutOrVersion;
        /// <summary>气泡通知标题（最大 64 字符）</summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        /// <summary>气泡通知图标标志</summary>
        public uint dwInfoFlags;
    }
}
