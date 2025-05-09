 /*
 *	 File: AudioController.cs
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
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using NAudio.CoreAudioApi;
using NAudio.Lame;
using NAudio.Wave;
using System.Diagnostics;
using System.Net;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.Json;

[ApiController]
[Route("api/audio")]
public class AudioController : ControllerBase
{
    private AudioProvider _provider;
    private readonly TokenService _tokens;

    public AudioController(AudioProvider provider, TokenService tokens)
    {
        string? name,format = "";
        int id = -1;
        if ((name = Environment.GetEnvironmentVariable("AudioCopy_DefaultDeviceName")) is not null || (format = Environment.GetEnvironmentVariable("AudioCopy_DefaultAudioQuality")) is not null)
        {
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            IEnumerable<string> clients = devices.Select((d) => $"{d.DeviceFriendlyName}");
            id = Array.IndexOf(clients.ToArray(), name);

            string[]? fmtArr = (format is not null && !string.IsNullOrWhiteSpace(format)) ? format.Split(',') : Array.Empty<string>();

            _provider = new(
                (fmtArr.Length == 3) ? new WaveFormat(int.Parse(fmtArr[0]), int.Parse(fmtArr[1]), int.Parse(fmtArr[2])) : null,
                id);

        }
        else
        {
            _provider = provider;
        }

        _tokens = tokens;
    }

    private bool CheckToken(string? token)
    {
        return _tokens.Validate(token);
    }

    private bool IsHostTokenVaild(string hostToken) => (Environment.GetEnvironmentVariable("AudioCopy_hostToken") ?? "") == hostToken;


    [HttpGet("GetListeningClient")]
    public async Task GetLiteningClients(string token,CancellationToken ct)
    {
        if (!CheckToken(token))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Response.WriteAsync("Unauthorized, please check your token.");
            return;
        }
        List<string> body = new();
        foreach (var item in _provider.SubscribedClients.Values)
        {
            body.Add($"{item.Item1}@{item.Item2}");
        }
        body.Add("none@none"); //∑¿÷πø’÷µµº÷¬“Ï≥£
        Response.ContentType = "application/json";
        await Response.WriteAsJsonAsync(body.ToArray(), ct);
    }


    [HttpPut("SetCaptureOptions")]
    public async Task SetCaptureOptions(int deviceId = -1, string? format = "", string hostToken = "", CancellationToken ct = default)
    {
        if (!IsHostTokenVaild(hostToken))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Response.WriteAsync("Unauthorized, please check your token.");
            return;
        }
        _provider.Dispose();
        string[]? fmtArr = (format is not null && !string.IsNullOrWhiteSpace(format)) ? format.Split(',') : Array.Empty<string>();

        _provider = new AudioProvider(
            (fmtArr.Length == 3) ? new WaveFormat(int.Parse(fmtArr[0]), int.Parse(fmtArr[1]), int.Parse(fmtArr[2])) : null,
            deviceId);

        return;

    }

    [HttpGet("GetAudioFormat")]
    public async Task GetAudioFormat(string token, CancellationToken ct)
    {
        if (!CheckToken(token) && !IsHostTokenVaild(token))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Response.WriteAsync("Unauthorized, please check your token.");
            return;
        }
        var format = _provider.PcmFormat;
        var json = new
        {
            format.SampleRate,
            format.BitsPerSample,
            format.Channels,
            _provider.isMp3Ready
        };
        Response.ContentType = "application/json";
        await Response.WriteAsJsonAsync(json, ct);
    }

    [HttpGet("mp3")]
    public async Task StreamMp3(string token, bool force = false, string clientName = "", CancellationToken ct = default)
    {
        if (!CheckToken(token))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Response.WriteAsync("Unauthorized, please check your token.");
            return;
        }

        if (!_provider.isMp3Ready || force)
        {
            Response.StatusCode = StatusCodes.Status406NotAcceptable;
            await Response.WriteAsync("Enable resample or use force=true argument to continue get streamed MP3 audio.");
            return;
        }

        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering(); 
        Response.ContentType = "audio/mpeg";

        var (id, pipe) = _provider.SubscribePcm((HttpContext.Connection.RemoteIpAddress ?? IPAddress.Any).ToString().Split(':').Last(),clientName);

        try
        {
            using var mp3Writer = new LameMP3FileWriter(Response.Body, _provider.PcmFormat, 128);

            var buffer = new byte[_provider.PcmBlockAlign * 16]; 
            while (!ct.IsCancellationRequested)
            {
                int n = await pipe.ReadAsync(buffer, 0, buffer.Length, ct);
                if (n <= 0)
                {
                    await Task.Delay(20, ct);
                    continue;
                }

                mp3Writer.Write(buffer, 0, n);

                await Response.Body.FlushAsync(ct);
            }
        }
        finally
        {
            _provider.UnsubscribePcm(id);
        }
    }


    [HttpGet("wav")]
    public async Task StreamWav(string token, string clientName, CancellationToken ct)
    {
        if (!CheckToken(token))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Response.WriteAsync("Unauthorized, please check your token.");
            return;
        }
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        Response.ContentType = "audio/wav";
        Response.Headers["Accept-Ranges"] = "bytes";  

        int channels = _provider.PcmFormat.Channels;
        int sampleRate = _provider.PcmFormat.SampleRate;
        int bitsSample = _provider.PcmFormat.BitsPerSample;
        int byteRate = sampleRate * channels * bitsSample / 8;
        short blockAlign = (short)(channels * bitsSample / 8);

        await Response.Body.WriteAsync(Encoding.ASCII.GetBytes("RIFF"), ct);
        await Response.Body.WriteAsync(BitConverter.GetBytes(uint.MaxValue), ct);
        await Response.Body.WriteAsync(Encoding.ASCII.GetBytes("WAVEfmt "), ct);
        await Response.Body.WriteAsync(BitConverter.GetBytes(16), ct);
        await Response.Body.WriteAsync(BitConverter.GetBytes((short)1), ct);
        await Response.Body.WriteAsync(BitConverter.GetBytes((short)channels), ct);
        await Response.Body.WriteAsync(BitConverter.GetBytes(sampleRate), ct);
        await Response.Body.WriteAsync(BitConverter.GetBytes(byteRate), ct);
        await Response.Body.WriteAsync(BitConverter.GetBytes(blockAlign), ct);
        await Response.Body.WriteAsync(BitConverter.GetBytes((short)bitsSample), ct);
        await Response.Body.WriteAsync(Encoding.ASCII.GetBytes("data"), ct);
        await Response.Body.WriteAsync(BitConverter.GetBytes(uint.MaxValue), ct);

        var (id, pipe) = _provider.SubscribePcm((HttpContext.Connection.RemoteIpAddress ?? IPAddress.Any).ToString().Split(':').Last(), clientName);
        try
        {
            var buffer = new byte[_provider.PcmBlockAlign * 16];
            while (!ct.IsCancellationRequested)
            {
                int n = await pipe.ReadAsync(buffer, 0, buffer.Length, ct);
                if (n <= 0) { await Task.Delay(20, ct); continue; }
                await Response.Body.WriteAsync(buffer, 0, n, ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        finally { _provider.UnsubscribePcm(id); }
    }

    [HttpGet("flac")]
    public async Task StreamFlac(string token, string clientName, CancellationToken ct)
    {
        if (!CheckToken(token))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Response.WriteAsync("Unauthorized, please check your token.");
            return;
        }

        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        Response.ContentType = "audio/ogg";

        var (id, pipe) = _provider.SubscribePcm((HttpContext.Connection.RemoteIpAddress ?? IPAddress.Any).ToString().Split(':').Last(), clientName);
        Process flacProc = null;
        try
        {
            var i = new ProcessStartInfo
            {
                FileName = @"flac.exe",
                Arguments = "--best --ogg --force-raw-format --endian=little " +
                             $"--sign=signed --channels={_provider.channels} --bps={_provider.bitRate} --sample-rate={_provider.sampleRate} --stdout -",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            flacProc = Process.Start(i)!;

            _ = Task.Run(async () =>
            {
                var buf = new byte[_provider.PcmBlockAlign * 16];
                while (!ct.IsCancellationRequested)
                {
                    int n = await pipe.ReadAsync(buf, 0, buf.Length, ct);
                    if (n > 0) flacProc.StandardInput.BaseStream.Write(buf, 0, n);
                    else await Task.Delay(20, ct);
                }
                flacProc.StandardInput.Close();
            }, ct);

            var outStream = flacProc.StandardOutput.BaseStream;
            var obuf = new byte[8192];
            while (!ct.IsCancellationRequested)
            {
                int m = await outStream.ReadAsync(obuf, 0, obuf.Length, ct);
                if (m > 0) await Response.Body.WriteAsync(obuf, 0, m, ct);
                else await Task.Delay(20, ct);
            }
        }
        finally
        {
            flacProc?.Kill(true);
            _provider.UnsubscribePcm(id);
        }
    }

    [HttpGet("raw")]
    public async Task StreamRaw(string token, string clientName, CancellationToken ct = default)
    {
        if (!CheckToken(token))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Response.WriteAsync("Unauthorized, please check your token.");
            return;
        }

        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        Response.ContentType = "application/octet-stream";
        var (id, pipe) = _provider.SubscribePcm((HttpContext.Connection.RemoteIpAddress ?? IPAddress.Any).ToString().Split(':').Last(), clientName);
        await Task.Delay(500);
        try
        {
            byte[] buffer = new byte[_provider.PcmBlockAlign * 16];
            int n;
            while (!ct.IsCancellationRequested)
            {
                n = pipe.Read(buffer, 0, buffer.Length);
                if (n > 0)
                {
                    //Console.WriteLine(result);
                    await Response.Body.WriteAsync(buffer.AsMemory(0, n), ct);
                }
                else
                {
                    await Task.Delay(20, ct);
                }
            }
        }
        finally
        {
            _provider.UnsubscribePcm(id);
        }
    }
}

