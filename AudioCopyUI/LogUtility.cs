using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AudioCopyUI
{
    public class InterceptingTextWriter : TextWriter
    {
        private readonly TextWriter writer;

        string cache = "";

        string? name = "";

        object locker = new();

        public InterceptingTextWriter(TextWriter original, string? _name)
        {
            writer = original;
            name = _name;
        }

        public override Encoding Encoding => writer.Encoding;

        public override void Write(char value)
        {
            writer.Write(value);
            lock (locker)
            {
                cache += value.ToString();
                if (cache.Contains(Environment.NewLine))
                {
                    Log(cache.Replace(Environment.NewLine, ""), name ?? "Console");
                    cache = "";
                }
            }
            
        }

        public override void Write(string? value)
        {
            writer.Write(value);
            if (value is null) return;
            lock (locker)
            {
                cache += value.ToString();
                if (cache.Contains(Environment.NewLine))
                {
                    Log(cache.Replace(Environment.NewLine, ""), name ?? "Console");
                    cache = "";
                }
            }
        }

        public override void WriteLine(string? value)
        {
            writer.WriteLine(value);
            lock (locker)
            {
                cache += value;
                Log(cache.Replace(Environment.NewLine, ""), name ?? "Console");
                cache = "";
            }
        }



    }

    class Logger
    {
        static string filePath = "";
        static ConcurrentQueue<string> buffer = new();

        public static string ___LogPath___ => filePath;
        public static bool ___PublicStackOn___ = false;
        public static string _LoggerInit_(string path, bool name = false)
        {
            if (bool.Parse(SettingUtility.GetOrAddSettings("RealtimeLogging", false.ToString()))) StartLogCmdWindow();
            running = true;
            if (name) filePath = path;
            else filePath = Path.Combine(path, $"{DateTime.Now:yyyy-MM-dd-hh-mm-ss}.log");
            if (!name) File.WriteAllText(filePath, $"Logger start at:{DateTime.Now}\r\n");
            writer = new(WriteLog);
            writer.Start();


            return filePath;
        }

        private static void StartLogCmdWindow()
        {
            try
            {
                logCmdProcess = new Process();
                logCmdProcess.StartInfo.FileName = "powershell.exe";
                logCmdProcess.StartInfo.UseShellExecute = false;
                logCmdProcess.StartInfo.RedirectStandardInput = true;
                //logCmdProcess.StartInfo.RedirectStandardOutput = true;
                logCmdProcess.StartInfo.CreateNoWindow = false;
                logCmdProcess.Start();
                //logCmdProcess.BeginOutputReadLine();
                logCmdWriter = logCmdProcess.StandardInput;
                if (logCmdWriter != null)
                {
                    logCmdWriter.AutoFlush = true;
                    logCmdWriter.WriteLine(
                        """
                        function loopback 
                        {
                            $k = 0
                            for(;($k -eq 0);) 
                            {

                                Read-Host 1>$null | Write-Host
                            }
                        }

                        Clear-Host;loopback
                        """);
                }
            }
            catch (Exception ex)
            {
                Log(ex, "boot logwindow", "logger");
            }
        }

        static void WriteLog()
        {
            while (running)
            {
                if (buffer.TryDequeue(out var str))
                {
                    File.AppendAllText(filePath, str);
                    if (logCmdWriter != null)
                    {
                        try
                        {
                            logCmdWriter.Write(str);
                        }
                        catch
                        {

                        }
                    }
                }

            }
        }

        static Thread writer;
        private static bool running;
        public static string ___PublicBuffer___ = "";
        private static Process? logCmdProcess;
        private static StreamWriter? logCmdWriter;


        public static void __FlushLog__(bool restart = false)
        {
            running = false;
            foreach (var item in buffer)
            {
                File.AppendAllText(filePath, item);
                if (logCmdWriter != null)
                {
                    try
                    {
                        logCmdWriter.Write(item);
                    }
                    catch
                    {

                    }
                }
            }
            if (restart)
            {
                running = true;
                writer = new(WriteLog);
                writer.Start();
            }
        }


        public static void Log(string msg) => Log(msg, "info"); //fix the vs auto completion

        public static void Log(Exception e) => Log(e, false);

        public static void Log(Exception e, bool isCritical) => Log($"{(isCritical ? "A critical " : "")}{e.GetType().Name} error: {e.Message} {e.StackTrace}", isCritical ? "Critical" : "error");

        public static void Log(Exception e, string message = "", object? sender = null) => Log($"{sender?.GetType().Name} report a {e.GetType().Name} error when trying to {message} \r\n error message: {e.Message} {e.StackTrace}{(e.Data.Contains("RemoteStackTrace") ? e.Data["RemoteStackTrace"] : "")}", "error");

        public static async Task<bool> LogAndDialogue(Exception e, string whatDoing = "", string? priButtonText = null, string? subButtonText = null, Page element = null, string append = "")
            => await LogAndDialogue(e, whatDoing, priButtonText, subButtonText, element, element.XamlRoot);

        public static async Task<bool> LogAndDialogue(Exception e, string whatDoing = "", string? priButtonText = null, string? subButtonText = null, object? obj = null, XamlRoot? root = null, string append = "")
        {
            Log(e, whatDoing, obj);
            return await ShowDialogue(localize("Error"), string.Format(localize("LogAndDialogue_Content"), whatDoing, e.GetType().Name, e.Message) + (string.IsNullOrWhiteSpace(append) ? "" : "\r\n" + append), priButtonText ?? localize("Accept"), subButtonText, root);
        }

        public static void Log(string msg, string level = "info")
        {
#if DEBUG
            Debug.Write($"[{level} @ {DateTime.Now}] {(msg.Contains('\r') ? "mutil-line log:\r\n" : "")}{msg}{(msg.Contains('\r') ? "\r\nmutil-line log ended." : "")}\r\n");
#endif
            buffer.Enqueue($"[{level} @ {DateTime.Now}] {(msg.Contains('\r') ? "mutil-line log:\r\n" : "")}{msg}{(msg.Contains('\r') ? "\r\nmutil-line log ended." : "")}\r\n");

            if (___PublicStackOn___ && level == "showToGUI") ___PublicBuffer___ = msg;


        }

#if DEBUG
        public static void LogDebug(string msg, string level = "info") => Log(msg, level);
#else
        public static void LogDebug(string msg, string level = "info") { }
#endif

        ~Logger()
        {
            if (logCmdProcess is not null && !logCmdProcess.HasExited)
            {
                logCmdProcess.Kill();
            }
        }
    }
}
