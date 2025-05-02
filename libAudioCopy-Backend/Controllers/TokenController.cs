 /*
 *	 File: TokenController.cs
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

using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace libAudioCopy.Controllers
{
    [ApiController]
    [Route("api/token")]
    public class TokenController : ControllerBase
    {
        private readonly TokenService _tokens;
        private readonly Dictionary<string, string> _pairList;
        private bool AllowLoopbackPair, AllowInternetPair;
        string StringTable = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";

        public TokenController(TokenService tokens, Dictionary<string, string> pairList)
        {
            _tokens = tokens;
            _pairList = pairList;
            AllowLoopbackPair = bool.Parse(Environment.GetEnvironmentVariable("AudioCopy_AllowLoopbackPair") ?? "False");
            AllowInternetPair = bool.Parse(Environment.GetEnvironmentVariable("AudioCopy_AllowInternetPair") ?? "False");
        }

        private void Forbid()
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        private bool IsLocalRequest()
        {
            var ip = HttpContext.Connection.RemoteIpAddress;
            return IPAddress.IsLoopback(ip);
        }

        private bool IsHostTokenVaild(string hostToken) => (Environment.GetEnvironmentVariable("AudioCopy_hostToken") ?? "") == hostToken;

        private bool IsSourceAddressValid(IPAddress ipAddress)
        {
            bool result = true;

            if (!IsLocalNetwork(ipAddress.ToString().Split(':').Last()))
            {
                result = AllowInternetPair;
            }

            return result;
        }

        private bool IsLocalNetwork(string ipAddress)
        {
            return ipAddress.StartsWith("192.168.") || ipAddress.StartsWith("10.") ||
                   (ipAddress.StartsWith("172.") && int.TryParse(ipAddress.Split('.')[1], out int secondOctet) && secondOctet >= 16 && secondOctet <= 31);
        }

        [HttpPost("add")]
        public IActionResult Add([FromQuery] string token, string hostToken)
        {
            if (!IsLocalRequest() || !IsHostTokenVaild(hostToken)) return Unauthorized("Unauthorized, please check your token.");
            if (!token.Aggregate(true,(a,b) => a && StringTable.Contains(b))) return BadRequest("包含非法字符");
            if (_tokens.Add(token)) return Created("", null);
            return BadRequest("已存在");
        }


        [HttpDelete("remove")]
        public IActionResult Remove([FromQuery] string token, string hostToken)
        {
            if (!IsLocalRequest() || !IsHostTokenVaild(hostToken)) return Unauthorized("Unauthorized, please check your token.");
            if (_tokens.Remove(token)) return NoContent();
            return NotFound();
        }

        [HttpGet("list")]
        public IActionResult List(string hostToken)
        {
            if (!IsLocalRequest() || !IsHostTokenVaild(hostToken)) return Unauthorized("Unauthorized, please check your token.");
            return Ok(_tokens.List());
        }

        [HttpGet("listPairing")]
        public IActionResult ListPairing(string hostToken)
        {
            if (!IsLocalRequest() || !IsHostTokenVaild(hostToken)) return Unauthorized("Unauthorized, please check your token.");
            return Ok(_pairList);
        }

        [HttpDelete("removePairing")]
        public IActionResult RemovePairing(string token, string hostToken)
        {
            if (!IsLocalRequest() || !IsHostTokenVaild(hostToken)) return Unauthorized("Unauthorized, please check your token.");
            if (_pairList.Remove(token, out _))
                return Ok();
            return BadRequest("设备不存在");
        }

        


        [HttpGet("/RequirePair")]
        public IActionResult Pair(string udid, string name)
        {
            if (udid == "AudioCopy")
            {
                return Ok("AudioCopy" + Environment.MachineName);
            }

            if (!IsSourceAddressValid(HttpContext.Connection.RemoteIpAddress))
            {
                return BadRequest("源不合法");
            }

            if (!udid.Aggregate(true, (a, b) => a && StringTable.Contains(b))) return BadRequest("包含非法字符");

            _pairList.Add(udid, name);
            return Created("", null);
        }
    }
}

