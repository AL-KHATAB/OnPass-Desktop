using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using OnPass.Domain;
using OnPass.Infrastructure.Security;

namespace OnPass.Infrastructure.Storage
{
    public static class PasswordStorage
    {
        public static void SaveEncryptedPasswords(byte[] encryptedData, byte[] iv, string filePath)
        {
            // The file format is IV + ciphertext so decryption can recover the IV
            // without keeping extra metadata files beside the vault.
            byte[] combinedData = new byte[iv.Length + encryptedData.Length];
            Buffer.BlockCopy(iv, 0, combinedData, 0, iv.Length);
            Buffer.BlockCopy(encryptedData, 0, combinedData, iv.Length, encryptedData.Length);
            File.WriteAllBytes(filePath, combinedData);
        }

        public static (byte[] encryptedData, byte[] iv) ReadEncryptedData(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Password file not found", filePath);
            }
            byte[] fileData = File.ReadAllBytes(filePath);
            if (fileData.Length < 16) // IV size is 16 bytes
            {
                throw new InvalidDataException("Password file is corrupted");
            }
            byte[] iv = new byte[16];
            byte[] encryptedData = new byte[fileData.Length - 16];
            Buffer.BlockCopy(fileData, 0, iv, 0, 16);
            Buffer.BlockCopy(fileData, 16, encryptedData, 0, encryptedData.Length);
            return (encryptedData, iv);
        }

        public static string SerializePasswords(List<PasswordItem> passwords)
        {
            return JsonSerializer.Serialize(passwords);
        }

        public static List<PasswordItem> DeserializePasswords(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new List<PasswordItem>();
            }
            try
            {
                return JsonSerializer.Deserialize<List<PasswordItem>>(json) ?? new List<PasswordItem>();
            }
            catch
            {
                return new List<PasswordItem>();
            }
        }

        public static void SavePasswords(List<PasswordItem> passwords, string username, byte[] key)
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string OnPassPath = Path.Combine(appDataPath, "OnPass");
            string passwordsFilePath = Path.Combine(OnPassPath, $"passwords_{username}.dat");

            // Password entries are always serialized as one encrypted blob so the
            // storage layer only exposes plaintext while the user session is active.
            string json = SerializePasswords(passwords);
            byte[] iv = AesEncryption.GenerateIV();
            byte[] encryptedData = AesEncryption.Encrypt(json, key, iv);
            SaveEncryptedPasswords(encryptedData, iv, passwordsFilePath);
        }

        public static List<PasswordItem> LoadPasswords(string username, byte[] key)
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string OnPassPath = Path.Combine(appDataPath, "OnPass");
                string passwordsFilePath = Path.Combine(OnPassPath, $"passwords_{username}.dat");

                if (!File.Exists(passwordsFilePath))
                {
                    return new List<PasswordItem>();
                }

                var (encryptedData, iv) = ReadEncryptedData(passwordsFilePath);
                string json = AesEncryption.Decrypt(encryptedData, key, iv);
                return DeserializePasswords(json);
            }
            catch (Exception)
            {
                return new List<PasswordItem>();
            }
        }
    }
}
