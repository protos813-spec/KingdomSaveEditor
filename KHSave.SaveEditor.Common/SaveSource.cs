namespace KHSave.SaveEditor.Common
{
    /// <summary>
    /// Save game source for locale guessing
    /// </summary>
    public static class SaveSource
    {
        /// <summary>File path, null if process memory.</summary>
        public static string FileName { get; set; }

        /// <summary>Human readable archive (e.g. "PS2 PSU", "PC KH1FM"), null if a raw save.</summary>
        public static string ArchiveName { get; set; }

        /// <summary>Entry name inside the archive (e.g. "BISLPS-25198-05"), null if a raw save.</summary>
        public static string EntryName { get; set; }

        public static void SetFile(string fileName)
        {
            FileName = fileName;
            ArchiveName = null;
            EntryName = null;
        }

        public static void SetArchiveEntry(string archiveName, string entryName)
        {
            ArchiveName = archiveName;
            EntryName = entryName;
        }

        /// <summary>
        /// True when the source names a JP PS2 product code, false when it names an
        /// international one or a Remix container, null when no matches.
        /// </summary>
        public static bool? IsJapaneseRegion()
        {
            var text = $"{EntryName} {System.IO.Path.GetFileName(FileName)}".ToUpperInvariant();
            if (text.Contains("SLPS") || text.Contains("SLPM") || text.Contains("SCPS"))
                return true;
            if (text.Contains("SLUS") || text.Contains("SCUS") || text.Contains("SLES") || text.Contains("SCES"))
                return false;

            // PS3/PS4/PC Remix containers default to the international text tables.
            var archive = ArchiveName ?? string.Empty;
            if (archive.StartsWith("PC ") || archive.StartsWith("PS3") || archive.StartsWith("PS4"))
                return false;

            return null;
        }
    }
}
