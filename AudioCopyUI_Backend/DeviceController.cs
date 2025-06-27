using AudioCopyUI.Backend;
using AudioCopyUI_MiddleWare;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Xml.Linq;
using static AudioCopyUI_MiddleWare.BackendHelper;

namespace AudioCopyUI.Backend
{
    public class DeviceController
    {
        [RequiresUnreferencedCode("something here :)")]
        public static void Init(RouteGroupBuilder group)
        {
            group.MapGet("/BootAudioClone", async (string token) =>
            {
                if (!TokenController.Auth(token))
                {
                    return Results.Unauthorized();
                }
                if (BootAudioClone is null) throw new ArgumentNullException();
                BootAudioClone();
                return Results.Text(CloneAddress);
            });

            Stopwatch sw = Stopwatch.StartNew();
            MediaInfo? i = null;

            group.MapGet("/GetSMTCInfoV2", async (string token) =>
            {
                if (!TokenController.Auth(token))
                {
                    return Results.Unauthorized();
                }


                i = await GetSMTCAsync();

                if (i is null) return Results.NoContent();
                i.AlbumArtBase64 = null;
                return Results.Ok(i);
            });

            group.MapGet("/GetSMTCInfo", async () =>
            {

                return Results.Ok(new MediaInfo
                {
                    Artist = localize("UpdateApp"),
                    AlbumArtist = localize("UpdateApp"),
                    Title = localize("UpdateApp"),
                });
            });

            group.MapGet("/GetAlbumPhoto", async (string token) =>
            {
                try
                {
                    if (!TokenController.Auth(token))
                    {
                        goto fallback;
                    }

                    MediaInfo? body = await GetSMTCAsync();


                    if (body != null && !string.IsNullOrEmpty(body.AlbumArtBase64))
                    {
                        string base64 = body.AlbumArtBase64;
                        var bytes = Convert.FromBase64String(base64);
                        var head = bytes.Take(64);
                        var headStr = Encoding.UTF8.GetString(head.ToArray());
                        if (headStr.Contains("PNG")) return Results.File(bytes, "image/png", "album.png");
                        return Results.File(bytes, "image/jpeg", "album.jpg");
                    }
                }
                catch (Exception ex)
                {
                    LogEx(ex, "Get album photo", "Backend-DeviceController");
                }

            fallback:
                return Results.File(
                    System.IO.File.ReadAllBytes(
                        Path.Combine(LocalStateFolder, "AudioCopy.png"))
                        , "image/png", "AudioCopy.png"); //返回一个默认值

            });



            var opt = new JsonSerializerOptions
            {
                TypeInfoResolver = DevicesInfoGenerationContext.Default
            };

            group.MapPost("/Discover", async (HttpContext context) =>
            {
                string identify = context.Request.Query["identify"];
                string magic = context.Request.Query["magic"];
                Log($"{context.Request.Host.Host} discovered me with: {identify}");

                DevicesInfo info = await context.Request.ReadFromJsonAsync<DevicesInfo>();

                if (magic == SecretProvider.ComputeSHA256WithSecret($"{DateTime.UtcNow:yyyy-MM-dd-HH-mm}/{identify}/{BackendAPIVersion}/{SecretProvider.ComputeSHA256WithSecret("Hello AudioCopy!")}"))
                {
                    DiscoveredClients.AddOrUpdate(info.udid, info, (_, _) => info);
                    var json = JsonSerializer.Serialize(new DevicesInfo
                    {
                        Name = Environment.MachineName,
                        AudioCopyVersion = AudioCopyVersion ?? "Unknown",
                        DeviceModel = ThisDeviceModel ?? "Unknown",
                        udid = ThisDeviceUdid,
                        DeviceType = ThisDeviceTypeID ?? DeviceType.Unknown

                    }, opt);
                    return Results.Text(json, "application/json", Encoding.UTF8);
                }

                return Results.UnprocessableEntity("Magic, time or version mismatch");
            });

            group.MapGet("GetClientName", (string token) =>
            {
                if (TokenController.Auth(token)) return Results.Text(Environment.MachineName, "text/plain", Encoding.UTF8);
                return Results.Unauthorized();
            });

            group.MapGet("GetIPAddress", () =>
            {
                return Results.Text(GetLocalNetworkAddresses().Aggregate((a,b) => $"{a},{b}"));
            });
        }


        private static async Task<MediaInfo?> GetSMTCAsync()
        {
            MediaInfo? i = null;
            var tcs = new TaskCompletionSource<MediaInfo?>();

            Dispatch(new(() =>
            {
                try
                {
                    var result = GetCurrentMediaInfoAsync();

                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }));

            return await tcs.Task;
        }

        static bool IsLocalNetwork(string ipAddress)
        {
            return ipAddress.StartsWith("192.168.") || ipAddress.StartsWith("10.") ||
                   (ipAddress.StartsWith("172.") && int.TryParse(ipAddress.Split('.')[1], out int secondOctet) && secondOctet >= 16 && secondOctet <= 31);
        }

        public static List<string> GetLocalNetworkAddresses()
        {
            List<string> address = new();


            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (var networkInterface in interfaces)
            {
                if (networkInterface.OperationalStatus == OperationalStatus.Up &&
                    networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    (bool.Parse(GetOrAddSettings("ShowAllAdapter", "False")) || (
                    !networkInterface.Description.Contains("Vmware", StringComparison.OrdinalIgnoreCase) &&
                    !networkInterface.Description.Contains("VirtualBox", StringComparison.OrdinalIgnoreCase))))
                {
                    var ipProperties = networkInterface.GetIPProperties();
                    foreach (var ipAddress in ipProperties.UnicastAddresses)
                    {
                        if (bool.Parse(GetOrAddSettings("ShowAllAdapter", "False")))
                        {
                            address.Add(ipAddress.Address.ToString());

                        }
                        else if (ipAddress.Address.AddressFamily == AddressFamily.InterNetwork &&
                            IsLocalNetwork(ipAddress.Address.ToString()))
                        {
                            address.Add(ipAddress.Address.ToString());
                        }
                    }
                }
            }

            return address.Distinct().OrderBy((a) => a.StartsWith("192.168.") ? 0 : 1).ToList();

        }


    }
}
