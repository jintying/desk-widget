using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;

namespace DeskWidget
{
    public static class Program
    {
        static WidgetWindow _w;
        static TrayIcon _tray;
        static AppConfig _cfg;
        static Mutex _mutex;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll")]
        static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);
        static uint _showMsg = 0;

        [STAThread]
        public static void Main()
        {
            // ---- 单实例：已运行则唤回已有窗口，而不是开第二个 ----
            bool isFirst;
            _mutex = new Mutex(true, "Global\\DeskWidget_SingleInstance_7f3a", out isFirst);
            if (!isFirst)
            {
                // 广播"显示"消息给已在运行的实例
                try
                {
                    uint msg = RegisterWindowMessage("DeskWidget_ShowMe");
                    PostMessage(HWND_BROADCAST, msg, IntPtr.Zero, IntPtr.Zero);
                }
                catch { }
                return;
            }

            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

            AppDomain.CurrentDomain.UnhandledException += (s, e) => Dump("Unhandled", e.ExceptionObject as Exception);
            app.DispatcherUnhandledException += (s, e) =>
            {
                Dump("Dispatcher", e.Exception);
                e.Handled = true;
            };

            try
            {
                _cfg = AppConfig.Load(AppConfig.DefaultPath);
                try
                {
                    bool reg = Startup.IsEnabled();
                    if (_cfg.AutoStart != reg) { _cfg.AutoStart = reg; _cfg.Save(AppConfig.DefaultPath); }
                }
                catch { }

                _w = new WidgetWindow(_cfg);
                _w.HideToTrayRequested += HideToTray;
                _w.SettingsRequested += OpenSettings;
                _w.ExitRequested += ExitApp;

                // 监听"唤回"广播
                _showMsg = RegisterWindowMessage("DeskWidget_ShowMe");
                _w.ShowMeRequested += ShowFromTray;

                bool noTray = false;
                foreach (var a in Environment.GetCommandLineArgs())
                    if (string.Equals(a, "--notray", StringComparison.OrdinalIgnoreCase)) noTray = true;

                if (noTray)
                {
                    _w.TrayUnavailable = true;
                    Dbg("以 --notray 启动：不创建托盘图标，收起时折叠为胶囊");
                    _tray = null;
                    if (!_cfg.StartMinimized) _w.Show();
                    // 允许 --notray 与 --settings 同时使用（截图/调试用）
                    foreach (var a in Environment.GetCommandLineArgs())
                    {
                        if (string.Equals(a, "--settings", StringComparison.OrdinalIgnoreCase))
                        {
                            app.Dispatcher.BeginInvoke(new Action(() => OpenSettings()));
                            break;
                        }
                    }
                    app.Run();
                    return;
                }

                _tray = new TrayIcon(_w,
                    () => ToggleVisible(),
                    () => OpenSettings(),
                    () => ExitApp());
                if (!_tray.Created)
                {
                    _w.TrayUnavailable = true;
                    try
                    {
                        MessageBox.Show(
                            "托盘图标创建失败。\n\n" +
                            "技术信息：" + (_tray.LastError ?? "未知") + "\n\n" +
                            "影响：窗口的 ✕ 按钮会变为「直接退出程序」，\n" +
                            "因为托盘不可用，收起后将无法再唤回。\n\n" +
                            "请在程序目录的 widget.log 中查看详细记录。",
                            "日程挂件",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                    catch { }
                }

                if (_cfg.StartMinimized)
                {
                    _tray.SetVisibleState(false);
                    _tray.ShowBalloon("日程挂件已在托盘", "双击托盘图标可显示挂件。");
                }
                else
                {
                    _w.Show();
                    _tray.SetVisibleState(true);
                }

                // 支持 --settings：启动后直接打开设置页
                bool openSettings = false;
                foreach (var a in Environment.GetCommandLineArgs())
                {
                    if (string.Equals(a, "--settings", StringComparison.OrdinalIgnoreCase)) openSettings = true;
                }
                if (openSettings)
                {
                    app.Dispatcher.BeginInvoke(new Action(() => OpenSettings()));
                }

                // 自检模式：验证托盘创建与隐藏行为后自动退出
                bool selfTest = false;
                foreach (var a in Environment.GetCommandLineArgs())
                    if (string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase)) selfTest = true;

                if (selfTest)
                {
                    var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                    t.Tick += (s2, e2) =>
                    {
                        t.Stop();
                        try
                        {
                            _w.Hide();
                            Dbg("自检：窗口已隐藏，托盘 Created=" + (_tray != null && _tray.Created));
                        }
                        catch (Exception ex) { Dump("SelfTest", ex); }
                        var t2 = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                        t2.Tick += (s3, e3) => { t2.Stop(); ExitApp(); };
                        t2.Start();
                    };
                    t.Start();
                }

                app.Run();
            }
            catch (Exception ex)
            {
                Dump("Startup", ex);
                MessageBox.Show("日程挂件启动失败：\n" + ex.Message, "日程挂件",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        static void ShowFromTray()
        {
            if (_w == null) return;
            if (!_w.IsVisible) _w.Show();
            _w.Activate();
            try { SetForegroundWindow(new WindowInteropHelper(_w).Handle); } catch { }
            if (_tray != null) _tray.SetVisibleState(true);
        }

        static void ToggleVisible()
        {
            if (_w.IsVisible)
            {
                _w.SaveGeometry();
                _w.Hide();
                _tray.SetVisibleState(false);
            }
            else
            {
                ShowFromTray();
            }
        }

        static void HideToTray()
        {
            _w.SaveGeometry();
            _w.Hide();
            _tray.SetVisibleState(false);
            _tray.ShowBalloon("日程挂件", "已收起到通知区域。若看不到图标，点任务栏右侧的 ^ 箭头查找。");

            // 首次收起时给出明确指引并提供一键跳转（只提示一次）
            if (!_cfg.TrayHintShown)
            {
                _cfg.TrayHintShown = true;
                try { _cfg.Save(AppConfig.DefaultPath); } catch { }

                var r = MessageBox.Show(
                    "挂件已收起到「通知区域」（系统托盘）。\n\n" +
                    "Windows 11 默认把新图标折叠在任务栏右侧的 ^ 箭头里，\n" +
                    "所以可能一时看不到。可以这样做：\n\n" +
                    "  1. 点任务栏的 ^，在里面找到本挂件图标（双击可显示）\n" +
                    "  2. 或点下面的「打开任务栏设置」，把本图标设为常显\n" +
                    "  3. 或直接把 exe 再运行一次，也会唤回窗口\n\n" +
                    "是否现在打开「任务栏设置」？",
                    "日程挂件已收起",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (r == MessageBoxResult.Yes)
                {
                    try { System.Diagnostics.Process.Start("ms-settings:taskbar"); } catch { }
                }
            }
        }

        static void OpenSettings()
        {
            var win = new SettingsWindow(_cfg, OnSettingsApplied)
            {
                Owner = _w.IsVisible ? _w : null
            };
            win.ShowDialog();
        }

        static void OnSettingsApplied(AppConfig cfg)
        {
            try
            {
                cfg.Save(AppConfig.DefaultPath);
                _w.Cfg = cfg;
                _w.Width = cfg.Width;
                _w.Height = cfg.Height;
                if (cfg.Left >= 0) _w.Left = cfg.Left;
                if (cfg.Top > 0) _w.Top = cfg.Top;
                _w.ApplyTheme();
                _w.Render();
                if (!_w.IsVisible) ShowFromTray();
            }
            catch (Exception ex) { Dump("ApplySettings", ex); }
        }

        static void ExitApp()
        {
            try
            {
                if (_w != null) _w.SaveGeometry();
                if (_cfg != null) _cfg.Save(AppConfig.DefaultPath);
                if (_tray != null) _tray.Dispose();
                if (_mutex != null) { try { _mutex.ReleaseMutex(); } catch { } }
            }
            catch { }
            Application.Current.Shutdown();
        }

        static void Dbg(string msg)
        {
            try
            {
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "widget.log"),
                    "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + Environment.NewLine,
                    System.Text.Encoding.UTF8);
            }
            catch { }
        }

        static void Dump(string tag, Exception ex)
        {
            try
            {
                string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "widget-error.log");
                File.AppendAllText(p,
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + tag + ": " +
                    (ex == null ? "(null)" : ex.ToString()) + Environment.NewLine,
                    System.Text.Encoding.UTF8);
            }
            catch { }
        }
    }
}
