using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DeskWidget
{
    /// <summary>挂件配置。读写 widget-config.json，字段名与文件保持一致。</summary>
    public class AppConfig
    {
        public string DataPath = @"D:\Desktop\_笔记归档\日程.md";
        public double Width = 380;
        public double Height = 560;
        public double Left = -1;          // -1 = 自动贴右边
        public double Top = 120;
        public double BgAlpha = 0.82;     // 0.30 ~ 1.00
        public double FontSize = 13;
        public string BgColor = "#121A26";
        public string Accent = "#7C9CFF";
        public bool AutoStart = false;
        public bool StartMinimized = false;
        public string FontFamily = "Microsoft YaHei";
        public bool TrayHintShown = false;

        public static string DefaultPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "widget-config.json"); }
        }

        public static AppConfig Load(string path)
        {
            var c = new AppConfig();
            try
            {
                if (!File.Exists(path)) { c.Save(path); return c; }
                string t = File.ReadAllText(path, Encoding.UTF8);

                var m = Regex.Match(t, "\"dataPath\"\\s*:\\s*\"([^\"]*)\"");
                if (m.Success) c.DataPath = m.Groups[1].Value.Replace("\\\\", "\\");

                c.Width = Num(t, "width", c.Width);
                c.Height = Num(t, "height", c.Height);
                c.Left = Num(t, "left", c.Left);
                c.Top = Num(t, "top", c.Top);
                c.BgAlpha = Num(t, "bgAlpha", c.BgAlpha);
                c.FontSize = Num(t, "fontSize", c.FontSize);
                c.AutoStart = Bool(t, "autoStart", c.AutoStart);
                c.StartMinimized = Bool(t, "startMinimized", c.StartMinimized);
                c.TrayHintShown = Bool(t, "trayHintShown", c.TrayHintShown);

                var bg = Regex.Match(t, "\"bgColor\"\\s*:\\s*\"([^\"]*)\"");
                if (bg.Success) c.BgColor = bg.Groups[1].Value;
                var ac = Regex.Match(t, "\"accent\"\\s*:\\s*\"([^\"]*)\"");
                if (ac.Success) c.Accent = ac.Groups[1].Value;
                var fn = Regex.Match(t, "\"fontFamily\"\\s*:\\s*\"([^\"]*)\"");
                if (fn.Success) c.FontFamily = fn.Groups[1].Value;
            }
            catch { }
            return c;
        }

        public void Save(string path)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine("  \"dataPath\": \"" + Json(DataPath) + "\",");
                sb.AppendLine("  \"width\": " + Width + ",");
                sb.AppendLine("  \"height\": " + Height + ",");
                sb.AppendLine("  \"left\": " + Left + ",");
                sb.AppendLine("  \"top\": " + Top + ",");
                sb.AppendLine("  \"bgAlpha\": " + BgAlpha.ToString("0.00") + ",");
                sb.AppendLine("  \"fontSize\": " + FontSize + ",");
                sb.AppendLine("  \"bgColor\": \"" + BgColor + "\",");
                sb.AppendLine("  \"accent\": \"" + Accent + "\",");
                sb.AppendLine("  \"fontFamily\": \"" + FontFamily + "\",");
                sb.AppendLine("  \"autoStart\": " + (AutoStart ? "true" : "false") + ",");
                sb.AppendLine("  \"startMinimized\": " + (StartMinimized ? "true" : "false") + ",");
                sb.AppendLine("  \"trayHintShown\": " + (TrayHintShown ? "true" : "false"));
                sb.AppendLine("}");
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }

        static string Json(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        static double Num(string t, string key, double def)
        {
            var m = Regex.Match(t, "\"" + key + "\"\\s*:\\s*(-?[0-9.]+)");
            if (!m.Success) return def;
            double v;
            if (double.TryParse(m.Groups[1].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out v)) return v;
            return def;
        }

        static bool Bool(string t, string key, bool def)
        {
            var m = Regex.Match(t, "\"" + key + "\"\\s*:\\s*(true|false)");
            if (!m.Success) return def;
            return m.Groups[1].Value == "true";
        }

        public AppConfig Clone()
        {
            return (AppConfig)MemberwiseClone();
        }
    }
}
