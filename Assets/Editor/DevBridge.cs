// Development bridge (safe to delete after setup):
//  * unpacks *.zip files dropped in <project>/_incoming into the project, then refreshes assets
//  * runs menu commands listed in <project>/_incoming/commands.txt (one menu path per line)
//  * writes compiler messages and console logs to <project>/_dev_log.txt
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

[InitializeOnLoad]
public static class DevBridge
{
    static readonly string Proj = Directory.GetParent(Application.dataPath).FullName;
    static readonly string Inbox = Path.Combine(Proj, "_incoming");
    static readonly string LogFile = Path.Combine(Proj, "_dev_log.txt");
    static double _next, _cooldownUntil;

    static DevBridge()
    {
        Directory.CreateDirectory(Inbox);
        EditorApplication.update += Tick;
        CompilationPipeline.assemblyCompilationFinished += (asm, msgs) =>
        {
            var sb = new StringBuilder();
            foreach (var m in msgs) sb.AppendLine("[" + m.type + "] " + m.file + "(" + m.line + "): " + m.message);
            Write("COMPILE " + Path.GetFileName(asm) + " -> " + msgs.Length + " messages\n" + sb);
        };
        Application.logMessageReceived += (c, st, t) =>
        {
            if (t == LogType.Log && !c.StartsWith("[")) return;   // keep the log focused
            Write(t + ": " + c + (t == LogType.Exception || t == LogType.Error ? "\n" + st : ""));
        };
        Write("DevBridge loaded " + DateTime.Now);
    }

    public static void Write(string s)
    {
        try { File.AppendAllText(LogFile, DateTime.Now.ToString("HH:mm:ss") + " " + s + "\n"); } catch { }
    }

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < _next) return;
        _next = EditorApplication.timeSinceStartup + 2.0;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        try
        {
            var zips = Directory.GetFiles(Inbox, "*.zip");
            if (zips.Length > 0 && EditorApplication.isPlaying) { EditorApplication.isPlaying = false; Write("Stopping play mode for update"); return; }
            foreach (var zip in zips)
            {
                if (new FileInfo(zip).Length == 0) continue;
                using (var a = ZipFile.OpenRead(zip))
                    foreach (var e in a.Entries)
                    {
                        if (string.IsNullOrEmpty(e.Name)) continue;
                        var dst = Path.GetFullPath(Path.Combine(Proj, e.FullName));
                        if (!dst.StartsWith(Proj)) continue;
                        Directory.CreateDirectory(Path.GetDirectoryName(dst));
                        e.ExtractToFile(dst, true);
                    }
                File.Delete(zip);
                Write("Unpacked " + Path.GetFileName(zip));
                _cooldownUntil = EditorApplication.timeSinceStartup + 12.0;
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                return;
            }
            if (EditorApplication.timeSinceStartup < _cooldownUntil) return;
            var cmds = Directory.GetFiles(Inbox, "commands*.txt");
            Array.Sort(cmds);
            foreach (var cmd in cmds)
            {
                var lines = File.ReadAllLines(cmd);
                File.Delete(cmd);
                foreach (var l in lines)
                {
                    var c = l.Trim();
                    if (c.Length == 0) continue;
                    if (c == "PLAY") { EditorApplication.isPlaying = true; Write("Entering play mode"); continue; }
                    if (c == "STOP") { EditorApplication.isPlaying = false; Write("Exit play mode"); continue; }
                    if (c.StartsWith("INVOKE "))
                    {
                        var full = c.Substring(7); int dot = full.LastIndexOf('.');
                        var tn = full.Substring(0, dot); var mn = full.Substring(dot + 1);
                        Type t = null;
                        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) { t = asm.GetType(tn); if (t != null) break; }
                        var m = t != null ? t.GetMethod(mn, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) : null;
                        if (m != null) { m.Invoke(null, null); Write("Invoked " + full); } else Write("INVOKE not found: " + full);
                        continue;
                    }
                    if (c.StartsWith("SHOT "))
                    {
                        ScreenCapture.CaptureScreenshot(Path.Combine(Proj, c.Substring(5)));
                        Write("Screenshot " + c.Substring(5));
                        continue;
                    }
                    if (c.StartsWith("OPEN ")) { UnityEditor.SceneManagement.EditorSceneManager.OpenScene(c.Substring(5)); Write("Opened " + c.Substring(5)); continue; }
                    bool ok = EditorApplication.ExecuteMenuItem(c);
                    Write("Menu '" + c + "' -> " + ok);
                }
                return;
            }
        }
        catch (Exception ex) { Write("DevBridge error: " + ex.Message); }
    }
}
