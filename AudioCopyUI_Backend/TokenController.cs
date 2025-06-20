using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using static AudioCopyUI_MiddleWare.BackendHelper;


namespace AudioCopyUI.Backend
{
    public class TokenController
    {
        private const string StringTable = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
        [DebuggerNonUserCode()]
        public static string MakeRandString(int length) => string.Concat(Enumerable.Repeat(StringTable, length / StringTable.Length + 5)).OrderBy(NewGuid).Take(length).Select(x => (char)x).Aggregate("", (x, y) => x + y);
        [DebuggerNonUserCode()]
        static Guid NewGuid(char _) => Guid.NewGuid();

        public static ConcurrentDictionary<string, string> Devices = new(),DiscoveredList = new();

        static Thread V1PairThread;

        [RequiresUnreferencedCode("something here :)")]
        public static void Init(RouteGroupBuilder group)
        {
            string ClientToken = GetOrAddSettings("udid", MakeRandString(128));
            if (File.Exists(Path.Combine(LocalStateFolder, "devices.yaml")))
            {
                using (StreamReader sr = new(Path.Combine(LocalStateFolder, "devices.yaml"))) //避免使用PublishTrimmed时候被极其受限的反序列化可用性影响到了
                {
                    string? line;
                    while (!string.IsNullOrWhiteSpace((line = sr.ReadLine())))
                    {
                        Devices.TryAdd(line.Split(':')[0], line.Split('\'')[1].Replace('\'', '\0'));
                    }
                }
            }
            else
            {
                Devices.AddOrUpdate(ClientToken, "localhost", (_, _) => "localhost");
                SaveDevices();
            }

            AudioCopyUI_MiddleWare.BackendHelper.CancelPair = new(DiscoveredList.Clear);


            Devices.AddOrUpdate(ClientToken, "localhost", (_, _) => ClientToken);

            Backend.backend.MapGet("/RequirePair", async (HttpContext context, string udid, string name, double version = 1) =>
            {
                context.Response.ContentType = "text/plain";
                if (udid.StartsWith("AudioCopy"))
                {
                    return Results.Text("AudioCopy" + Environment.MachineName);
                }

                udid = udid.Split('@')[0].Trim();
                var verCode = udid.Split('@').Last().Trim();
                if (!udid.Aggregate(true, (a, b) => a && StringTable.Contains(b))) return Results.BadRequest("Contain irregular syntax");

                if (Devices.ContainsKey(udid)) return Results.BadRequest("Already paired");

                bool success = false;

                if (version == 1 && !bool.Parse(GetOrAddSettings("V1PairCompatibility", "False")))
                {
                    V1PairThread = new(() => V1Pair(udid, name));
                    V1PairThread.Start();
                    return Results.Created("Added", null);

                }

                var tcs = new TaskCompletionSource<bool>();

                Dispatch(new(async () =>
                {

                    try
                    {
                        var result = await ShowDialogueWithRoot(localize("PairText"), string.Format(localize("PairConfirm"), name), localize("Accept"), localize("Cancel"));
                        tcs.SetResult(result);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }));

                success = await tcs.Task;


                if (success)
                {
                    Dispatch(new(() =>
                    {

                        if (version == Backend.VersionCode)
                        {
                            ShowDialogueWithRoot(localize("PairText"), localize("PairDone"), localize("Accept"), null);

                        }
                        else
                        {
                            ShowDialogueWithRoot(localize("PairText"), localize("UpdateApp"), localize("Accept"), null);

                        }

                    }));
                    if (name.Contains('\'')) name = name.Replace('\'', '_');
                    Devices.TryAdd(udid, name);
                    SaveDevices();
                    return Results.Created("Added", null);
                }
                else
                {
                    return Results.BadRequest("Deny");

                }


            });

            if (!bool.Parse(GetOrAddSettings("NoNewPair", "False")))
            {
                group.MapGet("/Discovered", async (HttpContext context, string udid, string name) =>
                {
                    List<string> emojis = new();
                    string PairID = "10.0.0.0";
                    do
                    {
                        PairID = $"10.{Random.Shared.Next(0, 254)}.{Random.Shared.Next(0, 254)}.{Random.Shared.Next(1, 254)}";
                        emojis = GenerateConnectionEmoji([PairID], false);
                        int count = emojis.Distinct().Count();
                    }
                    while (emojis.Distinct().Count() > 12);
                    string[] src = new string[emojis.Count];
                    emojis.ToArray().CopyTo(src, 0);
                    do
                    {
                        var item = TypicalEmojis[Random.Shared.Next(0, TypicalEmojis.Length - 1)];
                        if (!emojis.Contains(item)) emojis.Add(item);
                    }
                    while (emojis.Count < 16);
                    DiscoveredList.AddOrUpdate(udid, PairID, (_, _) => PairID);
                    Dispatch(new(async () =>
                    {
                        //todo:提示用户
                        //if (!IsWindowActive())
                        //{
                        //}

                        await ShowSpecialDialogue("PairDetected", new KeyValuePair<string, string[]>(name, src));

                    }));
                    Log(src.Aggregate((a, b) => $"{a},{b}"));
                    return Results.Text(emojis.OrderBy((_) => Guid.NewGuid()).Aggregate((a, b) => $"{a},{b}"), "text/plain", Encoding.UTF8);
                });


                group.MapGet("/ValidatePairID", async (HttpContext context, string selected, string udid, string name) =>
                {
                    if (!DiscoveredList.ContainsKey(udid)) return Results.BadRequest("Pairing is canceled or not started yet.");

                    var ids = DecodeEmojisToString(selected.Split(',', StringSplitOptions.RemoveEmptyEntries).ToArray());

                    if (ids.Count == 1 && ids[0] == DiscoveredList[udid])
                    {
                        if (name.Contains('\'')) name = name.Replace('\'', '_');

                        Devices.TryAdd(udid, name);
                        SaveDevices();
                        await ShowSpecialDialogue("PairFinished", new object());
                        return Results.Created("Added", null);
                    }

                    return Results.BadRequest("Token is invalid.");
                });
            }
        }

        static async void V1Pair(string id,string name)
        {
            Dispatch(new(async () =>
            {
                var result = await ShowDialogueWithRoot(localize("PairText"), string.Format(localize("PairConfirm"), name), localize("Accept"), localize("Cancel"));
                if (result)
                {
                    await ShowDialogueWithRoot(localize("PairText"), localize("UpdateApp"), localize("Accept"), null);
                    if (name.Contains('\'')) name = name.Replace('\'', '_');
                    Devices.TryAdd(id, name);
                    SaveDevices();
                }

            }));
            
        }

        public static void SaveDevices()
        {
            using (StreamWriter sw = new(Path.Combine(LocalStateFolder, "devices.yaml")))
            {
                foreach (var item in Devices)
                {
                    sw.WriteLine($"{item.Key}: \'{item.Value}\'");
                }
            }
            
        }

        internal static bool Auth(string token) => Devices.ContainsKey(token);

        static string[] TypicalEmojis = [
  "😀", "😡", "😭", "😱", "😴", "🤢",  "😎", "🥶",
  
  "👍", "👎", "✌️", "✋", "👋", "👏", "🤘", "🙏",
  
  "👨‍⚕️", "👩‍🎓", "🧑‍🚀", "🦸‍♂️", "🧛‍♀️", "👻", "🧞‍♀️", "🧝‍♂️",
  
  "🐶", "🐱", "🐭", "🐸", "🐧", "🐵", "🦁", "🐷",
  
  "🍎", "🍉", "🍌", "🍓", "🍕", "🍔", "🍟", "🍩",
  
  "🚗", "🚒", "🚌", "🚑", "🚀", "✈️", "🚜", "🏍️",
  
  "📱", "💻", "🎧", "🎸", "🕶️", "💡", "🔦", "📦",
  
  "🌞", "🌧️", "⛄", "🌈", "🌪️", "🔥", "🌊", "🌵" 
    ];

        static List<string> GenerateConnectionEmoji(List<string> addresses, bool force = false)
        {
            var emojis = new List<string>();
            var bitList = new List<bool>();

            foreach (string addr in addresses)
            {
                var nums = addr.Split('.');
                int type;
                if (nums[0] == "192") type = 0;
                else if (nums[0] == "10") type = 1;
                else if (nums[0] == "172") type = 2;
                else continue;

                bitList.Add((type & 2) != 0);
                bitList.Add((type & 1) != 0);

                byte[] ipBytes = IPAddress.Parse(addr).GetAddressBytes();
                foreach (byte b in ipBytes)
                {
                    for (int i = 7; i >= 0; i--)
                        bitList.Add((b & (1 << i)) != 0);
                }
            }

            int pad = (6 - (bitList.Count % 6)) % 6;
            for (int i = 0; i < pad; i++)
                bitList.Add(false);

            for (int i = 0; i < bitList.Count; i += 6)
            {
                int idx = 0;
                for (int j = 0; j < 6; j++)
                {
                    if (bitList[i + j])
                        idx |= 1 << (5 - j);
                }
                emojis.Add(TypicalEmojis[idx]);
            }

            return emojis;
        }

        static List<string> DecodeEmojisToString(IEnumerable<string> emojiSequence)
        {
            var bitList = new List<bool>();
            foreach (var emoji in emojiSequence)
            {
                int idx = Array.IndexOf(TypicalEmojis, emoji);
                for (int j = 5; j >= 0; j--)
                    bitList.Add((idx & (1 << j)) != 0);
            }

            int entryBits = 2 + 32;
            int count = bitList.Count / entryBits;
            var addresses = new List<string>(count);

            for (int i = 0; i < count; i++)
            {
                int offset = i * entryBits;

                byte[] bytes = new byte[4];
                for (int b = 0; b < 4; b++)
                {
                    int v = 0;
                    for (int k = 0; k < 8; k++)
                    {
                        if (bitList[offset + 2 + b * 8 + k])
                            v |= 1 << (7 - k);
                    }
                    bytes[b] = (byte)v;
                }

                addresses.Add(new IPAddress(bytes).ToString());
            }

            return addresses;
        }


    }
}
