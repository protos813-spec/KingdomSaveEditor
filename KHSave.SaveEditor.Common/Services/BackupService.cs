using System;
using System.IO;

namespace KHSave.SaveEditor.Common.Services
{
    /// <summary>
    /// Keeps a timestamped copy of a save file before the editor overwrites it, so a bad edit
    /// never costs the original. Backups sit next to the save as "&lt;name&gt;.&lt;timestamp&gt;.bak",
    /// for example "KHIIFM_WW.png.202609162301.bak".
    /// </summary>
    public static class BackupService
    {
        public const string TimestampFormat = "yyyyMMddHHmm";
        public const string Extension = ".bak";

        /// <summary>
        /// Name of the backup for a save, given the moment it is taken. Saving twice within the
        /// same minute would otherwise reuse a name and throw away the older, more valuable copy,
        /// so a counter is added instead.
        /// </summary>
        public static string GetBackupName(string fileName, DateTime timestamp, Func<string, bool> exists)
        {
            var stem = $"{fileName}.{timestamp.ToString(TimestampFormat)}";
            var candidate = stem + Extension;
            for (var i = 2; exists(candidate); i++)
                candidate = $"{stem}-{i}{Extension}";

            return candidate;
        }

        /// <summary>
        /// Copies the file aside. Returns the backup path, or null when there was nothing to copy
        /// or backups are switched off. Never throws: losing a backup must not block a save.
        /// </summary>
        public static string TryCreateBackup(string fileName)
        {
            if (!Global.BackupOnSave || string.IsNullOrEmpty(fileName))
                return null;

            try
            {
                if (!File.Exists(fileName))
                    return null;

                var backupName = GetBackupName(fileName, DateTime.Now, File.Exists);
                File.Copy(fileName, backupName);
                return backupName;
            }
            catch
            {
                return null;
            }
        }
    }
}
