//Replace false to true if you needed to compile the code by yourself.
#if false
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AudioCopyUI_Backend
{
    internal class SecretProvider
    {
        private static string Secret = "Example";

        [DebuggerNonUserCode()]
        public static string ComputeSHA256WithSecret(string src)
        {
            var input = src + Secret;
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
    }
}
#endif