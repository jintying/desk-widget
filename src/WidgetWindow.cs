using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Interop;

namespace DeskWidget
{
    /// <summary>主挂件窗口：渲染日程、支持拖动/缩放、关闭转托盘。</summary>
    public class WidgetWindow : Window
    {
        public AppConfig Cfg;
        readonly Border _border;
        readonly StackPanel _panel;
        readonly TextBlock _sub;
        readonly ScrollViewer _scroll;
        DateTime _lastWrite = DateTime.MinValue;
        bool _collapsed = false;
        double _savedW, _savedH, _savedLeft, _savedTop;
        Border _pill;

        public event Action HideToTrayRequested;
        public event Action SettingsRequested;
        public event Action ExitRequested;
        public event Action ShowMeRequested;
        public bool TrayUnavailable = false;

        public WidgetWindow(AppConfig cfg)
        {
            Cfg = cfg;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = false;
            ShowInTaskbar = true;
            ShowActivated = false;
            Title = "日程挂件";
            ResizeMode = ResizeMode.CanResizeWithGrip;
            MinWidth = 260;
            MinHeight = 220;
            Width = cfg.Width;
            Height = cfg.Height;

            var wa = SystemParameters.WorkArea;
            Left = cfg.Left < 0 ? Math.Max(0, wa.Right - cfg.Width - 40) : cfg.Left;
            Top = cfg.Top;

            _border = new Border
            {
                CornerRadius = new CornerRadius(14),
                BorderThickness = new Thickness(1)
            };

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // --- 标题栏（整条可拖动）---
            var header = new Grid { Margin = new Thickness(14, 12, 14, 0), Background = Brushes.Transparent };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                {
                    try { DragMove(); } catch { }
                    SaveGeometry();
                }
            };

            var titleBox = new StackPanel();
            titleBox.Children.Add(new TextBlock
            {
                Text = "日程",
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                FontFamily = new FontFamily(cfg.FontFamily)
            });
            _sub = new TextBlock
            {
                FontSize = 10,
                Margin = new Thickness(0, 2, 0, 0),
                Foreground = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255))
            };
            titleBox.Children.Add(_sub);
            Grid.SetColumn(titleBox, 0);
            header.Children.Add(titleBox);

            // 右侧按钮：设置 / 收起
            var btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
            btns.Children.Add(MakeIconButton("\u2699", "设置", () =>
            {
                if (SettingsRequested != null) SettingsRequested();
            }));
            btns.Children.Add(MakeIconButton("\u2500", "收起到托盘", () =>
            {
                if (HideToTrayRequested != null) HideToTrayRequested();
            }));
            btns.Children.Add(MakeIconButton("\u2715", "关闭到托盘", () =>
            {
                if (HideToTrayRequested != null) HideToTrayRequested();
            }));
            Grid.SetColumn(btns, 1);
            header.Children.Add(btns);
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // --- 内容区 ---
            _scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(14, 8, 14, 14)
            };
            _panel = new StackPanel();
            _scroll.Content = _panel;
            Grid.SetRow(_scroll, 1);
            root.Children.Add(_scroll);

            _border.Child = root;
            Content = _border;

            ApplyTheme();

            Loaded += (s, e) => { Render(); };
            Closing += (s, e) =>
            {
                // 托盘图标不可用时不能"收起"，否则窗口会彻底失联。
                // 改为最小化到任务栏（任务栏有按钮，随时可恢复）。
                if (TrayUnavailable)
                {
                    // 托盘不可用：折叠成小胶囊，点一下就能展开（绝不失联）
                    e.Cancel = true;
                    CollapseToPill();
                    return;
                }

                // 否则收起到托盘（真正退出走托盘菜单）
                e.Cancel = true;
                if (HideToTrayRequested != null) HideToTrayRequested();
            };

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (s, e) =>
            {
                try
                {
                    if (File.Exists(Cfg.DataPath))
                    {
                        var mt = File.GetLastWriteTime(Cfg.DataPath);
                        if (mt != _lastWrite) { _lastWrite = mt; Render(); }
                    }
                }
                catch { }
            };
            timer.Start();

            // 监听来自新实例的"唤回"广播：再次双击 exe 时唤起本窗口
            SourceInitialized += (s, e) =>
            {
                try
                {
                    var src = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                    if (src == null) return;
                    uint showMsg = NativeMsg.RegisterWindowMessage("DeskWidget_ShowMe");
                    src.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
                    {
                        if (showMsg != 0 && msg == (int)showMsg)
                        {
                            if (ShowMeRequested != null) ShowMeRequested();
                            handled = true;
                        }
                        return IntPtr.Zero;
                    });
                }
                catch { }
            };
        }

        Button MakeIconButton(string glyph, string tip, Action onClick)
        {
            var b = new Button
            {
                Content = glyph,
                ToolTip = tip,
                Width = 22,
                Height = 22,
                Margin = new Thickness(4, 0, 0, 0),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Focusable = false
            };
            b.Click += (s, e) => onClick();
            return b;
        }

        /// <summary>折叠成小胶囊（不依赖系统托盘，点一下即可展开）。</summary>
        public void CollapseToPill()
        {
            if (_collapsed) return;
            _savedW = ActualWidth > 50 ? ActualWidth : Width;
            _savedH = ActualHeight > 50 ? ActualHeight : Height;
            _savedLeft = Left;
            _savedTop = Top;
            _collapsed = true;

            var icon = new TextBlock
            {
                Text = "\uE787",
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 18,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _pill = new Border
            {
                CornerRadius = new CornerRadius(26),
                Background = new SolidColorBrush(Color.FromArgb(235, 0x25, 0x63, 0xEB)),
                Width = 52,
                Height = 52,
                Child = icon,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "点击展开日程挂件"
            };
            _pill.MouseLeftButtonUp += (s, e) => ExpandFromPill();

            Content = _pill;
            Width = 52;
            Height = 52;
            Top = Math.Max(20, _savedTop);
            Left = Math.Max(20, _savedLeft);
        }

        /// <summary>从胶囊展开回完整挂件。</summary>
        public void ExpandFromPill()
        {
            if (!_collapsed) return;
            _collapsed = false;
            Content = _border;
            Width = _savedW > 100 ? _savedW : Cfg.Width;
            Height = _savedH > 100 ? _savedH : Cfg.Height;
            Left = _savedLeft;
            Top = _savedTop;
            _lastWrite = DateTime.MinValue;
            Render();
        }

        public bool IsCollapsed { get { return _collapsed; } }

        public void SaveGeometry()
        {
            Cfg.Left = Left;
            Cfg.Top = Top;
            Cfg.Width = ActualWidth > 50 ? ActualWidth : Width;
            Cfg.Height = ActualHeight > 50 ? ActualHeight : Height;
        }

        public void ApplyTheme()
        {
            var bg = (Color)ColorConverter.ConvertFromString(Cfg.BgColor);
            _border.Background = new SolidColorBrush(Color.FromArgb((byte)(Cfg.BgAlpha * 255), bg.R, bg.G, bg.B));
            _border.BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
            if (_panel != null) { _lastWrite = DateTime.MinValue; Render(); }
        }

        static Brush BrushOf(string hex, byte alpha)
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            return new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
        }

        FrameworkElement MakeBar(int pct, Brush fill)
        {
            var g = new Grid { Height = 6, Margin = new Thickness(0, 5, 0, 0) };
            g.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(Color.FromArgb(38, 255, 255, 255))
            });
            var fg = new Border
            {
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = fill,
                Width = Math.Max(1, (Cfg.Width - 60) * pct / 100.0)
            };
            g.Children.Add(fg);
            g.SizeChanged += (s, e) => { fg.Width = Math.Max(1, g.ActualWidth * pct / 100.0); };
            return g;
        }

        public void Render()
        {
            string err;
            var groups = Schedule.Parse(Cfg.DataPath, out err);
            _panel.Children.Clear();

            string fmt = Path.GetFileName(Cfg.DataPath);

            if (err != null)
            {
                _sub.Text = fmt + "  ·  读取异常";
                _panel.Children.Add(new TextBlock
                {
                    Text = "⚠ " + err,
                    Foreground = Brushes.OrangeRed,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11
                });
                return;
            }

            _sub.Text = DateTime.Now.ToString("MM-dd HH:mm") + "  ·  " + fmt;

            var ff = new FontFamily(Cfg.FontFamily);
            foreach (var g in groups)
            {
                if (g.Items.Count == 0) continue;
                var gb = BrushOf(g.Color, 255);
                _panel.Children.Add(new TextBlock
                {
                    Text = g.Title,
                    FontSize = 10.5,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 12, 0, 6),
                    Foreground = gb,
                    FontFamily = ff
                });

                foreach (var it in g.Items)
                {
                    if (g.Kind == "daily")
                    {
                        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
                        var cb = new CheckBox
                        {
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(0, 0, 8, 0),
                            IsChecked = it.Done
                        };
                        string id = it.Id;
                        var label = new TextBlock
                        {
                            Text = it.Id + "  " + it.Name,
                            FontSize = Cfg.FontSize,
                            FontFamily = ff,
                            VerticalAlignment = VerticalAlignment.Center,
                            Foreground = new SolidColorBrush(Color.FromArgb(it.Done ? (byte)120 : (byte)232, 255, 255, 255))
                        };
                        cb.Click += (s, e) =>
                        {
                            bool done = cb.IsChecked == true;
                            bool ok = Schedule.SetDailyDone(Cfg.DataPath, id, done);
                            label.Foreground = new SolidColorBrush(Color.FromArgb(done ? (byte)120 : (byte)232, 255, 255, 255));
                            // 写回后同步刷新，避免与文件内容不一致
                            if (ok) _lastWrite = DateTime.MinValue;
                        };
                        row.Children.Add(cb);
                        row.Children.Add(label);
                        _panel.Children.Add(row);
                    }
                    else
                    {
                        var box = new StackPanel { Margin = new Thickness(0, 5, 0, 8) };
                        var line = new Grid();
                        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                        var nm = new TextBlock
                        {
                            Text = it.Id + "  " + it.Name,
                            FontSize = Cfg.FontSize,
                            FontFamily = ff,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            Foreground = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255))
                        };
                        Grid.SetColumn(nm, 0);
                        line.Children.Add(nm);

                        var pv = new TextBlock
                        {
                            Text = it.Progress + "%",
                            FontSize = 11,
                            FontWeight = FontWeights.Bold,
                            FontFamily = ff,
                            Margin = new Thickness(8, 0, 0, 0),
                            Foreground = gb
                        };
                        Grid.SetColumn(pv, 1);
                        line.Children.Add(pv);

                        box.Children.Add(line);
                        box.Children.Add(MakeBar(it.Progress, gb));
                        _panel.Children.Add(box);
                    }
                }
            }
        }
    }
}

namespace DeskWidget
{
    internal static class NativeMsg
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        internal static extern uint RegisterWindowMessage(string lpString);
    }
}