using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskWidget
{
    /// <summary>
    /// 设置页：浅蓝近白主题，简洁风。
    /// 改动实时预览，点"保存"写回配置文件并立即生效。
    /// </summary>
    public class SettingsWindow : Window
    {
        // ---- 浅蓝近白配色 ----
        static readonly Color C_Window = Color.FromRgb(0xF2, 0xF7, 0xFC);   // 窗口底：近白浅蓝
        static readonly Color C_Card = Color.FromRgb(0xFF, 0xFF, 0xFF);     // 卡片：纯白
        static readonly Color C_Text = Color.FromRgb(0x1B, 0x2A, 0x3A);     // 主文字：深墨蓝
        static readonly Color C_Sub = Color.FromRgb(0x5E, 0x72, 0x86);      // 次要文字
        static readonly Color C_Accent = Color.FromRgb(0x3B, 0x82, 0xF6);   // 强调：亮蓝
        static readonly Color C_Border = Color.FromRgb(0xD8, 0xE4, 0xF2);   // 描边
        static readonly Color C_Input = Color.FromRgb(0xFA, 0xFC, 0xFF);    // 输入框底

        static readonly Brush B_Window = new SolidColorBrush(C_Window);
        static readonly Brush B_Card = new SolidColorBrush(C_Card);
        static readonly Brush B_Text = new SolidColorBrush(C_Text);
        static readonly Brush B_Sub = new SolidColorBrush(C_Sub);
        static readonly Brush B_Accent = new SolidColorBrush(C_Accent);
        static readonly Brush B_Border = new SolidColorBrush(C_Border);
        static readonly Brush B_Input = new SolidColorBrush(C_Input);

        readonly AppConfig _cfg;
        readonly AppConfig _draft;
        readonly Action<AppConfig> _onApply;

        Slider _alpha;
        Slider _font;
        TextBox _hexBg;
        TextBox _hexAccent;
        TextBox _dataPath;
        CheckBox _autoStart;
        CheckBox _startMin;
        TextBlock _preview;
        TextBlock _validate;

        public SettingsWindow(AppConfig cfg, Action<AppConfig> onApply)
        {
            _cfg = cfg;
            _draft = cfg.Clone();
            _onApply = onApply;

            Title = "日程挂件 · 设置";
            Width = 540;
            Height = 660;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            ShowInTaskbar = true;
            Background = B_Window;

            var outer = new Grid { Margin = new Thickness(18) };
            outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            outer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 顶部标题
            var head = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            head.Children.Add(new TextBlock
            {
                Text = "设置",
                FontSize = 22,
                FontWeight = FontWeights.Light,
                Foreground = B_Text
            });
            head.Children.Add(new TextBlock
            {
                Text = "调整外观、行为与数据源，保存后立即生效",
                FontSize = 12,
                Foreground = B_Sub,
                Margin = new Thickness(0, 4, 0, 0)
            });
            Grid.SetRow(head, 0);
            outer.Children.Add(head);

            // 内容
            var stack = new StackPanel();
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = stack
            };
            Grid.SetRow(scroll, 1);
            outer.Children.Add(scroll);

            // ===== 外观卡片 =====
            var card1 = Card(stack, "外观");

            _alpha = SliderRow(card1, "背景不透明度", 0.3, 1.0, _draft.BgAlpha, v =>
            {
                _draft.BgAlpha = v;
                UpdatePreview();
            });

            _font = SliderRow(card1, "字号", 10, 20, _draft.FontSize, v =>
            {
                _draft.FontSize = v;
                UpdatePreview();
            });

            _hexBg = TextRow(card1, "背景色（HEX）", _draft.BgColor, v =>
            {
                if (IsHex(v)) { _draft.BgColor = v; UpdatePreview(); }
            });
            _hexAccent = TextRow(card1, "强调色（HEX）", _draft.Accent, v =>
            {
                if (IsHex(v)) { _draft.Accent = v; UpdatePreview(); }
            });

            // 预设色块
            var swatchRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 8, 0, 4)
            };
            swatchRow.Children.Add(new TextBlock
            {
                Text = "快速配色",
                Width = 108,
                FontSize = 12,
                Foreground = B_Sub,
                VerticalAlignment = VerticalAlignment.Center
            });
            foreach (var hex in new[] { "#F2F7FC", "#FFFFFF", "#E8F1FA", "#121A26", "#1B2A1E", "#2A1B2E" })
            {
                var btn = new Button
                {
                    Width = 28,
                    Height = 24,
                    Margin = new Thickness(0, 0, 6, 0),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
                    BorderBrush = B_Border,
                    BorderThickness = new Thickness(1),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = hex
                };
                string h = hex;
                btn.Click += (s, e) =>
                {
                    _draft.BgColor = h;
                    _hexBg.Text = h;
                    UpdatePreview();
                };
                swatchRow.Children.Add(btn);
            }
            card1.Children.Add(swatchRow);

            // 预览
            var previewBox = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BorderBrush = B_Border,
                Margin = new Thickness(0, 6, 0, 2),
                Padding = new Thickness(12, 10, 12, 10)
            };
            _preview = new TextBlock
            {
                Text = "预览   L1  AI Forge 工坊   25%",
                FontSize = 13
            };
            previewBox.Child = _preview;
            card1.Children.Add(previewBox);

            // ===== 行为卡片 =====
            var card2 = Card(stack, "行为");
            _autoStart = CheckRow(card2, "开机自动启动", _draft.AutoStart, v => _draft.AutoStart = v);
            _startMin = CheckRow(card2, "启动后直接收进通知区域", _draft.StartMinimized, v => _draft.StartMinimized = v);

            card2.Children.Add(new TextBlock
            {
                Text = "提示：点挂件右上角 ✕ 只是收起，不会退出。要彻底退出请右键任务栏右下角的托盘图标 → 退出。",
                FontSize = 11,
                Foreground = B_Sub,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            });

            // ===== 数据源卡片 =====
            var card3 = Card(stack, "数据源");

            var pathRow = new Grid { Margin = new Thickness(0, 2, 0, 4) };
            pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _dataPath = new TextBox
            {
                Text = _draft.DataPath,
                Padding = new Thickness(8, 5, 8, 5),
                Background = B_Input,
                Foreground = B_Text,
                BorderBrush = B_Border,
                BorderThickness = new Thickness(1),
                FontSize = 12
            };
            _dataPath.TextChanged += (s, e) =>
            {
                _draft.DataPath = _dataPath.Text;
                ValidatePath();
            };
            Grid.SetColumn(_dataPath, 0);
            pathRow.Children.Add(_dataPath);

            var browse = SmallButton("浏览…");
            browse.Margin = new Thickness(8, 0, 0, 0);
            browse.Click += (s, e) =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "选择日程 Markdown 文件",
                    Filter = "Markdown 文件 (*.md)|*.md|所有文件 (*.*)|*.*"
                };
                if (dlg.ShowDialog() == true) _dataPath.Text = dlg.FileName;
            };
            Grid.SetColumn(browse, 1);
            pathRow.Children.Add(browse);
            card3.Children.Add(pathRow);

            _validate = new TextBlock
            {
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 6)
            };
            card3.Children.Add(_validate);

            var sample = SmallButton("生成示例文档…");
            sample.HorizontalAlignment = HorizontalAlignment.Left;
            sample.Click += (s, e) =>
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "保存示例日程文档",
                    Filter = "Markdown 文件 (*.md)|*.md",
                    FileName = "日程示例.md"
                };
                if (dlg.ShowDialog() == true)
                {
                    File.WriteAllText(dlg.FileName, Schedule.SampleDocument(), new System.Text.UTF8Encoding(false));
                    MessageBox.Show("示例文档已生成：\n" + dlg.FileName, "完成");
                }
            };
            card3.Children.Add(sample);

            // ===== 底部按钮 =====
            var bar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };
            var cancel = SmallButton("取消");
            cancel.Width = 92;
            cancel.Margin = new Thickness(0, 0, 8, 0);
            cancel.Click += (s, e) => Close();

            var save = SmallButton("保存");
            save.Width = 92;
            save.IsDefault = true;
            save.Background = B_Accent;
            save.Foreground = Brushes.White;
            save.BorderBrush = B_Accent;
            save.Click += (s, e) =>
            {
                Startup.Set(_autoStart.IsChecked == true);
                _cfg.DataPath = _draft.DataPath;
                _cfg.Width = _draft.Width;
                _cfg.Height = _draft.Height;
                _cfg.Left = _draft.Left;
                _cfg.Top = _draft.Top;
                _cfg.BgAlpha = _draft.BgAlpha;
                _cfg.FontSize = _draft.FontSize;
                _cfg.BgColor = _draft.BgColor;
                _cfg.Accent = _draft.Accent;
                _cfg.AutoStart = _draft.AutoStart;
                _cfg.StartMinimized = _draft.StartMinimized;
                if (_onApply != null) _onApply(_cfg);
                Close();
            };
            bar.Children.Add(cancel);
            bar.Children.Add(save);
            Grid.SetRow(bar, 2);
            outer.Children.Add(bar);

            Content = outer;
            UpdatePreview();
            ValidatePath();
        }

        /// <summary>白色圆角卡片，带标题。返回卡片内容面板供继续添加控件。</summary>
        StackPanel Card(StackPanel parent, string title)
        {
            var inner = new StackPanel();
            inner.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = B_Accent,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var card = new Border
            {
                Background = B_Card,
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                BorderBrush = B_Border,
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 12),
                Child = inner
            };
            parent.Children.Add(card);
            return inner;
        }

        Button SmallButton(string text)
        {
            return new Button
            {
                Content = text,
                Padding = new Thickness(14, 6, 14, 6),
                FontSize = 12,
                Background = B_Card,
                Foreground = B_Text,
                BorderBrush = B_Border,
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
        }

        Slider SliderRow(StackPanel parent, string label, double min, double max, double value, Action<double> onChange)
        {
            var row = new Grid { Margin = new Thickness(0, 4, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(108) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            row.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 12,
                Foreground = B_Sub,
                VerticalAlignment = VerticalAlignment.Center
            });

            var sl = new Slider
            {
                Minimum = min,
                Maximum = max,
                Value = value,
                VerticalAlignment = VerticalAlignment.Center,
                IsSnapToTickEnabled = false
            };
            sl.ValueChanged += (s, e) => onChange(sl.Value);
            Grid.SetColumn(sl, 1);
            row.Children.Add(sl);
            parent.Children.Add(row);
            return sl;
        }

        TextBox TextRow(StackPanel parent, string label, string value, Action<string> onChange)
        {
            var row = new Grid { Margin = new Thickness(0, 4, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(108) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            row.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 12,
                Foreground = B_Sub,
                VerticalAlignment = VerticalAlignment.Center
            });

            var tb = new TextBox
            {
                Text = value,
                Padding = new Thickness(8, 5, 8, 5),
                Background = B_Input,
                Foreground = B_Text,
                BorderBrush = B_Border,
                BorderThickness = new Thickness(1),
                FontSize = 12
            };
            tb.TextChanged += (s, e) => onChange(tb.Text);
            Grid.SetColumn(tb, 1);
            row.Children.Add(tb);
            parent.Children.Add(row);
            return tb;
        }

        CheckBox CheckRow(StackPanel parent, string label, bool value, Action<bool> onChange)
        {
            var cb = new CheckBox
            {
                Content = label,
                IsChecked = value,
                Foreground = B_Text,
                FontSize = 12,
                Margin = new Thickness(0, 5, 0, 5)
            };
            cb.Checked += (s, e) => onChange(true);
            cb.Unchecked += (s, e) => onChange(false);
            parent.Children.Add(cb);
            return cb;
        }

        static bool IsHex(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (!s.StartsWith("#")) return false;
            if (s.Length != 7 && s.Length != 4) return false;
            try { ColorConverter.ConvertFromString(s); return true; } catch { return false; }
        }

        void UpdatePreview()
        {
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(_draft.BgColor);
                var c2 = (Color)ColorConverter.ConvertFromString(_draft.Accent);
                // 预览块：浅色背景时用深字，深色背景时用白字
                double lum = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B);
                _preview.Background = new SolidColorBrush(Color.FromArgb((byte)(_draft.BgAlpha * 255), c.R, c.G, c.B));
                _preview.Foreground = lum > 150 ? B_Text : Brushes.White;
                _preview.FontSize = _draft.FontSize;
                _preview.Text = "预览   L1  AI Forge 工坊   25%";
                if (c2 != C_Accent)
                {
                    // 强调色只影响挂件，预览里不覆盖文字色以保持可读
                }
            }
            catch { }
        }

        void ValidatePath()
        {
            int tasks, daily;
            string msg = Schedule.Validate(_draft.DataPath, out tasks, out daily);
            _validate.Text = msg;
            bool ok = tasks > 0 || daily > 0;
            _validate.Foreground = ok
                ? new SolidColorBrush(Color.FromRgb(0x1E, 0x8E, 0x5A))
                : new SolidColorBrush(Color.FromRgb(0xC0, 0x51, 0x3A));
        }
    }
}
