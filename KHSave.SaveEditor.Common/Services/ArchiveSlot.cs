using KHSave.Archives;
using System;
using System.Linq;

namespace KHSave.SaveEditor.Common.Services
{
    /// <summary>Helpers for moving a save between the slots of the archive it came from.</summary>
    public static class ArchiveSlot
    {
        public static bool IsEmpty(IArchiveEntry entry) =>
            entry == null || string.IsNullOrEmpty(entry.Name);

        /// <summary>
        /// Slot names usually end with the slot number, as in "BISLPM-66675FM-03", so an empty
        /// destination gets the source name with that trailing number swapped for its own. Names
        /// that do not end in a number, such as the "SYS" entry, get the slot appended instead,
        /// because two entries sharing a name would be confusing both here and in the game.
        /// </summary>
        public static string DeriveName(string sourceName, int targetIndex)
        {
            if (string.IsNullOrEmpty(sourceName))
                return $"Save {targetIndex}";

            var digits = sourceName.Reverse().TakeWhile(char.IsDigit).Count();
            if (digits == 0)
                return $"{sourceName}-{targetIndex:00}";

            var stem = sourceName.Substring(0, sourceName.Length - digits);
            return stem + targetIndex.ToString(new string('0', digits));
        }

        /// <summary>
        /// Gives an empty destination slot an identity so the game lists it. A slot that already
        /// holds a save keeps its own name, since that name is how the game identifies it.
        /// </summary>
        public static void Prepare(IArchive archive, IArchiveEntry target, IArchiveEntry source, int targetIndex)
        {
            var now = DateTime.Now;
            if (IsEmpty(target))
            {
                target.Name = MakeUnique(archive, target, DeriveName(source?.Name, targetIndex));
                target.DateCreated = now;
                target.FlagCreated = source?.FlagCreated ?? 0;
                target.FlagModified = source?.FlagModified ?? 0;
            }

            target.DateModified = now;
        }

        private static string MakeUnique(IArchive archive, IArchiveEntry target, string candidate)
        {
            bool Taken(string name) => archive.Entries
                .Any(x => !ReferenceEquals(x, target) && x.Name == name);

            if (!Taken(candidate))
                return candidate;

            for (var i = 2; ; i++)
            {
                var alternative = $"{candidate}-{i}";
                if (!Taken(alternative))
                    return alternative;
            }
        }
    }
}
