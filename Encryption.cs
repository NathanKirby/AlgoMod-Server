using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AlgoModPatreonServer
{
    internal class Encryption
    {
        /// <summary>
        /// Obfuscates IDs list for added privacy.
        /// </summary>
        /// <remarks>This was coded in a way to allow easy cross-language decryption.</remarks>
        /// <returns>Returns cleaned obfuscated IDs.</returns>
        public static string EncryptIDS(string input)
        {
            string ecnryptedText = string.Empty;
            string key = Variables.IDSKey;

            try
            {
                Random randomInt = new();
                char[] inputChars = input.ToCharArray();
                char[] possibleChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()_+-=[]{};:,.<>?".ToCharArray();
                int ki = 0;

                foreach (char c in inputChars)
                {
                    string newPart;
                    string RandomChars = string.Empty;

                    if (ki == key.Length)
                    {
                        ki = 0;
                    }

                    int ri = 0;
                    int currentKey = int.Parse(key[ki].ToString());

                    while (ri <= currentKey)
                    {
                        // Adds random char
                        RandomChars += possibleChars[randomInt.Next(0, possibleChars.Length)];

                        ri++;
                    }

                    newPart = RandomChars + c;
                    ecnryptedText += newPart;

                    ki++;
                }
            }
            catch (Exception ex)
            {
                PatreonServer.Log($"!!!Error: EncryptIDS: {ex.Message}\n");
            }

            return PatreonServer.CleanString(ecnryptedText);
        }


        /// <summary>
        /// Uses the IDs key to parse plain text from obfuscated text.
        /// </summary>
        /// <returns>Returns cleaned plain IDs text.</returns>
        public static string DecryptIDS(string encryptedText)
        {
            string decryptedText = string.Empty;
            string key = Variables.IDSKey;
            int ki = 0;

            try
            {
                // Processes text untill it is all gone
                while (!string.IsNullOrEmpty(encryptedText))
                {
                    if (ki >= key.Length)
                    {
                        ki = 0;
                    }

                    // Gets current key value and adds one to accoutn for index
                    int currentKey = int.Parse(key[ki].ToString()) + 1;

                    // Removes chars from the beginning of text based on the key int
                    encryptedText = encryptedText.Substring(currentKey);

                    // Adds the first char of remaining string to output
                    decryptedText += encryptedText[0].ToString();

                    // Removes the first char it just added to output
                    encryptedText = encryptedText[1..];

                    ki++;
                }
            }
            catch (Exception ex)
            {
                PatreonServer.Log($"!!!Error: DecryptIDS: {ex.Message}\n");
            }

            return PatreonServer.CleanString(decryptedText);
        }


        /// <summary>
        /// Uses AES to decrypt messages received from client.
        /// </summary>
        /// <returns>Returns plain text of client message.</returns>
        public static string DecryptMessage(string encryptedData)
        {
            string plainText = string.Empty;

            try
            {
                // Get key and IV as byte array for use in decryption
                byte[] key = Encoding.ASCII.GetBytes(Variables.MessageKey);
                byte[] IV = Encoding.ASCII.GetBytes(Variables.MessageIV);

                using Aes aes = Aes.Create();

                // Configure decryptor
                ICryptoTransform decryptor = aes.CreateDecryptor(key, IV);

                // Get ecrypted string as byte array
                byte[] encryptedBytes = Convert.FromBase64String(encryptedData);

                // Stream bytes to memory
                using MemoryStream memoryStream = new(encryptedBytes);

                // Setup cryptography stream with decryptor to allow reading of plain text
                using CryptoStream cryptoStream = new(memoryStream, decryptor, CryptoStreamMode.Read);

                // Read plain text
                using StreamReader reader = new(cryptoStream);
                plainText = reader.ReadToEnd();

            }
            catch (Exception ex)
            {
                PatreonServer.Log($"!!!Error: DecryptMessage: {ex.Message}\n");
            }

            return plainText;
        }
    }
}
