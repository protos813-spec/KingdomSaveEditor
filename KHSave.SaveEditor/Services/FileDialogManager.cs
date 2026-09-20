using System;
using System.Collections.Generic;
using System.IO;
using KHSave.SaveEditor.Interfaces;
using KHSave.SaveEditor.Common.Services;
using Xe.Tools.Wpf.Dialogs;

namespace KHSave.SaveEditor.Services
{
    public class FileDialogManager : IFileDialogManager
    {
        private readonly IEnumerable<FileDialogFilter> Filters = FileDialogFilterComposer
            .Compose()
            .AddExtensions("All supported games", "bin", "sav", "dat;*")
            .AddPatterns("Kingdom Hearts I", ";BESCES*", ";BASLUS-20370*", ";BISLPM-66233-*")
            .AddPatterns("Kingdom Hearts II", ";BISLPM-66675FM-**")
            .AddExtensions("Kingdom Hearts Birth By Sleep", "DAT")
            .AddPatterns("Kingdom Hearts Re: CoM", ";BISLUS-21799COM-*", ";BASLUS-21799COM-*")
            .AddExtensions("Kingdom Hearts 1.5/2.5 ReMIX", "DAT")
            .AddPatterns("Kingdom Hearts 0.2", ";ue4savegame*.sav")
            .AddPatterns("Kingdom Hearts III", ";__data__slot*.bin", ";KHIII_slot*.bin")
            .AddPatterns("Kingdom Hearts PC ports saves", "png")
            .AddExtensions("Final Fantasy VII REMAKE", ";ff7remake*")
            .AddExtensions("PS2 Save Archive", "psu", "cbs")
            .AddExtensions("PS2 Single Archive (PS3)", "psv")
            ;

        private readonly IWindowManager _windowManager;

        public bool IsFileOpen { get; private set; }

        public string CurrentFileName { get; private set; }

        public FileDialogManager(IWindowManager windowManager)
        {
            _windowManager = windowManager;
        }

        public void InjectFileName(string fileName, Action<Stream> onSuccess)
        {
            IsFileOpen = true;
            CurrentFileName = fileName;
            using (var stream = File.OpenRead(fileName))
            {
                try
                { onSuccess(stream); }
                catch
                {
                    IsFileOpen = false;
                    throw;
                }
            }
        }

        public void Open(Action<Stream> onSuccess) =>
            FileDialog.OnOpen(fileName => InjectFileName(fileName, onSuccess), Filters);

        public void Save(Action<Stream> onSuccess)
        {
            if (IsFileOpen)
            {
                BackupService.TryCreateBackup(CurrentFileName);
                using (var stream = File.Create(CurrentFileName))
                {
                    onSuccess(stream);
                }
            }
            else
            {
                SaveAs(onSuccess);
            }
        }

        public void ExportAs(string defaultFileName, string filterName, string extension, Action<Stream> onSuccess) =>
            PickAndWrite(defaultFileName, FileDialogFilterComposer
                .Compose()
                .AddExtensions(filterName, extension)
                .AddAllFiles(), false, onSuccess);

        public void SaveAs(Action<Stream> onSuccess) =>
            PickAndWrite(CurrentFileName, Filters, true, onSuccess);

        /// <param name="retarget">
        /// Whether the chosen file becomes the one being edited
        /// </param>
        private void PickAndWrite(
            string defaultFileName,
            IEnumerable<FileDialogFilter> filters,
            bool retarget,
            Action<Stream> onSuccess) =>
            FileDialog.OnSave(fileName =>
            {
                BackupService.TryCreateBackup(fileName);
                using (var stream = File.Create(fileName))
                {
                    if (retarget)
                        CurrentFileName = fileName;
                    onSuccess(stream);
                }
            }, filters, defaultFileName);
    }
}
