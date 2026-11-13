using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32;

namespace RoboIsland;

public record Evt(string App, string Kind, string Text);

public class Settings
{
    public string Lang { get; set; } = "ar";
    public bool RoboEnabled { get; set; } = true;
    public bool NotifyStart { get; set; } = true;
    public bool NotifyDone { get; set; } = true;
    public bool NotifyApproval { get; set; } = true;
    public bool NotifyError { get; set; } = true;
    public bool Startup { get; set; } = false;
    public string Position { get; set; } = "center";
    public double Speed { get; set; } = 1;
    public bool Claude { get; set; } = true;
    public bool Codex { get; set; } = true;

    static string P => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RoboIsland", "settings.json");
    public static bool Exists => File.Exists(P);
    public static Settings Load() { try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(P)) ?? new(); } catch { return new(); } }
    public void Save() { try { Directory.CreateDirectory(Path.GetDirectoryName(P)!); File.WriteAllText(P, JsonSerializer.Serialize(this)); } catch { } }
    public void Reset()
    {
        var d = new Settings();
        Lang = d.Lang; RoboEnabled = d.RoboEnabled; NotifyStart = d.NotifyStart; NotifyDone = d.NotifyDone;
        NotifyApproval = d.NotifyApproval; NotifyError = d.NotifyError; Startup = d.Startup;
        Position = d.Position; Speed = d.Speed; Claude = d.Claude; Codex = d.Codex;
        Save();
    }
}

public static class Loc
{
    static readonly Dictionary<string, string[]> D = new()
    {
        ["settings"] = new[] { "الإعدادات", "Settings" },
        ["quit"] = new[] { "إنهاء", "Quit" },
        ["language"] = new[] { "اللغة", "Language" },
        ["robo"] = new[] { "تفعيل Robo", "Enable Robo" },
        ["nStart"] = new[] { "إشعار عند بدء المهمة", "Notify when a task starts" },
        ["nDone"] = new[] { "إشعار عند انتهاء المهمة", "Notify when a task finishes" },
        ["nApproval"] = new[] { "إشعار عند طلب موافقة", "Notify on approval requests" },
        ["nError"] = new[] { "إشعار عند حدوث خطأ", "Notify on errors" },
        ["startup"] = new[] { "التشغيل مع Windows", "Start with Windows" },
        ["position"] = new[] { "موقع الجزيرة", "Island position" },
        ["left"] = new[] { "يسار", "Left" },
        ["center"] = new[] { "وسط", "Center" },
        ["right"] = new[] { "يمين", "Right" },
        ["speed"] = new[] { "سرعة الحركة", "Animation speed" },
        ["claude"] = new[] { "مراقبة Claude Code", "Monitor Claude Code" },
        ["codex"] = new[] { "مراقبة Codex", "Monitor Codex" },
        ["connectClaude"] = new[] { "ربط Claude Code", "Connect Claude Code" },
        ["connectCodex"] = new[] { "ربط Codex", "Connect Codex" },
        ["reset"] = new[] { "إعادة ضبط الإعدادات", "Reset settings" },
        ["idle"] = new[] { "جاهز", "Idle" },
        ["working"] = new[] { "يعمل", "Working" },
        ["success"] = new[] { "انتهت المهمة", "Task done" },
        ["attention"] = new[] { "ينتظرك", "Needs you" },
        ["error"] = new[] { "خطأ", "Error" },
        ["dismiss"] = new[] { "إخفاء", "Dismiss" },
        ["connected"] = new[] { "تم الربط. أعد تشغيل الأداة لتفعيله.", "Connected. Restart the tool to apply." },
        ["failed"] = new[] { "تعذر الربط (ربما يوجد إعداد notify آخر).", "Could not connect (another notify setting may exist)." },
    };
    public static string T(string k, string lang) => D.TryGetValue(k, out var v) ? v[lang == "ar" ? 0 : 1] : k;
}

public static class Hooks
{
    static string Cut(string s, int n = 80) { s = s.Replace("\r", " ").Replace("\n", " ").Trim(); return s.Length > n ? s[..n] + "…" : s; }

    public static void Send(Evt e)
    {
        try
        {
            using var c = new NamedPipeClientStream(".", "RoboIsland", PipeDirection.Out);
            c.Connect(500);
            using var w = new StreamWriter(c);
            w.WriteLine(JsonSerializer.Serialize(e));
        }
        catch { }
    }

    // Called as: RoboIsland.exe --hook claude   (JSON on stdin)
    //            RoboIsland.exe --hook codex <json>
    public static int Run(string[] a)
    {
        try
        {
            string raw = a[1] == "codex" ? (a.Length > 2 ? a[^1] : "{}") : new StreamReader(Console.OpenStandardInput()).ReadToEnd();
            var j = JsonNode.Parse(raw)!;
            Evt? e;
            if (a[1] == "claude")
            {
                var ev = (string?)j["hook_event_name"];
                e = ev switch
                {
                    "UserPromptSubmit" => new Evt("claude", "start", Cut((string?)j["prompt"] ?? "")),
                    "PreToolUse" => new Evt("claude", "working", (string?)j["tool_name"] ?? ""),
                    "Notification" => new Evt("claude", "attention", Cut((string?)j["message"] ?? "")),
                    "Stop" => new Evt("claude", "done", ""),
                    "StopFailure" => new Evt("claude", "error", Cut((string?)j["error"] ?? "")),
                    "SessionEnd" => new Evt("claude", "end", ""),
                    _ => null
                };
            }
            else
            {
                var t = (string?)j["type"] ?? "";
                var msg = Cut((string?)j["last-assistant-message"] ?? "");
                e = t.Contains("approval") ? new Evt("codex", "attention", msg)
                  : t == "agent-turn-complete" ? new Evt("codex", "done", msg) : null;
            }
            if (e != null) Send(e);
        }
        catch { }
        return 0;
    }
}

public static class Integrations
{
    static string Exe => Environment.ProcessPath!;
    static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static bool Claude()
    {
        try
        {
            var p = Path.Combine(Home, ".claude", "settings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            JsonObject root = new();
            if (File.Exists(p)) { root = JsonNode.Parse(File.ReadAllText(p))!.AsObject(); File.Copy(p, p + ".bak", true); }
            if (root["hooks"] is not JsonObject hooks) { hooks = new JsonObject(); root["hooks"] = hooks; }
            foreach (var ev in new[] { "UserPromptSubmit", "PreToolUse", "Notification", "Stop", "SessionEnd" })
            {
                if (hooks[ev] is not JsonArray arr) { arr = new JsonArray(); hooks[ev] = arr; }
                for (int i = arr.Count - 1; i >= 0; i--)
                    if (arr[i]!.ToJsonString().Contains("RoboIsland")) arr.RemoveAt(i);
                arr.Add(new JsonObject
                {
                    ["hooks"] = new JsonArray(new JsonObject { ["type"] = "command", ["command"] = $"\"{Exe}\" --hook claude" })
                });
            }
            File.WriteAllText(p, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            return true;
        }
        catch { return false; }
    }

    public static bool Codex()
    {
        try
        {
            var p = Path.Combine(Home, ".codex", "config.toml");
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            var lines = File.Exists(p) ? new List<string>(File.ReadAllLines(p)) : new List<string>();
            var line = $"notify = [\"{Exe.Replace('\\', '/')}\", \"--hook\", \"codex\"]";
            int i = lines.FindIndex(l => l.TrimStart().StartsWith("notify"));
            if (i >= 0) { if (!lines[i].Contains("RoboIsland")) return false; lines[i] = line; }
            else lines.Insert(0, line);
            if (File.Exists(p)) File.Copy(p, p + ".bak", true);
            File.WriteAllLines(p, lines);
            return true;
        }
        catch { return false; }
    }

    public static void Startup(bool on)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)!;
            if (on) k.SetValue("RoboIsland", $"\"{Exe}\""); else k.DeleteValue("RoboIsland", false);
        }
        catch { }
    }
}
