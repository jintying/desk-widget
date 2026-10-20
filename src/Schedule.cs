using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DeskWidget
{
    public class TaskItem
    {
        public string Id;
        public string Name;
        public int Progress;
        public string Group;
        public bool Done;
    }

    public class Group
    {
        public string Title;
        public string Color;
        public string Kind;          // daily | task
        public List<TaskItem> Items = new List<TaskItem>();
    }

    /// <summary>
    /// 解析与回写 日程.md。
    ///
    /// 支持的文档格式（轻量约定）：
    ///   ## 三、每日任务      —— 每日勾选表格，行内用 ⬜ / ✅ 标记
    ///   ### L1 · 任务名      —— 长期任务
    ///   ### M1 · 任务名      —— 中期任务
    ///   （任务小节内出现 "进度" 的行，取其中的百分比，如 **25%** 或 25%）
    /// </summary>
    public static class Schedule
    {
        public static List<Group> Parse(string path, out string error)
        {
            error = null;
            var groups = new List<Group>();
            if (!File.Exists(path)) { error = "找不到文件：" + path; return groups; }

            string raw;
            try { raw = File.ReadAllText(path, Encoding.UTF8); }
            catch (Exception ex) { error = "读取失败：" + ex.Message; return groups; }

            var tasks = new List<TaskItem>();
            var rx = new Regex(@"(?ms)^###\s+([LDM]\d+)\s*·\s*(.+?)\s*$(.*?)(?=^###\s|^##\s|\z)");
            foreach (Match m in rx.Matches(raw))
            {
                string id = m.Groups[1].Value.Trim();
                string body = m.Groups[3].Value;
                int pct = 0;
                foreach (var line in body.Split('\n'))
                {
                    if (line.Contains("进度"))
                    {
                        var pm = Regex.Match(line, @"\*\*(\d+)%\*\*");
                        if (!pm.Success) pm = Regex.Match(line, @"(\d+)%");
                        if (pm.Success) int.TryParse(pm.Groups[1].Value, out pct);
                        break;
                    }
                }
                tasks.Add(new TaskItem
                {
                    Id = id,
                    Name = m.Groups[2].Value.Trim(),
                    Progress = pct,
                    Group = id.Substring(0, 1)
                });
            }

            var daily = new List<TaskItem>();
            var dm = Regex.Match(raw, @"(?ms)##\s+三、.*?(?=^##\s)");
            if (dm.Success)
            {
                foreach (Match x in Regex.Matches(dm.Value, @"(?m)^\|\s*\*\*(D\d)\*\*\s*\|\s*([^|]+)\|\s*(⬜|✅)"))
                {
                    daily.Add(new TaskItem
                    {
                        Id = x.Groups[1].Value,
                        Name = x.Groups[2].Value.Trim(),
                        Done = x.Groups[3].Value == "✅"
                    });
                }
            }

            groups.Add(new Group { Title = "每日清理", Color = "#3FBF7F", Kind = "daily", Items = daily });

            var longTerm = new Group { Title = "长期", Color = "#7C9CFF", Kind = "task" };
            var midTerm = new Group { Title = "中期", Color = "#E8B44A", Kind = "task" };
            foreach (var t in tasks)
            {
                if (t.Group == "L") longTerm.Items.Add(t);
                else if (t.Group == "M") midTerm.Items.Add(t);
            }
            groups.Add(longTerm);
            groups.Add(midTerm);
            return groups;
        }

        /// <summary>
        /// 把勾选状态写回 md 文件：把该行的 ⬜ / ✅ 改成目标标记。
        /// 返回 true 表示文件确实被修改。
        /// </summary>
        public static bool SetDailyDone(string path, string id, bool done)
        {
            try
            {
                if (!File.Exists(path)) return false;
                string raw = File.ReadAllText(path, Encoding.UTF8);
                string mark = done ? "✅" : "⬜";
                var rx = new Regex(@"(?m)(\|\s*\*\*" + Regex.Escape(id) + @"\*\*\s*\|[^|]*\|\s*)(⬜|✅)");
                string updated = rx.Replace(raw, "${1}" + mark, 1);
                if (updated == raw) return false;
                File.WriteAllText(path, updated, new UTF8Encoding(false));
                return true;
            }
            catch { return false; }
        }

        /// <summary>校验文档是否含有挂件能识别的结构，返回可读结论。</summary>
        public static string Validate(string path, out int taskCount, out int dailyCount)
        {
            taskCount = 0;
            dailyCount = 0;
            if (!File.Exists(path)) return "文件不存在";

            string raw;
            try { raw = File.ReadAllText(path, Encoding.UTF8); }
            catch (Exception ex) { return "读取失败：" + ex.Message; }

            taskCount = Regex.Matches(raw, @"(?m)^###\s+[LDM]\d+\s*·").Count;
            var dm = Regex.Match(raw, @"(?ms)##\s+三、.*?(?=^##\s)");
            if (dm.Success)
                dailyCount = Regex.Matches(dm.Value, @"(?m)^\|\s*\*\*D\d\*\*\s*\|").Count;

            if (taskCount == 0 && dailyCount == 0)
                return "未识别到任何任务结构，请参照 README 的格式说明";
            return string.Format("识别到 {0} 个长期/中期任务、{1} 个每日任务", taskCount, dailyCount);
        }

        /// <summary>生成一份示例文档，供"导入"时给用户参考。</summary>
        public static string SampleDocument()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 我的日程 · 待办清单");
            sb.AppendLine();
            sb.AppendLine("## 三、🟩 每日任务");
            sb.AppendLine();
            sb.AppendLine("| 编号 | 任务 | 今日 | 备注 |");
            sb.AppendLine("| :--- | :--- | :---: | :--- |");
            sb.AppendLine("| **D1** | 每日签到 | ⬜ | 1 分钟 |");
            sb.AppendLine("| **D2** | 阅读 30 分钟 | ⬜ | |");
            sb.AppendLine();
            sb.AppendLine("## 四、🟨 长期与中期任务");
            sb.AppendLine();
            sb.AppendLine("### L1 · 代码学习");
            sb.AppendLine();
            sb.AppendLine("| 字段 | 内容 |");
            sb.AppendLine("| :--- | :--- |");
            sb.AppendLine("| **进度** | **25%** |");
            sb.AppendLine();
            sb.AppendLine("### M1 · 小游戏制作");
            sb.AppendLine();
            sb.AppendLine("| 字段 | 内容 |");
            sb.AppendLine("| :--- | :--- |");
            sb.AppendLine("| **进度** | **10%** |");
            return sb.ToString();
        }
    }
}
