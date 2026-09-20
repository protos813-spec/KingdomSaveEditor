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

using System.IO;
using KHSave.Lib3;
using KHSave.SaveEditor.Common.Contracts;

namespace KHSave.SaveEditor.Services
{
    /// <summary>
    /// Re-encrypts a KH3 PC save when writing it back to disk.
    /// </summary>
    public class Kh3PcEncryptedWriteToStream : IWriteToStream
    {
        private readonly IWriteToStream realWriteToStream;

        public Kh3PcEncryptedWriteToStream(IWriteToStream realWriteToStream, string accountId)
        {
            this.realWriteToStream = realWriteToStream;
            AccountId = accountId;
        }

        public string AccountId { get; }

        /// <summary>The editor underneath, which serializes the plain save.</summary>
        public IWriteToStream Inner => realWriteToStream;

        public void WriteToStream(Stream stream)
        {
            using (var plainStream = new MemoryStream())
            {
                realWriteToStream.WriteToStream(plainStream);
                SaveKh3PcCrypto.Encrypt(plainStream, stream, AccountId);
            }
        }
    }
}
