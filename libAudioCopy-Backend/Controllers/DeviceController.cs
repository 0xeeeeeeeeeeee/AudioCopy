/*
*	 File: DeviceController.cs
*	 Website: https://github.com/0xeeeeeeeeeeee/AudioCopy
*	 Copyright 2024-2025 (C) 0xeeeeeeeeeeee (0x12e)
*
*   This file is part of AudioCopy
*	 
*	 AudioCopy is free software: you can redistribute it and/or modify
*	 it under the terms of the GNU General Public License as published by
*	 the Free Software Foundation, either version 2 of the License, or
*	 (at your option) any later version.
*	 
*	 AudioCopy is distributed in the hope that it will be useful,
*	 but WITHOUT ANY WARRANTY; without even the implied warranty of
*	 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
*	 GNU General Public License for more details.
*	 
*	 You should have received a copy of the GNU General Public License
*	 along with AudioCopy. If not, see <http://www.gnu.org/licenses/>.
*/

using libAudioCopy;
using libAudioCopy.Audio;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NAudio.CoreAudioApi;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace libAudioCopy_Backend.Controllers;

[ApiController]
[Route("api/device")]
public class DeviceController : Controller
{
    private readonly TokenService _tokens;
    private readonly ConcurrentBag<MediaInfo> _SMTCInfo;
    public DeviceController(TokenService tokens, ConcurrentBag<MediaInfo> SMTCInfo)
    {
        _tokens = tokens;
        _SMTCInfo = SMTCInfo;
    }

    private bool CheckToken(string? token)
    {
        return _tokens.Validate(token);
    }

    private bool IsHostTokenVaild(string hostToken) => (Environment.GetEnvironmentVariable("AudioCopy_hostToken") ?? "") == hostToken;


    [HttpGet("RebootClient")]
    public IActionResult RebootClient(string hostToken,int delay)
    {
        if (!IsHostTokenVaild(hostToken))
        {
            return Unauthorized("Unauthorized, please check your token.");
        }

        Thread.Sleep(delay);

        Process.Start(new ProcessStartInfo { FileName = "cmd.exe",Arguments= "/c start audiocopy:reboot" , UseShellExecute = false });
        
        return Ok("Rebooting client...");
    }

    [HttpGet("GetIPAddress")]
    public IActionResult GetIPAddress(string token)
    {
        if (!CheckToken(token))
        {
            return Unauthorized("Unauthorized, please check your token.");
        }

        return Ok(GetLocalNetworkAddresses());
    }

    [HttpGet("GetAudioDevices")]
    public IActionResult GetAudioDevices(string token)
    {
        if (!CheckToken(token))
        {
            return Unauthorized("Unauthorized, please check your token.");
        }
        var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        IEnumerable<string> clients = devices.Select((d) => $"{d.DeviceFriendlyName}");
        
        return Ok(clients);
    }

    [HttpGet("GetListeningClient")]
    public async Task GetLiteningClients(string token, CancellationToken ct)//避免反复调用创建AudioProvider，造成卡顿
    {
        if (!CheckToken(token))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Response.WriteAsync("Unauthorized, please check your token.");
            return;
        }
        List<string> body = AudioProvider.ListeningClients.ToList();
        
        body.Add("none@none"); //防止空值导致异常
        Response.ContentType = "application/json";
        await Response.WriteAsJsonAsync(body.ToArray(), ct);
    }

    [HttpGet("testState")]
    public IActionResult TestState(string token)
    {
        if (!CheckToken(token))
        {
            return Unauthorized("Unauthorized, please check your token.");
        }

        return Ok("Valid");
    }

    [HttpPost("UploadSMTCInfo")]
    public async Task<IActionResult> UploadSMTCInfo(string hostToken,MediaInfo info)
    {
        if (!IsHostTokenVaild(hostToken))
        {
            return Unauthorized("Unauthorized, please check your token.");
        }

        _SMTCInfo.Clear();

        _SMTCInfo.Add(info);

        return Ok();
    }

    [HttpGet("GetSMTCInfo")]
    public IActionResult GetSMTCInfo(string token)
    {
        if (!CheckToken(token))
        {
            return Unauthorized("Unauthorized, please check your token.");
        }
        if(_SMTCInfo.TryTake(out var body))
        {
            _SMTCInfo.Add(body);

            MediaInfo body1 = JsonSerializer.Deserialize<MediaInfo>(JsonSerializer.Serialize(body)); //隔离，防止莫名其妙的问题
            body1.AlbumArtBase64 = null;
            return Ok(body1);

        }

        return BadRequest();
    }

    [HttpGet("GetAlbumPhoto")]
    public IActionResult GetSMTCAlbumPhoto(string? token = null)
    {
        try
        {


            if (token is null || !CheckToken(token))
            {
                goto fallback;
            }
            if (_SMTCInfo.TryTake(out var body))
            {
                _SMTCInfo.Add(body);
                if (body != null && !string.IsNullOrEmpty(body.AlbumArtBase64))
                {
                    string base64 = body.AlbumArtBase64;
                    var bytes = Convert.FromBase64String(base64);
                    var head = bytes.Take(64);
                    var headStr = Encoding.UTF8.GetString(head.ToArray());
                    if (headStr.Contains("PNG")) return File(bytes, "image/png", "album.png");
                    return File(bytes, "image/jpeg", "album.jpg");
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"A {ex.GetType().Name} error happens:{ex.Message}");
        }

    fallback:
            return File(
                System.IO.File.ReadAllBytes(
                    Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Environment.CurrentDirectory,
                    "AudioCopy.png")), "image/png", "AudioCopy.png"); //返回一个默认值


    }

    [HttpGet("/index")]
    public async Task Index(string? token = "")
    {
        Response.ContentType = "text/html";
        if (token is null || !CheckToken(token))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            string html1 =
$"""
<!DOCTYPE html><html><head><meta charset='utf-8'/>
    <title>AudioCopy</title>
</head>
<body>
    <img src="api/device/GetAlbumPhoto" width="100" height="100">
    <br>   
    <a href="https://github.com/0xeeeeeeeeeeee/AudioCopy">AudioCopy</a>
</body>
</html>
""";
            await Response.BodyWriter.WriteAsync(Encoding.UTF8.GetBytes(html1));
            return;
        }
        string html = @$"
<!DOCTYPE html><html><head><meta charset='utf-8'/><title>音频流</title></head><body>
  <h3>MP3 流</h3><audio controls autoplay src='/api/audio/mp3?token={token}'></audio>
  <h3>WAV 流</h3><audio controls src='/api/audio/wav?token={token}'></audio>
  <h3>FLAC 流</h3><audio controls src='/api/audio/flac?token={token}'></audio>
</body></html>";
        await Response.BodyWriter.WriteAsync(Encoding.UTF8.GetBytes(html));
    }


    private static List<string> GetLocalNetworkAddresses()
    {
        List<string> address = new();
        bool IsLocalNetwork(string ipAddress)
        {
            return ipAddress.StartsWith("192.168.") || ipAddress.StartsWith("10.") ||
                   ipAddress.StartsWith("172.") && int.TryParse(ipAddress.Split('.')[1], out int secondOctet) && secondOctet >= 16 && secondOctet <= 31;
        }

        var interfaces = NetworkInterface.GetAllNetworkInterfaces();

        foreach (var networkInterface in interfaces)
        {
            if (networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                !networkInterface.Description.Contains("Vmware", StringComparison.OrdinalIgnoreCase) &&
                !networkInterface.Description.Contains("VirtualBox", StringComparison.OrdinalIgnoreCase))
            {
                var ipProperties = networkInterface.GetIPProperties();
                foreach (var ipAddress in ipProperties.UnicastAddresses)
                {
                    if (ipAddress.Address.AddressFamily == AddressFamily.InterNetwork &&
                        IsLocalNetwork(ipAddress.Address.ToString()))
                    {
                        address.Add(ipAddress.Address.ToString());
                    }
                }
            }
        }
        return address;
    }

    public class MediaInfo
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        public string AlbumArtist { get; set; }
        public string AlbumTitle { get; set; }
        public string PlaybackType { get; set; }
        public string? AlbumArtBase64 { get; set; }
    }

}

