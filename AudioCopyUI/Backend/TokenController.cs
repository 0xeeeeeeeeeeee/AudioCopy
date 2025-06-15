using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NAudio.SoundFont;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

namespace AudioCopyUI.Backend
{
    internal class TokenController
    {
        private const string StringTable = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";

        public static ConcurrentDictionary<string, string> Devices = new();

        static Thread V1PairThread;

        public static void Init(RouteGroupBuilder group)
        {
            if (File.Exists(Path.Combine(LocalStateFolder, "devices.json")))
            {
                Devices = JsonSerializer.Deserialize<ConcurrentDictionary<string, string>>(File.ReadAllText(Path.Combine(LocalStateFolder, "devices.json"))) ?? new();
            }
            if(File.Exists(Path.Combine(LocalStateFolder, "wwwroot\\tokens.json")))
            {
                ConvertPairList();
            }
            string ClientToken = SettingUtility.GetOrAddSettings("udid", AlgorithmServices.MakeRandString(128));

            Devices.AddOrUpdate(ClientToken, "localhost", (_, _) => ClientToken);

            Backend.backend.MapGet("/RequirePair", async (HttpContext context, string udid, string name, int version = 1) =>
            {
                context.Response.ContentType = "text/plain";
                if (udid.StartsWith("AudioCopy"))
                {
                    return Results.Text("AudioCopy" + Environment.MachineName);
                }

                //if (udid == "AudioCopy")
                //{
                //    return Results.BadRequest("Please update your application to AudioCopy v2 to get more features");
                //}

                udid = udid.Split('@')[0].Trim();
                
                var verCode = udid.Split('@').Last().Trim();

                //if (version == 1)
                //{
                //    return Results.BadRequest("Please update your application to AudioCopy v2 to get more features");
                //}

                if (Devices.ContainsKey(udid)) return Results.BadRequest("Already paired");

                if (!udid.Aggregate(true, (a, b) => a && StringTable.Contains(b))) return Results.BadRequest("Contain irregular syntax");

                bool success = false;

                //Backend.Dispatcher(new Action(async () =>
                //{
                //    success = 
                //}));
                
                if(version == 1 && !bool.Parse(SettingUtility.GetOrAddSettings("V1PairCompatibility", "False")))
                {
                    V1PairThread = new(() => V1Pair(udid, name));
                    V1PairThread.Start();
                    return Results.Created("Added", null);

                }

                var tcs = new TaskCompletionSource<bool>();

                MainWindow.dispatcher.TryEnqueue(async () =>
                {
                   
                    try
                    {
                        var result = await ___ShowDialogue__WithRoot___(localize("PairText"), string.Format(localize("PairConfirm"),name), localize("Accept"), localize("Cancel"), MainWindow.xamlRoot);
                        //await ___ShowDialogue__WithRoot___("info", "done", localize("Accept"), null, MainWindow.xamlRoot);

                        tcs.SetResult(result);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                });

                success = await tcs.Task;


                if (success)
                {
                    MainWindow.dispatcher.TryEnqueue(() =>
                    {
                        
                        if(version == Backend.VersionCode)
                        {
                            ___ShowDialogue__WithRoot___(localize("PairText"), localize("PairDone"), localize("Accept"), null, MainWindow.xamlRoot);

                        }
                        else
                        {
                            ___ShowDialogue__WithRoot___(localize("PairText"), localize("UpdateApp"), localize("Accept"), null, MainWindow.xamlRoot);

                        }

                    });
                    Devices.TryAdd(udid, name);
                    SaveDevices();
                    return Results.Created("Added", null);
                }
                else
                {
                    return Results.BadRequest("Deny");

                }


            });


            
        }

        static async void V1Pair(string id,string name)
        {
            MainWindow.dispatcher.TryEnqueue(async () =>
            {
                var result = await ___ShowDialogue__WithRoot___(localize("PairText"), name, localize("Accept"), localize("Cancel"), MainWindow.xamlRoot);
                if (result)
                {
                    await ___ShowDialogue__WithRoot___(localize("PairText"), localize("UpdateApp"), localize("Accept"), null, MainWindow.xamlRoot);
                    Devices.TryAdd(id, name);
                    SaveDevices();
                }


            });
            
        }
         
        public static void SaveDevices()
        {
            File.WriteAllText(Path.Combine(LocalStateFolder, "devices.json"), JsonSerializer.Serialize(Devices));
        }

        internal static bool Auth(string token) => Devices.ContainsKey(token);

        public static void ConvertPairList()
        {
            Dictionary<string, string> dest = new();
            Dictionary<string, string> src;
            try
            {
                src = JsonSerializer.Deserialize<Dictionary<string, string>>(SettingUtility.GetSetting("deviceMapping")) ?? new();

            }
            catch (Exception)
            {
                src = new();
            }
            //File.Move(Path.Combine(LocalStateFolder, "wwwroot\\tokens.json"), Path.Combine(LocalStateFolder, "wwwroot\\tokens.json.old"));
            SaveDevices();
        }
    }
}
