using System.IO;
using System.Linq;
using KHSave.Archives;
using KHSave.SaveEditor.Common.Contracts;

namespace KHSave.SaveEditor.Common.Services
{
    public class ArchiveWriteToStream : IWriteToStream
    {
        private readonly IWriteToStream realWriteToStream;

        public ArchiveWriteToStream(IWriteToStream realWriteToStream, IArchive archive, IArchiveEntry entry)
        {
            this.realWriteToStream = realWriteToStream;
            Archive = archive;
            Entry = entry;
        }

        /// <summary>The editor underneath, which serializes the save itself.</summary>
        public IWriteToStream Inner => realWriteToStream;

        public IArchive Archive { get; }
        public IArchiveEntry Entry { get; }

        public void WriteToStream(Stream stream)
        {
            using (var entryStream = new MemoryStream())
            {
                realWriteToStream.WriteToStream(entryStream);
                Entry.Data = entryStream.GetBuffer();
            }

            // The slot is the position in the list, so match the entry itself first. Falling back
            // to the name would land on the wrong slot when several of them are empty.
            var index = IndexOf(Entry);
            if (index >= 0)
                Archive.Entries[index] = Entry;
            else
                Archive.Entries.Add(Entry);

            Archive.Write(stream);
        }

        private int IndexOf(IArchiveEntry entry)
        {
            for (var i = 0; i < Archive.Entries.Count; i++)
            {
                if (ReferenceEquals(Archive.Entries[i], entry))
                    return i;
            }

            // Not the same object, so fall back to a named match.
            if (string.IsNullOrEmpty(entry.Name))
                return -1;

            return Archive.Entries
                .Select((x, i) => (Entry: x, Index: i))
                .Where(x => x.Entry.Name == entry.Name)
                .Select(x => x.Index)
                .DefaultIfEmpty(-1)
                .First();
        }
    }
}
