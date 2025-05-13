/*
 * Copyright (C) 2025 Your Name
 * Licensed under GPLv2. See LICENSE for details.
 */

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace AudioCopyUI
{
    class GlobalUtility
    {


        public static string LocalStateFolder => ApplicationData.Current.LocalFolder.Path;

        public static ContentDialog _____SplashDialog_____;

        static ConcurrentDictionary<Page, object> locker = new();
        static ConcurrentDictionary<object, bool> isShowing = new();

        public static async Task<bool> ShowDialogue(string title, string content, string priButtonText, string? subButtonText, Page element)
            => await ___ShowDialogue__WithRoot___(title, content, priButtonText, subButtonText, element.Content.XamlRoot);


        public static async Task<bool> ___ShowDialogue__WithRoot___(string title, string content, string priButtonText, string? subButtonText, XamlRoot element)
        {

            ContentDialog confirmDialog = new ContentDialog
            {
                XamlRoot = element,
                Title = title,
                Content = content,
                PrimaryButtonText = priButtonText,
                CloseButtonText = subButtonText,
                DefaultButton = ContentDialogButton.Primary
            };
            return await ShowDialogue(confirmDialog, element, ContentDialogResult.Primary);
        }


        private static async Task<bool> ShowDialogue(ContentDialog confirmDialog, object element, ContentDialogResult resultButton)
        {
            try
            {
                Task t = new(() =>
                {
                    while (true)
                    {
                        if (isShowing.TryGetValue(element, out var key))
                        {
                            if (!key) break;
                        }
                        else break;//not exist
                    }
                });
                t.Start();
                await t;
                isShowing.AddOrUpdate(element, (_) => true, (_, _) => true);
                var result = (await confirmDialog.ShowAsync()) == resultButton;
                isShowing[element] = false;
                return result;
            }
            catch (Exception ex)
            {
                Log("Trying to show many dialog at one time.","error");
                await Task.Delay(Random.Shared.Next(1000, 5000));
                return await ShowDialogue(confirmDialog, element, resultButton);
            }




        }


        

    }



    class SettingUtility
    {
        private static ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        public static string HostToken => GetOrAddSettings("hostToken", "abcd");

        public static string GetSetting(string key) => localSettings.Values[key] as string;
        public static void TryGetSettings(string key, out string target, string defaultValue = "")
        {
            if (localSettings.Values.TryGetValue(key, out var value))
            {
                target = value as string ?? throw new InvalidOperationException();
            }
            else
            {
                target = defaultValue;
            }
        }

        public static void SetSettings(string key, string value)
        {
            localSettings.Values[key] = value;
        }

        public static string GetOrAddSettings(string key, string defaultValue = "")
        {
            if (localSettings.Values.TryGetValue(key, out var value))
            {
                return value as string ?? throw new InvalidOperationException();
            }
            else
            {
                SetSettings(key, defaultValue);
                return defaultValue;
            }
        }

        public static bool Exists(string key) => localSettings.Values.ContainsKey(key);
    }

    class Logger
    {
        static string filePath = "";
        static ConcurrentQueue<string> buffer = new(), publicBuffer = new();

        public static string ___LogPath___ => filePath;
        public static bool ___PublicStackOn___ = false;
        public static void _LoggerInit_(string path)
        {    
            running = true;
            filePath = Path.Combine(path, $"{DateTime.Now:yyyy-MM-dd-hh-mm-ss}.log");
            File.WriteAllText(filePath, $"Logger start at:{DateTime.Now}\r\n");
            writer = new(WriteLog);
            writer.Start();
        }

        static void WriteLog()
        {
            while (running)
            {
                if (buffer.TryDequeue(out var str))
                {
                    File.AppendAllText(filePath, str);
                }
            }
        }

        static Thread writer;
        private static bool running;
        public static string ___PublicBuffer___ = "";

        public static void __FlushLog__(bool restart = false)
        {
            running = false;
            foreach (var item in buffer)
            {
                File.AppendAllText(filePath, item);
            }
            if (restart)
            {
                running = true;
                writer = new(WriteLog);
                writer.Start();
            }
        }


        public static void Log(string msg) => Log(msg, "info"); //fix the vs auto completion

        public static void Log(Exception e) => Log(e,false);

        public static void Log(Exception e, bool isCritical) => Log($"{(isCritical ? "A critical " : "")}{e.GetType().Name} error: {e.Message} {e.StackTrace}",isCritical ? "Critical" : "error");

        public static void Log(Exception e, string message = "", object? sender = null) => Log($"{sender?.GetType().Name} report a {e.GetType().Name} error when trying to {message} \r\n error message: {e.Message} {e.StackTrace}{(e.Data.Contains("RemoteStackTrace") ? e.Data["RemoteStackTrace"] : "")}", "error");

        public static async Task<bool> LogAndDialogue(Exception e, string whatDoing = "", string? priButtonText = "好的", string? subButtonText = null, Page element = null)
            => await LogAndDialogue(e, whatDoing, priButtonText, subButtonText, element, element.XamlRoot);

        public static async Task<bool> LogAndDialogue(Exception e, string whatDoing = "", string? priButtonText = "好的", string? subButtonText = null, object? obj = null, XamlRoot? root = null)
        {
            Log(e,whatDoing,obj);
            return await ___ShowDialogue__WithRoot___("错误", $"{whatDoing}时发生了{e.GetType().Name}错误：\r\n{e.Message}", priButtonText ?? "好的", subButtonText, root);
        }

        public static void Log(string msg, string level = "info")
        {
#if DEBUG
            Debug.Write($"[{level} @ {DateTime.Now}] {(msg.Contains('\r') ? "mutil-line log:\r\n" : "")}{msg}{(msg.Contains('\r') ? "\r\nmutil-line log ended." : "")}\r\n");
#endif
            buffer.Enqueue($"[{level} @ {DateTime.Now}] {(msg.Contains('\r')? "mutil-line log:\r\n" : "")}{msg}{(msg.Contains('\r') ? "\r\nmutil-line log ended." : "")}\r\n");

            if (___PublicStackOn___  && level == "showToGUI") ___PublicBuffer___ = msg;


        }

#if DEBUG
        public static void LogDebug(string msg, string level = "info") => Log(msg, level);
#else
        public static void LogDebug(string msg, string level = "info") { }
#endif

    }

    public class DoubleToSliderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // 将 double 转为 Slider 的 Value（double）
            if (value is double d)
                return d;
            if (value is string s && double.TryParse(s, out var result))
                return result;
            return 0d;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // 将 Slider 的 Value（double）转回 double
            if (value is double d)
                return d;
            return 0d;
        }
    }
}
