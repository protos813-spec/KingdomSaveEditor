/*
    Kingdom Save Editor
    Copyright (C) 2026 Mikko Mäntylä (BFlorry)

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

/*
    KH3 PC (Steam / Epic Games Store) save container.

    Layout of KHIII_slot*.bin / KHIII_system.bin:

        [ AES-256-ECB ciphertext, N bytes, N % 16 == 0 ]
        [ 0x08 ]
        [ MD5 of plaintext[0 .. FileSize+0x10) ]

    The AES key is derived from the platform account ID: the SteamID64 for
    Steam and the 32-character account ID for Epic, both of which are also the
    name of the folder that contains "SaveGames\kh3sv2".

    Key derivation originally shared as a PowerShell script by dedede123 and
    fungualtissue1230 on the OpenKH Discord.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KHSave.Lib3
{
    public static class SaveKh3PcCrypto
    {
        public const int TrailerLength = 17;
        public const byte TrailerMarker = 0x08;
        private const int MagicCode = 0x45764053; // "S@vE"
        private const int BlockSize = 16;

        private static readonly byte[] KeyMask = Encoding.ASCII.GetBytes("hN96q4X9f%BCURBV&pMT4kcvqTMhHYD&");
        private static readonly byte[] KeyTable = Encoding.ASCII.GetBytes(
            "ABCDE!#$%&FGHIJ012345KLMNOPqrstuvwxyzQRSTUVWXYZ6789abcdefgh},.<>ijklmnop()=~|-^+*;:[]{/?_@");

        private static readonly Regex AccountIdRegex = new Regex(@"^[0-9A-Za-z_-]+$", RegexOptions.Compiled);
        private const string SaveGamesFolder = "SaveGames";
        private static readonly char[] PathSeparators = { '\\', '/' };

        public static byte[] DeriveKey(string accountId)
        {
            if (string.IsNullOrEmpty(accountId))
                throw new ArgumentException("Account ID must not be empty", nameof(accountId));

            var account = Encoding.UTF8.GetBytes(accountId);
            var key = new byte[32];
            for (var i = 0; i < key.Length; i++)
            {
                var idx = (KeyMask[i] ^ account[(i + 1) % account.Length]) % KeyTable.Length;
                key[i] = KeyTable[idx];
            }

            return key;
        }

        public static bool IsValidAccountId(string accountId) =>
            !string.IsNullOrEmpty(accountId) && AccountIdRegex.IsMatch(accountId);

        /// <summary>
        /// Looks for the account ID in the save path: it is the folder that contains "SaveGames", e.g.
        /// ...\KINGDOM HEARTS III\Steam\76561197999624471\SaveGames\kh3sv2\data\KHIII_slot0.bin
        /// ...\KINGDOM HEARTS III\Epic Games Store\ec588173027141ca830c671ff0914555\SaveGames\kh3sv2\...
        /// </summary>
        public static string TryGetAccountIdFromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            var parts = path.Split(PathSeparators);

            for (var i = parts.Length - 2; i >= 1; i--)
            {
                if (!string.Equals(parts[i], SaveGamesFolder, StringComparison.OrdinalIgnoreCase))
                    continue;

                var accountId = parts[i - 1];
                return IsValidAccountId(accountId) ? accountId : null;
            }

            return null;
        }

        /// <summary>
        /// Account IDs of the KH3 installs found in the local Documents folder.
        /// </summary>
        public static IEnumerable<string> FindLocalAccountIds()
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var roots = new[]
            {
                Path.Combine(documents, "My Games", "KINGDOM HEARTS III", "Steam"),
                Path.Combine(documents, "KINGDOM HEARTS III", "Epic Games Store"),
            };

            foreach (var root in roots)
            {
                if (!Directory.Exists(root))
                    continue;
                foreach (var dir in Directory.EnumerateDirectories(root))
                {
                    var name = Path.GetFileName(dir);
                    if (IsValidAccountId(name) && Directory.Exists(Path.Combine(dir, SaveGamesFolder)))
                        yield return name;
                }
            }
        }

        /// <summary>
        /// Returns the first candidate account ID whose key decrypts the save header.
        /// </summary>
        public static string TryFindAccountId(Stream stream, IEnumerable<string> candidates)
        {
            var prevPosition = stream.Position;
            var block = new byte[BlockSize];
            stream.SetPosition(0).ReadExactly(block, 0, BlockSize);
            stream.Position = prevPosition;

            var plain = new byte[BlockSize];
            foreach (var accountId in candidates)
            {
                using (var aes = CreateAes(accountId))
                    aes.DecryptEcb(block, plain, PaddingMode.None);
                if (BitConverter.ToInt32(plain, 0) == MagicCode)
                    return accountId;
            }

            return null;
        }

        public static bool IsEncrypted(Stream stream)
        {
            var length = stream.Length;
            if (length < TrailerLength + BlockSize || (length - TrailerLength) % BlockSize != 0)
                return false;

            var prevPosition = stream.Position;
            try
            {
                var reader = new BinaryReader(stream.SetPosition(0));
                if (reader.ReadInt32() == MagicCode)
                    return false;

                stream.Position = length - TrailerLength;
                return reader.ReadByte() == TrailerMarker;
            }
            finally
            {
                stream.Position = prevPosition;
            }
        }

        /// <summary>
        /// Decrypts a KH3 PC save container into a plain save.
        /// </summary>
        /// <exception cref="InvalidDataException">Wrong account ID or corrupted file.</exception>
        public static MemoryStream Decrypt(Stream stream, string accountId)
        {
            var length = (int)stream.Length;
            var cipherLength = length - TrailerLength;
            var data = new byte[length];
            stream.SetPosition(0).ReadExactly(data, 0, length);

            var plain = new byte[cipherLength];
            using (var aes = CreateAes(accountId))
                aes.DecryptEcb(new ReadOnlySpan<byte>(data, 0, cipherLength), plain, PaddingMode.None);

            if (BitConverter.ToInt32(plain, 0) != MagicCode)
                throw new InvalidDataException("Unable to decrypt the Kingdom Hearts III save: the account ID is wrong.");

            var expectedMd5 = new ReadOnlySpan<byte>(data, cipherLength + 1, 16);
            if (!expectedMd5.SequenceEqual(ComputeMd5(plain)))
                throw new InvalidDataException("The Kingdom Hearts III save decrypted, but its MD5 does not match: the file is corrupted.");

            return new MemoryStream(plain);
        }

        /// <summary>
        /// Encrypts a plain KH3 PC save into the container format.
        /// </summary>
        public static void Encrypt(Stream plainStream, Stream outStream, string accountId)
        {
            var length = (int)plainStream.Length;
            if (length % BlockSize != 0)
                throw new InvalidDataException($"Plain save length 0x{length:X} is not a multiple of {BlockSize}.");

            var plain = new byte[length];
            plainStream.SetPosition(0).ReadExactly(plain, 0, length);

            var cipher = new byte[length];
            using (var aes = CreateAes(accountId))
                aes.EncryptEcb(plain, cipher, PaddingMode.None);

            outStream.Write(cipher, 0, cipher.Length);
            outStream.WriteByte(TrailerMarker);
            outStream.Write(ComputeMd5(plain), 0, 16);
        }

        private static Aes CreateAes(string accountId)
        {
            var aes = Aes.Create();
            aes.Key = DeriveKey(accountId);
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            return aes;
        }

        private static byte[] ComputeMd5(byte[] plain)
        {
            var fileSize = BitConverter.ToInt32(plain, 4);
            var hashedLength = Math.Min(plain.Length, fileSize + 0x10);
            return MD5.HashData(new ReadOnlySpan<byte>(plain, 0, hashedLength));
        }
    }
}
