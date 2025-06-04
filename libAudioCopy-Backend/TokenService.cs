/*
*	 File: TokenService.cs
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

using Microsoft.AspNetCore.Hosting;
using System.Text.Json;

namespace libAudioCopy_Backend
{
    public class TokenService
    {
        private readonly string _filePath;
        private readonly HashSet<string> _tokens;
        private readonly object _lock = new();
        public bool Presiet = true;

        public TokenService(IWebHostEnvironment env)
        {
            if (Presiet)
            {
                _filePath = Path.Combine(env.ContentRootPath, "tokens.json");
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    var list = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                    _tokens = new HashSet<string>(list);
                }
                else
                {
                    _tokens = new HashSet<string>();
                }
            }
            else
            {
                _tokens = new HashSet<string>();
            }
        }

        public bool Validate(string token)
            => ((Environment.GetEnvironmentVariable("AudioCopy_hostToken") ?? throw new Exception("Not defined host token.")) == token) || (!string.IsNullOrEmpty(token) && _tokens.Contains(token));

        public bool Add(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            lock (_lock)
            {
                var added = _tokens.Add(token);
                if (added) Save();
                return added;
            }
        }

        public bool Remove(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            lock (_lock)
            {
                var removed = _tokens.Remove(token);
                if (removed) Save();
                return removed;
            }
        }

        public IEnumerable<string> List()
        {
            lock (_lock) return _tokens.ToArray();
        }

        private void Save()
        {
            if (!Presiet) return;
            var json = JsonSerializer.Serialize(_tokens);
            File.WriteAllText(_filePath, json);
        }
    }
}

