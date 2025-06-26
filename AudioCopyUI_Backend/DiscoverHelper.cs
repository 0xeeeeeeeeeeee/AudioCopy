using AudioCopyUI.Backend;
using AudioCopyUI_MiddleWare;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static AudioCopyUI_MiddleWare.BackendHelper;

namespace AudioCopyUI.Backend
{
    public class DiscoverHelper
    {
        static Thread discoverThread;
        static object locker = new();
        static object addLocker = new();

        static HttpClient c;
        public static DevicesInfo thisDevice;

        static JsonSerializerOptions sourceGenOptions = new JsonSerializerOptions
        {
            TypeInfoResolver = DevicesInfoGenerationContext.Default
        };

        static string identify = $"{Environment.MachineName};{AudioCopyVersion};{ThisDeviceModel}";


        public static void Init(int interval)
        {
            
            c = new();
            c.Timeout = TimeSpan.FromSeconds(3);
            thisDevice = new DevicesInfo
            {
                Name = Environment.MachineName,
                AudioCopyVersion = AudioCopyVersion ?? "Unknown",
                DeviceModel = ThisDeviceModel ?? "Unknown",
                udid = ThisDeviceUdid,
                DeviceType = ThisDeviceTypeID ?? DeviceType.Unknown
            };
            discoverThread = new(() =>
            {
                lock (locker)
                {
                    Discover().GetAwaiter().GetResult();
                    Thread.Sleep(interval * 1000);
                }

            });

            discoverThread.Start();
        }


        public static async Task Discover(int maxDegreeOfParallelism = 128)
        {
            var ips = GetLocalNetworkAddresses();
            var tasks = new List<Task>();
            var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
            string helloHash = SecretProvider.ComputeSHA256WithSecret("Hello AudioCopy!");
            var reachable = new List<string>();
            Log("Start detecting...");
            foreach (var ip in ips)
            {
                await semaphore.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var reply = await new Ping().SendPingAsync(ip, 350);
                        if (reply.Status == IPStatus.Success)
                        {
                            //Log($"{ip} is reachable.");
                            reachable.Add(ip);
                        }
                    }
                    catch
                    {
                        //Log($"{ip} is not reachable.");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                    
                }));
                
            }
            await Task.WhenAll(tasks);
            Log($"Detected {reachable.Count} clients.");

            var tasks1 = new List<Task>();
            var semaphore1 = new SemaphoreSlim(8);
            Log("Start discovering...");
            foreach (var addr in reachable)
            {
                await semaphore1.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var item = addr.ToLower(); //避免引用
                        HttpClient h = new();
                        h.Timeout = TimeSpan.FromSeconds(3);
                        //Log($"Discovering:{item}");
                        var jsonContent = new StringContent(JsonSerializer.Serialize(thisDevice, sourceGenOptions), Encoding.UTF8, "application/json");
                        string now = DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm");
                        string magic = SecretProvider.ComputeSHA256WithSecret($"{now}/{identify}/{BackendAPIVersion}/{helloHash}");
                        string url = $"http://{item}:23456/api/device/Discover?identify={Uri.EscapeDataString(identify)}&magic={magic}";
                        var rsp = await h.PostAsync(url, jsonContent);
                        if (rsp.IsSuccessStatusCode)
                        {
                            Log($"Discovered:{item}");
                            var info = await rsp.Content.ReadFromJsonAsync<DevicesInfo>(sourceGenOptions);

                            if (info != null &&
                                !string.IsNullOrWhiteSpace(info.Name) &&
                                !string.IsNullOrWhiteSpace(info.udid) &&
                                !string.IsNullOrWhiteSpace(info.AudioCopyVersion) &&
                                !string.IsNullOrWhiteSpace(info.DeviceModel))
                            {
                                Log($"Discovered:{info.Name} : {info.udid}, AudioCopy:v{info.AudioCopyVersion}");
                                lock(addLocker)
                                {
                                    DiscoveredClients.AddOrUpdate(info.udid, info, (_, _) => info);
                                    if (!DiscoveredClientsAddress.ContainsKey(info.udid))
                                    {
                                        DiscoveredClientsAddress.TryAdd(info.udid, Enumerable.ToList([item]));
                                    }
                                    else
                                    {
                                        DiscoveredClientsAddress[info.udid].Add(item);
                                    }

                                }
                            }

                        }
//#if DEBUG
//                        else
//                        {
//                            Log($"Failed to discover {item}:{await rsp.Content.ReadAsStringAsync()}");
//                        }
//#endif
                    }
                    catch (Exception)
                    {
                        //Log($"{item} is not discoverable.");
                    }
                    finally
                    {
                        semaphore1.Release();
                    }
                }));
            }
            await Task.WhenAll(tasks1);

        }

        public static async Task<HttpResponseMessage> ManualDiscover(string addr)
        {
            HttpClient h = new();
            h.Timeout = TimeSpan.FromSeconds(3);
            //Log($"Discovering:{item}");
            var jsonContent = new StringContent(JsonSerializer.Serialize(thisDevice, sourceGenOptions), Encoding.UTF8, "application/json");
            string now = DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm");
            string helloHash = SecretProvider.ComputeSHA256WithSecret("Hello AudioCopy!");
            string magic = SecretProvider.ComputeSHA256WithSecret($"{now}/{identify}/{BackendAPIVersion}/{helloHash}");
            string url = $"http://{addr}/api/device/Discover?identify={Uri.EscapeDataString(identify)}&magic={magic}";
            return await h.PostAsync(url, jsonContent);
        }

        [DebuggerNonUserCode()]
        private static List<string> GetLocalNetworkAddresses()
        {
            List<string> addresses = new();

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
                    foreach (var unicastAddress in ipProperties.UnicastAddresses)
                    {
                        if (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            var ipAddress = unicastAddress.Address;
                            var subnetMask = unicastAddress.IPv4Mask;

                            if (subnetMask != null)
                            {
                                var localNetworkAddresses = GetAllIPsInSubnet(ipAddress, subnetMask);
                                addresses.AddRange(localNetworkAddresses);
                            }
                        }
                    }
                }
            }

            return addresses.OrderBy((s) => s.StartsWith("192.168.") ? 0 : 1).Distinct().ToList();
        }

        private static IEnumerable<string> GetAllIPsInSubnet(IPAddress ipAddress, IPAddress subnetMask)
        {
            List<string> ips = new();

            var ipBytes = ipAddress.GetAddressBytes();
            var maskBytes = subnetMask.GetAddressBytes();

            if (ipBytes.Length != maskBytes.Length)
                throw new ArgumentException("IP address and subnet mask lengths do not match.");

            var networkAddressBytes = new byte[ipBytes.Length];
            for (int i = 0; i < ipBytes.Length; i++)
            {
                networkAddressBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
            }

            var broadcastAddressBytes = new byte[ipBytes.Length];
            for (int i = 0; i < ipBytes.Length; i++)
            {
                broadcastAddressBytes[i] = (byte)(networkAddressBytes[i] | ~maskBytes[i]);
            }

            var networkAddress = new IPAddress(networkAddressBytes);
            var broadcastAddress = new IPAddress(broadcastAddressBytes);

            var currentAddress = networkAddressBytes;
            while (!currentAddress.SequenceEqual(broadcastAddressBytes))
            {
                ips.Add(new IPAddress(currentAddress).ToString());

                for (int i = currentAddress.Length - 1; i >= 0; i--)
                {
                    if (++currentAddress[i] != 0)
                        break;
                }
            }
            //ips.Add(ipAddress.ToString()); 

            return ips;
        }
    }
}
