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

using System;
using System.IO;
using KHSave.Lib3;
using KHSave.Lib3.Types;
using Xunit;

namespace KHSave.Tests
{
    public class Kh3PcCryptoTest
    {
        private const string Slot0Path = "Saves/kh3_steam_slot0.bin";
        private const string Slot1Path = "Saves/kh3_steam_slot1.bin";
        private const string SystemPath = "Saves/kh3_steam_system.bin";
        private const string AccountId = "76561198125911929";

        [Fact]
        public void DeriveKeyTest()
        {
            var key = SaveKh3PcCrypto.DeriveKey("76561197999624471");
            Assert.Equal(32, key.Length);
            Assert.Equal("Ew0$iIKA!rPM$HqI2ot%!AAj(4PEv4L2", System.Text.Encoding.ASCII.GetString(key));
        }

        [Theory]
        [InlineData(@"C:\Users\me\Documents\My Games\KINGDOM HEARTS III\Steam\76561197999624471\SaveGames\kh3sv2\data\KHIII_slot0.bin", "76561197999624471")]
        [InlineData(@"C:\Users\me\Documents\KINGDOM HEARTS III\Epic Games Store\ec588173027141ca830c671ff0914555\SaveGames\kh3sv2\data\KHIII_slot1.bin", "ec588173027141ca830c671ff0914555")]
        [InlineData(@"C:\Users\me\Documents\KINGDOM HEARTS III\Epic Games Store\4271\SaveGames\kh3sv2\data\KHIII_slot0.bin", "4271")]
        [InlineData(@"C:\Downloads\4271\SaveGames\kh3sv2\data\KHIII_slot0.bin", "4271")]
        [InlineData(@"C:\Downloads\KHIII_slot0.bin", null)]
        [InlineData(@"C:\Downloads\76561197999624471\KHIII_slot0.bin", null)]
        [InlineData(@"C:\SaveGames\KHIII_slot0.bin", null)]
        [InlineData("Saves/kh3.bin", null)]
        public void AccountIdFromPathTest(string path, string expected) =>
            Assert.Equal(expected, SaveKh3PcCrypto.TryGetAccountIdFromPath(path));

        [Theory]
        [InlineData("__data__slot0.bin", "KHIII_slot0.bin")]
        [InlineData("__data__slot1.bin", "KHIII_slot1.bin")]
        [InlineData(@"C:\Downloads\PS4 save\__data__slot2.bin", "KHIII_slot2.bin")]
        [InlineData("KHIII_system.bin", "KHIII_system.bin")]
        [InlineData("kh3.bin", "KHIII_slot0.bin")]
        [InlineData(null, "KHIII_slot0.bin")]
        public void SuggestPcFileNameTest(string sourceFileName, string expected) =>
            Assert.Equal(expected, SaveKh3PcCrypto.SuggestPcFileName(sourceFileName));

        // The 1.09 console layout is weirdly different (by 8 bytes), failing as expected
        [Theory]
        [InlineData(0x94F4F0, true)]  // PC
        [InlineData(0x94E8F0, true)]  // PS4 1.02
        [InlineData(0x94F308, false)] // PS4 1.09
        [InlineData(0, false)]
        public void CanEncryptTest(long plainLength, bool expected) =>
            Assert.Equal(expected, SaveKh3PcCrypto.CanEncrypt(plainLength));

        [Fact]
        public void ConvertConsoleSaveToPcTest() => File.OpenRead("Saves/kh3.bin").Using(stream =>
        {
            var save = SaveKh3.Read(stream);
            var plain = new MemoryStream();
            save.Write(plain);

            var encrypted = new MemoryStream();
            SaveKh3PcCrypto.Encrypt(plain, encrypted, AccountId);

            Assert.True(SaveKh3PcCrypto.IsEncrypted(encrypted));
            Assert.Equal(plain.ToArray(), SaveKh3PcCrypto.Decrypt(encrypted, AccountId).ToArray());
        });

        [Theory]
        [InlineData("76561197999624471", true)]
        [InlineData("ec588173027141ca830c671ff0914555", true)]
        [InlineData("4271", true)]
        [InlineData("", false)]
        [InlineData(" 4271", false)]
        [InlineData("a/b", false)]
        public void IsValidAccountIdTest(string accountId, bool expected) =>
            Assert.Equal(expected, SaveKh3PcCrypto.IsValidAccountId(accountId));

        [Fact]
        public void DeriveKeyShortIdTest() =>
            Assert.Equal("Ew0$iIKA!rPM$HqI2ot%!AAj(4PEv4L2".Length, SaveKh3PcCrypto.DeriveKey("4271").Length);

        [Theory]
        [InlineData(Slot0Path, true)]
        [InlineData(Slot1Path, true)]
        [InlineData(SystemPath, true)]
        [InlineData("Saves/kh3.bin", false)]
        [InlineData("Saves/kh3_109.bin", false)]
        public void IsEncryptedTest(string path, bool expected) =>
            File.OpenRead(path).Using(stream => Assert.Equal(expected, SaveKh3PcCrypto.IsEncrypted(stream)));

        [Fact]
        public void TryFindAccountIdTest() => File.OpenRead(Slot0Path).Using(stream =>
        {
            Assert.Equal(AccountId, SaveKh3PcCrypto.TryFindAccountId(stream, new[] { "76561197999624471", AccountId }));
            Assert.Null(SaveKh3PcCrypto.TryFindAccountId(stream, new[] { "76561197999624471" }));
        });

        [Fact]
        public void EncryptedSaveIsNotDirectlyValid() =>
            File.OpenRead(Slot0Path).Using(stream => Assert.False(SaveKh3.IsValid(stream)));

        [Fact]
        public void DecryptAndReadTest() => File.OpenRead(Slot0Path).Using(stream =>
        {
            var plain = SaveKh3PcCrypto.Decrypt(stream, AccountId);
            Assert.True(SaveKh3.IsValid(plain));

            var save = SaveKh3.Read(plain);
            Assert.IsType<SaveKh3PC>(save);
            Assert.Equal(0x94F4D8, save.FileSize);
            Assert.Equal(1, save.Level);
            Assert.Equal(64, save.Munny);
            Assert.Equal(337, save.PlayTime);
            Assert.Equal(2026, new DateTime(save.SaveTimestamp, DateTimeKind.Utc).Year);
            Assert.Equal(0x40, ((SaveKh3PC)save).GameflowBools.Length);
        });

        [Fact]
        public void WrongAccountIdThrows() => File.OpenRead(Slot0Path).Using(stream =>
            Assert.Throws<InvalidDataException>(() => SaveKh3PcCrypto.Decrypt(stream, "76561197999624471")));

        [Theory]
        [InlineData(Slot0Path)]
        [InlineData(Slot1Path)]
        [InlineData(SystemPath)]
        public void RoundTripIsByteIdentical(string path) => File.OpenRead(path).Using(stream =>
        {
            var original = new MemoryStream();
            stream.CopyTo(original);

            var plain = SaveKh3PcCrypto.Decrypt(original, AccountId);
            var reencrypted = new MemoryStream();
            SaveKh3PcCrypto.Encrypt(plain, reencrypted, AccountId);

            Assert.Equal(original.ToArray(), reencrypted.ToArray());
        });

        [Fact]
        public void WriteWithoutChangesKeepsChecksumAndReencryptsIdentically() => File.OpenRead(Slot0Path).Using(stream =>
        {
            var original = new MemoryStream();
            stream.CopyTo(original);

            var save = SaveKh3.Read(SaveKh3PcCrypto.Decrypt(original, AccountId));
            var originalChecksum = save.Checksum;

            var rewritten = new MemoryStream();
            save.Write(rewritten);
            Assert.Equal(originalChecksum, save.Checksum);

            var reencrypted = new MemoryStream();
            SaveKh3PcCrypto.Encrypt(rewritten, reencrypted, AccountId);
            Assert.Equal(original.ToArray(), reencrypted.ToArray());
        });

        [Fact]
        public void WriteWithChangesAndReencrypt() => File.OpenRead(Slot0Path).Using(stream =>
        {
            var save = SaveKh3.Read(SaveKh3PcCrypto.Decrypt(stream, AccountId));
            save.Munny = 9999;
            save.Difficulty = DifficultyType.Critical;

            var plain = new MemoryStream();
            save.Write(plain);
            var encrypted = new MemoryStream();
            SaveKh3PcCrypto.Encrypt(plain, encrypted, AccountId);

            Assert.True(SaveKh3PcCrypto.IsEncrypted(encrypted));
            var reloaded = SaveKh3.Read(SaveKh3PcCrypto.Decrypt(encrypted, AccountId));
            Assert.Equal(9999, reloaded.Munny);
            Assert.Equal(DifficultyType.Critical, reloaded.Difficulty);
        });
    }
}
