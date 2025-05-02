/*
*	 File: AlgorithmServices.cs
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




using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace AudioCopyUI
{
    public static class AlgorithmServices
    {
        [DebuggerNonUserCode()]
        public static async Task<string> ComputeFileSHA512Async(string fileName, CancellationToken? ct = null)
        {
            if (ct is null) ct = CancellationToken.None;
            string hashSHA512 = "";
            if (System.IO.File.Exists(fileName))
            {
                using (System.IO.FileStream fs = new System.IO.FileStream(fileName, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {

                    System.Security.Cryptography.SHA512 calculator = System.Security.Cryptography.SHA512.Create();
                    Byte[] buffer = await calculator.ComputeHashAsync(fs, (CancellationToken)ct);
                    calculator.Clear();

                    StringBuilder stringBuilder = new StringBuilder();
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        stringBuilder.Append(buffer[i].ToString("x2"));
                    }
                    hashSHA512 = stringBuilder.ToString();
                }
            }
            return hashSHA512;
        }

        [DebuggerNonUserCode()]
        public static async Task<string> ComputeFileSHA256Async(string fileName, CancellationToken? ct = null)
        {
            if (ct is null) ct = CancellationToken.None;
            string hashSHA512 = "";
            if (System.IO.File.Exists(fileName))
            {
                using (System.IO.FileStream fs = new System.IO.FileStream(fileName, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {

                    System.Security.Cryptography.SHA256 calculator = System.Security.Cryptography.SHA256.Create();
                    Byte[] buffer = await calculator.ComputeHashAsync(fs, (CancellationToken)ct);
                    calculator.Clear();

                    StringBuilder stringBuilder = new StringBuilder();
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        stringBuilder.Append(buffer[i].ToString("x2"));
                    }
                    hashSHA512 = stringBuilder.ToString();
                }
            }
            return hashSHA512;
        }

        public static string ComputeStringSHA512(string input)
        {
            using (SHA512 sha512 = SHA512.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(input);
                byte[] hash = sha512.ComputeHash(bytes);

                StringBuilder stringBuilder = new StringBuilder();
                for (int i = 0; i < hash.Length; i++)
                {
                    stringBuilder.Append(hash[i].ToString("x2"));
                }
                return stringBuilder.ToString();
            }
        }

        public static string ComputeStringSHA256ToBase64(string input)
        {
            using (SHA256 sha512 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(input);
                byte[] hash = sha512.ComputeHash(bytes);
                //Console.WriteLine(Convert.ToBase64String(hash));
                return Convert.ToBase64String(hash);
            }
        }

        private const string StringTable = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";

        public static bool AreArraysEqual<T>(T[] val1, T[] val2)
        {
            if (val1 is not IEnumerable<T> || val2 is not IEnumerable<T>) throw new NotSupportedException();
            if (val1.Length != val2.Length)
                return false;
            for (int i = 0; i < val1.Length; i++)
            {
                if (val1[i].Equals(val2[i]))
                    return false;
            }

            return true;
        }

        public static T[] AppendToArray<T>(T[] input, T newValue)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input == Array.Empty<T>()) return [newValue];
            var result = new T[input.Length + 1];
            Array.Copy(input, result, input.Length);
            result[input.Length] = newValue;
            return result;
        }

        [DebuggerNonUserCode()]
        public static string MakeRandString(int length) => string.Concat(Enumerable.Repeat(StringTable, length / StringTable.Length + 5)).OrderBy(x => Guid.NewGuid()).Take(length).Select(x => (char)x).Aggregate("", (x, y) => x + y);


    }
}
