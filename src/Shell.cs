using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace DeskWidget
{
    /// <summary>开机自启动：写 HKCU\...\Run，不需要管理员权限。</summary>
    public static class Startup
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "DeskWidget";

        public static string ExePath
        {
            get
            {
                var p = System.Reflection.Assembly.GetExecutingAssembly().Location;
                return string.IsNullOrEmpty(p)
                    ? System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName
                    : p;
            }
        }

        public static bool IsEnabled()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (k == null) return false;
                    return !string.IsNullOrEmpty(k.GetValue(ValueName) as string);
                }
            }
            catch { return false; }
        }

        public static bool Set(bool enabled)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return false;
                    if (enabled) k.SetValue(ValueName, "\"" + ExePath + "\"");
                    else k.DeleteValue(ValueName, false);
                }
                return true;
            }
            catch { return false; }
        }
    }

    /// <summary>
    /// 托盘图标：直接调用 Shell_NotifyIcon，绕开 WinForms NotifyIcon 的静默失败。
    /// 每个 API 调用都检查返回值并写入 widget.log，便于定位问题。
    /// </summary>
    public sealed class TrayIcon : IDisposable
    {
        const int NIM_ADD = 0x00000000;
        const int NIM_MODIFY = 0x00000001;
        const int NIM_DELETE = 0x00000002;
        const int NIM_SETVERSION = 0x00000004;

        const int NIF_MESSAGE = 0x00000001;
        const int NIF_ICON = 0x00000002;
        const int NIF_TIP = 0x00000004;
        const int NIF_INFO = 0x00000010;
        const int NIF_SHOWTIP = 0x00000080;

        const int NOTIFYICON_VERSION_4 = 4;
        const int WM_TRAYICON = 0x8000 + 1;   // WM_APP + 1

        const int WM_LBUTTONUP = 0x0202;
        const int WM_LBUTTONDBLCLK = 0x0203;
        const int WM_RBUTTONUP = 0x0205;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
            public uint uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool Shell_NotifyIcon(int message, ref NOTIFYICONDATA data);

        [DllImport("user32.dll")]
        static extern IntPtr LoadImage(IntPtr hinst, string name, uint type, int cx, int cy, uint load);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetModuleHandle(string name);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT p);

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X; public int Y; }

        sealed class TrayHost : System.Windows.Forms.NativeWindow
        {
            public TrayIcon Owner;
            public void EnsureHandle()
            {
                if (Handle != IntPtr.Zero) return;
                var cp = new System.Windows.Forms.CreateParams
                {
                    Caption = "DeskWidgetTrayHost",
                    X = -32000, Y = -32000,
                    Width = 1, Height = 1
                };
                cp.Style = 0x800000;                      // WS_POPUP
                cp.ExStyle = 0x00000080 | 0x08000000;     // WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE
                CreateHandle(cp);
            }
            protected override void WndProc(ref System.Windows.Forms.Message m)
            {
                if (Owner != null && m.Msg == Owner.WmTray) { Owner.OnTrayMessage(m.LParam); return; }
                base.WndProc(ref m);
            }
        }

        readonly TrayHost _host = new TrayHost();
        readonly IntPtr _hwnd;
        readonly uint _id = 1;
        readonly IntPtr _hIcon;
        readonly System.Windows.Forms.ContextMenuStrip _menu;
        readonly System.Windows.Forms.ToolStripMenuItem _toggleItem;
        HwndSource _source;

        public int WmTray { get { return WM_TRAYICON; } }
        public void OnTrayMessage(IntPtr lParam)
        {
            int mouse = (int)(lParam.ToInt64() & 0xFFFF);
            if (mouse == WM_LBUTTONUP || mouse == WM_LBUTTONDBLCLK)
            {
                if (ToggleVisible != null) ToggleVisible();
            }
            else if (mouse == WM_RBUTTONUP)
            {
                ShowMenu();
            }
        }

        public bool Created { get; private set; }
        public string LastError { get; private set; }

        public event Action ToggleVisible;
        public event Action OpenSettings;
        public event Action ExitApp;

        public TrayIcon(Window owner, Action toggle, Action settings, Action exit)
        {
            if (toggle != null) ToggleVisible += toggle;
            if (settings != null) OpenSettings += settings;
            if (exit != null) ExitApp += exit;

            _host.Owner = this;
            _host.EnsureHandle();
            _hwnd = _host.Handle;
            Log("托盘回调窗口 HWND=0x" + _hwnd.ToInt64().ToString("X"));

            // 从 exe 里取图标（build 时用 /win32icon 嵌入了 app.ico，取 16x16 最清晰）
            _hIcon = LoadAppIcon();

            // 右键菜单用 WinForms 的 ContextMenuStrip（弹出位置好控制）
            _menu = new System.Windows.Forms.ContextMenuStrip();
            _toggleItem = new System.Windows.Forms.ToolStripMenuItem("显示挂件");
            _toggleItem.Click += (s, e) => { if (ToggleVisible != null) ToggleVisible(); };
            _menu.Items.Add(_toggleItem);
            _menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            var st = new System.Windows.Forms.ToolStripMenuItem("设置…");
            st.Click += (s, e) => { if (OpenSettings != null) OpenSettings(); };
            _menu.Items.Add(st);
            _menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            var ex = new System.Windows.Forms.ToolStripMenuItem("退出");
            ex.Click += (s, e) => { if (ExitApp != null) ExitApp(); };
            _menu.Items.Add(ex);

            AddIcon();
        }

        static IntPtr LoadAppIcon()
        {
            try
            {
                // 优先按尺寸从 exe 资源里取（最可靠，尺寸正确）
                var h = LoadImage(GetModuleHandle(null), "#32512", 1 /*IMAGE_ICON*/, 16, 16, 0);
                if (h != IntPtr.Zero) { Log("托盘图标：从 exe 资源加载 16x16 成功"); return h; }

                // 回退到 app.ico
                string ico = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(ico))
                {
                    var i = new System.Drawing.Icon(ico, 16, 16);
                    Log("托盘图标：从 app.ico 加载 16x16 成功");
                    return i.Handle;
                }
            }
            catch (Exception ex) { Log("托盘图标加载失败: " + ex.Message); }

            var sys = System.Drawing.SystemIcons.Application.Handle;
            Log("托盘图标：回退到系统默认图标");
            return sys;
        }

        void AddIcon()
        {
            var d = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _hwnd,
                uID = _id,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _hIcon,
                szTip = "日程挂件",
                szInfo = "",
                szInfoTitle = "",
                uVersion = 0
            };

            bool ok = Shell_NotifyIcon(NIM_ADD, ref d);
            int err = Marshal.GetLastWin32Error();
            Created = ok;
            LastError = ok ? null : ("Shell_NotifyIcon(NIM_ADD) 失败，Win32 错误码 " + err);

            if (ok)
            {
                // 设为现代版本（弹窗与菜单行为更符合 Win10/11）
                d.uVersion = NOTIFYICON_VERSION_4;
                Shell_NotifyIcon(NIM_SETVERSION, ref d);
                Log("托盘图标已添加（NIM_ADD 成功）");
            }
            else
            {
                Log(LastError);
            }
        }

        void HookMessages()
        {
            try
            {
                _source = HwndSource.FromHwnd(_hwnd);
                if (_source == null) { Log("无法取得 HwndSource，托盘消息不可用"); return; }
                _source.AddHook(WndProc);
                Log("托盘消息钩子已安装");
            }
            catch (Exception ex) { Log("安装消息钩子失败: " + ex.Message); }
        }

        IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_TRAYICON)
            {
                // v4 消息：低 16 位是鼠标消息
                int mouse = (int)(lParam.ToInt64() & 0xFFFF);
                switch (mouse)
                {
                    case WM_LBUTTONUP:
                        if (ToggleVisible != null) ToggleVisible();
                        handled = true;
                        break;
                    case WM_LBUTTONDBLCLK:
                        if (ToggleVisible != null) ToggleVisible();
                        handled = true;
                        break;
                    case WM_RBUTTONUP:
                        ShowMenu();
                        handled = true;
                        break;
                }
            }
            return IntPtr.Zero;
        }

        void ShowMenu()
        {
            try
            {
                SetForegroundWindow(_hwnd);
                POINT p;
                GetCursorPos(out p);
                _menu.Show(new System.Drawing.Point(p.X, p.Y));
            }
            catch (Exception ex) { Log("弹出菜单失败: " + ex.Message); }
        }

        public void SetVisibleState(bool windowVisible)
        {
            try
            {
                if (_toggleItem != null) _toggleItem.Text = windowVisible ? "收起到托盘" : "显示挂件";
            }
            catch { }
        }

        public void ShowBalloon(string title, string text)
        {
            try
            {
                var d = new NOTIFYICONDATA
                {
                    cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                    hWnd = _hwnd,
                    uID = _id,
                    uFlags = NIF_INFO,
                    szInfo = text,
                    szInfoTitle = title,
                    dwInfoFlags = 0x1,   // NIIF_INFO
                    szTip = "日程挂件"
                };
                bool ok = Shell_NotifyIcon(NIM_MODIFY, ref d);
                if (!ok) Log("气泡提示失败，错误码 " + Marshal.GetLastWin32Error());
            }
            catch (Exception ex) { Log("气泡提示异常: " + ex.Message); }
        }

        static void Log(string msg)
        {
            try
            {
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "widget.log"),
                    "[" + DateTime.Now.ToString("HH:mm:ss") + "] [托盘] " + msg + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch { }
        }

        public void Dispose()
        {
            try
            {
                var d = new NOTIFYICONDATA
                {
                    cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                    hWnd = _hwnd,
                    uID = _id
                };
                Shell_NotifyIcon(NIM_DELETE, ref d);
                Log("托盘图标已移除");
            }
            catch { }
        }
    }
}
