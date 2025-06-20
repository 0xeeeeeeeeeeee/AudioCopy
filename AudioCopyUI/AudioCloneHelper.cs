using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Windows.Forms.DataFormats;

namespace AudioCopyUI
{
    internal class AudioCloneHelper
    {
        private static Process cloneProcess;

        public static int Port { get; private set; }
        public static string Token { get; private set; }
        public static bool Running { get; private set; } = false;

        static Timer tokenRefreshTimer;

        static object locker = new();

        internal static async Task Boot()
        {
            if (Running) return;
            
            //if (bool.Parse(SettingUtility.GetOrAddSettings("OldBackend", "False")))
            //{
            //    Port = 23456;
            //    Running = true;
            //    return;
            //}
            if(bool.Parse(SettingUtility.GetOrAddSettings("OverrideAudioCloneOptions", "False")) && uint.TryParse(SettingUtility.GetOrAddSettings("OverrideAudioClonePort", "null"),out var v))
            {
                Port = (int)v;
                Running = true;
                return;
            }
            var random = new Random();
            int minPort = 23457;
            int maxPort = 65000;
            int port;
            bool isAvailable = false;

            for (int i = 0; i < 100; i++)
            {
                port = random.Next(minPort, maxPort + 1);
                if (IsPortAvailable(port))
                {
                    Port = port;
                    isAvailable = true;
                    break;
                }
            }

            if (!isAvailable)
            {
                throw new InvalidOperationException("No port available.");
            }

            lock (locker)
            {
                Token = AlgorithmServices.MakeRandString(256);
            }

            if (!Running)
            {
                try
                {


                    var backendPath = Path.Combine(LocalStateFolder, @"backend\AudioClone.Server.exe");
                    var i = new ProcessStartInfo
                    {
                        FileName = backendPath,
                        WorkingDirectory = Path.Combine(LocalStateFolder, "backend"),
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        RedirectStandardInput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,

                    };

                    string format;

                    if ((format = SettingUtility.GetOrAddSettings("resampleType", "1")) != "1")
                    {
                        string[]? fmtArr = (format is not null && !string.IsNullOrWhiteSpace(format)) ? format.Split(',') : Array.Empty<string>();
                        if (fmtArr.Length == 3 && fmtArr.All(x => int.TryParse(x, out var val) && val > 0))
                        {
                            i.EnvironmentVariables.Add("AudioCopy_DefaultAudioQuality", format);
                        }
                    }
                
                    i.EnvironmentVariables.Add("ASPNETCORE_URLS", $"http://+:{Port}");
#if DEBUG
                    i.EnvironmentVariables.Add("ASPNETCORE_ENVIRONMENT", "Development");
#endif
                    cloneProcess = new Process
                    {
                        StartInfo = i
                    };
                    cloneProcess.OutputDataReceived += (sender, e) => { 
                        if (e.Data != null)
                        {
                            Log(e.Data ?? "", "clone_stdout");

                            if (e.Data.StartsWith("!Token"))
                            {
                                cloneProcess.StandardInput.WriteLine(Token);
                                
                            }
                            
                        }
                    };
                    cloneProcess.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            Log(e.Data ?? "", "clone_stderr");
                        }
                    };
                    cloneProcess.EnableRaisingEvents = true;
                    cloneProcess.Exited += CloneProcess_Exited;
                    cloneProcess.Start();
                    cloneProcess.BeginOutputReadLine();               
                    cloneProcess.BeginErrorReadLine();
                    await Task.Delay(5000);
                }
                catch(Exception ex)
                {
                    Log(ex,"Boot Audioclone","Boot audioclone");
                }
            }
            Running = true;

            tokenRefreshTimer ??= new Timer(RefreshToken, null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));

        }

        private static void CloneProcess_Exited(object? sender, EventArgs e)
        {
            Running = false;
            Log($"AudioClone.Server (port:{Port}) has exited.");
            Port = -1;
        }

        private static void RefreshToken(object? state)
        {
            lock (locker)
            {
                Token = AlgorithmServices.MakeRandString(256);
            }
        }

        private static bool IsPortAvailable(int port)
        {
            try
            {
                var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return true;
            }
            catch
            {
                return false;
            }
        }
        public static async Task Kill()
        {
            if (cloneProcess is not null)
                try
                {
                    cloneProcess.Kill();
                }
                catch (Exception) { }

            ProcessStartInfo i = new ProcessStartInfo
            {
                FileName = "taskkill.exe",
                Arguments = "/t /f /im \"AudioClone.Server.exe\" ",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            Log(localize("Init_Stage1"), "showToGUI");

            var p = Process.Start(i);
            await Task.Run(p.WaitForExit);
            Log($"Taskkill write stdout:{p.StandardOutput.ReadToEnd()} stderr:{p.StandardError.ReadToEnd()}");

        }

        
    }
}
