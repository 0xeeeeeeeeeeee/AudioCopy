using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Foundation;

namespace AudioCopyUI.Backend
{
    internal class TokenController
    {
        private const string StringTable = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";

        private static ConcurrentDictionary<string, string> Devices = new();

        public static void Init(RouteGroupBuilder group)
        {
            if (File.Exists(Path.Combine(LocalStateFolder, "devices.json")))
            {
                Devices = JsonSerializer.Deserialize<ConcurrentDictionary<string, string>>(File.ReadAllText(Path.Combine(LocalStateFolder, "devices.json"))) ?? new();
            }

            group.MapPost("/RequirePair", async (string udid, string name) =>
            {
                if (udid == "AudioCopy")
                {
                    return Results.Ok("AudioCopy" + Environment.MachineName);
                }

                if (Devices.ContainsKey(udid)) return Results.BadRequest("Already paired");

                if (!udid.Aggregate(true, (a, b) => a && StringTable.Contains(b))) return Results.BadRequest("Contain irregular syntax");

                bool success = false;

                //Backend.Dispatcher(new Action(async () =>
                //{
                //    success = 
                //}));

                var tcs = new TaskCompletionSource<bool>();

                MainWindow.dispatcher.TryEnqueue(async () =>
                {
                    try
                    {
                        var result = await ___ShowDialogue__WithRoot___("pair", name, "yes", "no", MainWindow.xamlRoot);
                        //await ___ShowDialogue__WithRoot___("info", "done", "yes", null, MainWindow.xamlRoot);

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

        private static void SaveDevices()
        {
            File.WriteAllText(Path.Combine(LocalStateFolder, "devices.json"), JsonSerializer.Serialize(Devices));
        }

        internal static bool Auth(string token) => Devices.ContainsKey(token);


    }
}
