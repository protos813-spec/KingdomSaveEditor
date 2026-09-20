/*
    Kingdom Save Editor
    Copyright (C) 2020 Luciano Ciccariello
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

using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Xe.Tools;
using Xe.Tools.Wpf.Commands;
using KHSave.SaveEditor.Common;
using KHSave.SaveEditor.Common.Contracts;
using KHSave.Trssv;
using KHSave.Lib2;
using KHSave.LibRecom;
using KHSave.SaveEditor.Common.Exceptions;
using KHSave.Archives;
using KHSave.SaveEditor.Common.Views;
using KHSave.SaveEditor.Common.Services;
using KHSave.SaveEditor.Interfaces;
using System;
using KHSave.SaveEditor.Services;
using System.Windows.Controls;
using KHSave.Lib3;
using KHSave.LibFf7Remake;
using KHSave.Lib1;
using KHSave.LibBbs;
using KHSave.LibDDD;
using KHSave.SaveEditor.Views;
using KHSave.LibPersona5;
using System.Net.Http;
using System.Net;
using KHSave.LibPersona3;

namespace KHSave.SaveEditor.ViewModels
{
    public class MainWindowViewModel : BaseNotifyPropertyChanged
    {
        private readonly IFileDialogManager fileDialogManager;
        private readonly IWindowManager windowManager;
        private readonly IAlertMessage alertMessage;
        private readonly ContentFactory contentFactory;
        private object dataContext;
        private ContentType _saveKind;

        private bool _isProcess;
        private string _processTitleName;
        private ProcessStream _processStream;

        private string OriginalTitle => "Kingdom Save Editor";
        public string CurrentVersion { get; }

        private Window Window => Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive);

        public string Title
        {
            get
            {
                if (_isProcess)
                    return $"[P] {_processTitleName} | {OriginalTitle}";

                return IsFileOpen ? $"{fileDialogManager.CurrentFileName} | {OriginalTitle}" : OriginalTitle;
            }
        }

        public bool IsFileOpen => SaveKind != ContentType.Unload && fileDialogManager.IsFileOpen;

        public ContentType SaveKind
        {
            get => _saveKind;
            set
            {
                _saveKind = value;
                ChangeContent(_saveKind);
            }
        }

        public HomeViewModel HomeContext { get; }
        public RelayCommand OpenCommand { get; }
        public RelayCommand OpenPcsx2Command { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand SaveAsCommand { get; }
        public RelayCommand SaveToSlotCommand { get; }
        public RelayCommand ExportKh3PcCommand { get; }
        public RelayCommand ExportKh3DecryptedCommand { get; }
        public RelayCommand ImportCommand { get; }
        public RelayCommand ExitCommand { get; }
        public RelayCommand GetLatestVersionCommand { get; }
        public RelayCommand OpenLinkCommand { get; }
        public RelayCommand OpenGitHubIssueLinkCommand { get; }

        public object DataContext
        {
            get => dataContext;
            set
            {
                dataContext = value;
                OnPropertyChanged();
            }
        }

        public Action<UserControl> OnControlChanged { get; set; }
        public IRefreshUi RefreshUi { get; set; }
        public IOpenStream OpenStream { get; set; }
        public IWriteToStream WriteToStream { get; set; }
        public IGetSave GetSave { get; private set; }

        /// <summary>Keep a timestamped .bak copy of a save file before overwriting it.</summary>
        public bool IsBackupOnSaveEnabled
        {
            get => Global.BackupOnSave;
            set
            {
                Global.BackupOnSave = value;
                OnPropertyChanged();
            }
        }

        public bool IsAdvancedMode
        {
            get => Global.IsAdvancedMode;
            set
            {
                Global.IsAdvancedMode = value;
                InvokeRefreshUi();
            }
        }

        public MainWindowViewModel(
            IFileDialogManager fileDialogManager,
            IWindowManager windowManager,
            IAlertMessage alertMessage,
            IAppIdentity appIdentity,
            ContentFactory contentFactory,
            HomeViewModel homeContext)
        {
            this.fileDialogManager = fileDialogManager;
            this.windowManager = windowManager;
            this.alertMessage = alertMessage;
            this.contentFactory = contentFactory;
            HomeContext = homeContext;
            CurrentVersion = appIdentity.Version;

            OpenCommand = new RelayCommand(o => fileDialogManager.Open(stream => Open(stream)));
            OpenPcsx2Command = new RelayCommand(o => OpenPcsx2(stream => Open(stream)));
            SaveCommand = new RelayCommand(o =>
            {
                CatchException(() =>
                {
                    if (_isProcess)
                        Save(_processStream);
                    else
                    {
                        // Create in-memory back-up to recover disastrous savings
                        var backupStream = new MemoryStream();
                        if (File.Exists(fileDialogManager.CurrentFileName))
                        {
                            using (var originalStream = File.OpenRead(fileDialogManager.CurrentFileName))
                                originalStream.CopyTo(backupStream);
                        }

                        try
                        {
                            fileDialogManager.Save(Save);
                        }
                        catch
                        {
                            // Restore the in-memory back-up before throwing
                            using (var stream = File.Create(fileDialogManager.CurrentFileName))
                                backupStream.SetPosition(0).CopyTo(stream);
                            throw;
                        }
                    }
                });
            },
                x => IsFileOpen || _isProcess);
            SaveAsCommand = new RelayCommand(o => CatchException(() => fileDialogManager.SaveAs(Save)),
                x => IsFileOpen || _isProcess);
            SaveToSlotCommand = new RelayCommand(o => CatchException(SaveToSlot),
                x => CurrentArchiveWriter != null);
            ExportKh3PcCommand = new RelayCommand(o => CatchException(ExportKh3Pc),
                x => IsFileOpen && SaveKind == ContentType.KingdomHearts3);
            ExportKh3DecryptedCommand = new RelayCommand(o => CatchException(ExportKh3Decrypted),
                x => CurrentKh3PcWriter != null);
            ImportCommand = new RelayCommand(o => CatchException(() =>
            {
                MessageBox.Show(
                    "This functionality allows you to import a Kingdom Hearts II save of a region over another region.\n\n" +
                    "Note that this will not import the whole save but only the known values, therefore some content of " +
                    "your old save will still be present (eg. Gummiship, Journal, Minigames).");

                new FileDialogManager(windowManager)
                    .Open(stream =>
                    {
                        if (GetSave == null)
                            throw new Exception("The game you decided to operate with is not within the supported game list that supports import transfer.");

                        switch (TransferService.Transfer(GetSave.GetSave(), stream))
                        {
                            case TransferService.Result.Success:
                                RefreshUi.RefreshUi();
                                break;
                            case TransferService.Result.GameNotSupported:
                                throw new Exception("The game you decided to operate with is NOT within the supported game list that supports import transfer.");
                            case TransferService.Result.SourceNotCompatible:
                                throw new Exception("The game you selected is different than the save you have currently opened, therefore it is not possible to import it.");
                            case TransferService.Result.InternalError:
                                throw new Exception("Oh well, this was not supposed to happen... you might want to report this. Sorry 😅");
                        }
                    });
            }), x => (IsFileOpen || _isProcess) && IsTransferSupported());
            ExitCommand = new RelayCommand(x => Window.Close());

            OpenLinkCommand = new RelayCommand(url => Process.Start(new ProcessStartInfo()
            {
                FileName = url as string,
                UseShellExecute = true
            }));

            OpenGitHubIssueLinkCommand = new RelayCommand(async url =>
            {
                using var client = new HttpClient(new HttpClientHandler
                {
                    AllowAutoRedirect = false,
                    AutomaticDecompression = DecompressionMethods.All,
                });
                using var response = await client.GetAsync(url as string, HttpCompletionOption.ResponseHeadersRead);
                if (IsIssueSectionClosed(response))
                    MessageBox.Show("The bug report section is temporarily closed as some users written toxic comments in it and due to spam.",
                        "Bug report section temporarily closed", MessageBoxButton.OK, MessageBoxImage.Warning);
                else
                    OpenLinkCommand.Execute(url);
            });
        }

        private bool IsTransferSupported()
        {
            switch (SaveKind)
            {
                case ContentType.KingdomHearts2:
                    return true;
                default:
                    return false;
            }
        }

        private void Buffered(Stream stream, Action<Stream> call) => Buffered(stream, bufferedStream =>
        {
            call(bufferedStream);
            return true;
        });

        private T Buffered<T>(Stream stream, Func<Stream, T> call)
        {
            const int DefaultBufferLength = 1024 * 1024;
            var bufferLength = DefaultBufferLength;
            if (stream.Length > 0 && stream.Length < DefaultBufferLength)
                bufferLength = (int)stream.Length;

            var bufferedStream = stream is BufferedStream ? (BufferedStream)stream :
                new BufferedStream(stream, bufferLength);

            var result = call(bufferedStream);
            if (bufferedStream.CanWrite)
                bufferedStream.Flush();

            return result;
        }

        public void Open(string fileName) => CatchException(() =>
        {
            fileDialogManager.InjectFileName(fileName, stream => Open(stream));
        });

        public void OpenPcsx2(Func<Stream, bool> openStream) => CatchException(() =>
        {
            var process = new AttachToProcessWindow("pcsx2").WaitForProcess();
            if (process != null)
            {
                var stream = new AttachToPcsx2GameWindow().WaitForGame(process);
                if (stream != null)
                    OpenProcessStream(stream, openStream);
            }
        });

        public bool Open(Stream stream) => CatchException(() =>
        {
            CloseProcessStream();
            SaveSource.SetFile(stream is FileStream fileStream ? fileStream.Name : null);

            try
            {
                if (!Buffered(stream, TryOpen))
                    throw CreateUnsupportedSaveExceptiom();

                InvokeRefreshUi();
                OnPropertyChanged(nameof(Title));
                return true;
            }
            catch (SaveNotSupportedException ex)
            {
                alertMessage.Error(ex);
            }

            return false;
        });

        /// <summary>Set when the open save came out of a multi-slot save file.</summary>
        private ArchiveWriteToStream CurrentArchiveWriter => WriteToStream as ArchiveWriteToStream;

        /// <summary>
        /// Writes the save being edited into another slot of the same save file, picked with the
        /// same dialog used to choose a slot when opening.
        /// </summary>
        private void SaveToSlot()
        {
            var writer = CurrentArchiveWriter;
            if (writer == null)
                throw new Exception("This save was not opened from a save file with multiple slots.");

            var archive = writer.Archive;
            IArchiveEntry target = null;
            var picked = windowManager.Push<ArchiveManagerView>(
                onSetup: window => window.SetArchive(archive, fileDialogManager.CurrentFileName),
                onSuccess: window =>
                {
                    target = window.SelectedEntry;
                    return target != null;
                });

            if (picked != true || target == null)
                return;

            var index = archive.Entries.IndexOf(target);
            if (!ReferenceEquals(target, writer.Entry) && !ArchiveSlot.IsEmpty(target))
            {
                var answer = MessageBox.Show(
                    $"Slot {index + 1} already contains \"{target.Name}\", last saved on {target.DateModified}." +
                    "\n\nOverwriting it cannot be undone. Continue?",
                    "Save to slot",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);

                if (answer != MessageBoxResult.Yes)
                    return;
            }

            ArchiveSlot.Prepare(archive, target, writer.Entry, index);

            // Point the editor at the chosen slot so this and later saves land there.
            var previous = WriteToStream;
            WriteToStream = new ArchiveWriteToStream(writer.Inner, archive, target);
            try
            {
                fileDialogManager.Save(Save);
            }
            catch
            {
                WriteToStream = previous;
                throw;
            }
        }

        private void Save(Stream stream)
        {
            Buffered(stream, WriteToStream.WriteToStream);
            OnPropertyChanged(nameof(Title));
        }

        /// <summary>Set when the open save came out of an encrypted KH3 PC save file.</summary>
        private Kh3PcEncryptedWriteToStream CurrentKh3PcWriter => WriteToStream as Kh3PcEncryptedWriteToStream;

        /// <summary>
        /// Writes the save as a separate encrypted KH3 PC save
        /// </summary>
        private void ExportKh3Pc()
        {
            if (!(GetSave?.GetSave() is ISaveKh3 save))
                throw new Exception("Only a Kingdom Hearts III save can be converted to the PC format.");

            var plainStream = new MemoryStream();
            Kh3PlainWriter.WriteToStream(plainStream);

            if (!(save is SaveKh3PC))
            {
                if (!SaveKh3PcCrypto.CanEncrypt(plainStream.Length))
                    throw new Exception(
                        $"This save has the layout of Kingdom Hearts III version {save.MajorVersion}.{save.MinorVersion}, " +
                        "which cannot be stored in the PC save container. Only a save written by a game version that " +
                        "matches the PC release can be converted.");

                if (!ConfirmForeignSaveLayout(save))
                    return;
            }

            var accountId = AskKh3AccountId();
            if (accountId == null)
                return;

            var fileName = SaveKh3PcCrypto.SuggestPcFileName(fileDialogManager.CurrentFileName);
            var folder = SaveKh3PcCrypto.TryGetSaveFolder(accountId);
            if (folder != null)
                fileName = Path.Combine(folder, fileName);

            fileDialogManager.ExportAs(fileName, "Kingdom Hearts III PC save", "bin",
                stream => SaveKh3PcCrypto.Encrypt(plainStream, stream, accountId));
        }

        /// <summary>
        /// Writes the save without the PC encryption, which is the form the console decryption tools expect.
        /// </summary>
        private void ExportKh3Decrypted()
        {
            var writer = CurrentKh3PcWriter;
            if (writer == null)
                throw new Exception("This save was not opened from an encrypted Kingdom Hearts III PC save.");

            var current = fileDialogManager.CurrentFileName ?? "KHIII_slot0.bin";
            var name = Path.Combine(
                Path.GetDirectoryName(current) ?? string.Empty,
                $"{Path.GetFileNameWithoutExtension(current)}_decrypted.bin");

            fileDialogManager.ExportAs(name, "Decrypted Kingdom Hearts III save", "bin",
                stream => Buffered(stream, writer.Inner.WriteToStream));
        }

        /// <summary>The KH3 editor itself, with the PC encryption wrapper taken off.</summary>
        private IWriteToStream Kh3PlainWriter => CurrentKh3PcWriter?.Inner ?? WriteToStream;

        private string AskKh3AccountId()
        {
            string accountId = null;
            var result = windowManager.Push<Kh3AccountIdWindow>(
                onSetup: window => window.AskForEncryption(
                    SaveKh3PcCrypto.TryGetAccountIdFromPath(fileDialogManager.CurrentFileName)),
                onSuccess: window =>
                {
                    accountId = window.AccountId;
                    return true;
                });

            return result == true ? accountId : null;
        }

        /// <summary>
        /// Version mismatch warning prompt. Might be a nothingburger coz the game will probably upgrade the save as needed.
        /// </summary>
        private static bool ConfirmForeignSaveLayout(ISaveKh3 save) =>
            MessageBox.Show(
                $"This save was written by Kingdom Hearts III version {save.MajorVersion}.{save.MinorVersion}, while the PC release writes a newer layout. Whether the game upgrades an older save when loading it is untested.\n\n" +
                "The conversion writes a new file and leaves this save untouched, so it is safe to try. Convert?",
                "Save version mismatch",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.Yes) == MessageBoxResult.Yes;

        public bool TryOpen(Stream stream) =>
            TryOpenKh1(stream) ||
            TryOpenKh2(stream) ||
            TryOpenKhBbs(stream) ||
            TryOpenKhDDD(stream) ||
            TryOpenKhRecom(stream) ||
            TryOpenKh02(stream) ||
            TryOpenKh3(stream) ||
            TryOpenFF7Remake(stream) ||
            TryOpenPersona3(stream) ||
            TryOpenPersona5(stream) ||
            TryOpenArchive(stream);

        private bool Open(IArchiveFactory archiveFactory, Stream stream)
        {
            var archive = archiveFactory.Read(stream);
            stream.Close();
            return Open(archive);
        }

        private bool Open(IArchive archive)
        {
            var result = windowManager.Push<ArchiveManagerView>(
                onSetup: window => window.SetArchive(archive, fileDialogManager.CurrentFileName),
                onSuccess: window => Open(archive, window.SelectedEntry));

            if (result == false)
            {
                ChangeContent(ContentType.Unload);
            }

            return true;
        }

        private bool Open(IArchive archive, IArchiveEntry archiveEntry)
        {
            bool result;

            SaveSource.SetArchiveEntry(archive.Name, archiveEntry.Name);
            using (var stream = new MemoryStream(archiveEntry.Data))
                result = TryOpen(stream);

            if (!result)
                throw CreateUnsupportedSaveExceptiom();

            // archiveEntry.Name
            // archiveEntry.DateCreated
            // archiveEntry.DateModified

            WriteToStream = new ArchiveWriteToStream(WriteToStream, archive, archiveEntry);

            return result;
        }

        public bool TryOpenArchive(Stream stream)
        {
            if (!ArchiveFactories.TryGetFactory(stream, out var archiveFactory))
                return false;

            stream.Position = 0;
            return Open(archiveFactory, stream);
        }

        public bool TryOpenKh1(Stream stream) => TryOpen(SaveKh1.IsValid, stream, ContentType.KingdomHearts);
        public bool TryOpenKh2(Stream stream) => TryOpen(SaveKh2.IsValid, stream, ContentType.KingdomHearts2);
        public bool TryOpenKhBbs(Stream stream) => TryOpen(SaveKhBbs.IsValid, stream, ContentType.KingdomHeartsBbs);
        public bool TryOpenKhDDD(Stream stream) => TryOpen(SaveKhDDD.IsValid, stream, ContentType.KingdomHeartsDDD);
        public bool TryOpenKhRecom(Stream stream) => TryOpen(SaveKhRecom.IsValid, stream, ContentType.KingdomHeartsRecom);
        public bool TryOpenKh02(Stream stream) => TryOpen(SaveKh02.IsValid, stream, ContentType.KingdomHearts02);
        public bool TryOpenKh3(Stream stream) =>
            TryOpen(SaveKh3.IsValid, stream, ContentType.KingdomHearts3) ||
            TryOpenKh3Encrypted(stream);

        /// <summary>
        /// KH3 Steam / Epic saves are AES encrypted with a key derived from the account ID
        /// </summary>
        private bool TryOpenKh3Encrypted(Stream stream)
        {
            if (!SaveKh3PcCrypto.IsEncrypted(stream))
                return false;

            var accountId = SaveKh3PcCrypto.TryGetAccountIdFromPath(fileDialogManager.CurrentFileName)
                ?? SaveKh3PcCrypto.TryFindAccountId(stream, SaveKh3PcCrypto.FindLocalAccountIds());
            if (accountId == null)
            {
                string enteredId = null;
                var result = windowManager.Push<Kh3AccountIdWindow>(
                    onSuccess: window =>
                    {
                        enteredId = window.AccountId;
                        return true;
                    });
                if (result != true)
                    return false;
                accountId = enteredId;
            }

            var plainStream = SaveKh3PcCrypto.Decrypt(stream, accountId);
            if (!TryOpen(SaveKh3.IsValid, plainStream, ContentType.KingdomHearts3))
                return false;

            WriteToStream = new Kh3PcEncryptedWriteToStream(WriteToStream, accountId);
            return true;
        }
        public bool TryOpenFF7Remake(Stream stream) => TryOpen(SaveFf7Remake.IsValid, stream, ContentType.FinalFantasy7Remake);
        public bool TryOpenPersona3(Stream stream) => TryOpen(SavePersona3.IsValid, stream, ContentType.Persona3);
        public bool TryOpenPersona5(Stream stream) => TryOpen(SavePersona5.IsValid, stream, ContentType.Persona5);

        public bool TryOpen(Func<Stream, bool> prediate, Stream stream, ContentType contentType)
        {
            if (!prediate(stream))
                return false;

            _saveKind = contentType;
            ChangeContent(contentType, stream);
            return true;
        }

        private void OpenProcessStream(ProcessStream processStream, Func<Stream, bool> openStream)
        {
            if (openStream(processStream))
            {
                _isProcess = true;
                _processStream = processStream;
                _processTitleName = $"PCSX2@{processStream.BaseAddress:X08}";

                OnPropertyChanged(nameof(Title));
            }
        }

        private void CloseProcessStream()
        {
            if (!_isProcess)
                return;

            _isProcess = false;
            _processTitleName = string.Empty;
            _processStream?.Dispose();
            _processStream = null;

            OnPropertyChanged(nameof(Title));
        }

        public static void CatchException(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static T CatchException<T>(Func<T> action)
        {
            try
            {
                return action();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return default(T);
            }
        }

        private static Exception CreateUnsupportedSaveExceptiom() =>
            new SaveNotSupportedException("The specified save game is not recognized.\nBe sure to have the last version or that the save is decrypted or supported.");

        public void InvokeRefreshUi() => RefreshUi?.RefreshUi();

        private void ChangeContent(ContentType contentType, Stream stream = null)
        {
            try
            {
                contentFactory.LoadIconPack(contentType);
                var contentResponse = contentFactory.Factory(contentType);

                RefreshUi = contentResponse.RefreshUi;
                WriteToStream = contentResponse.WriteToStream;
                GetSave = contentResponse.GetSave;

                if (stream != null)
                    contentResponse.OpenStream.OpenStream(stream);

                OnPropertyChanged(nameof(SaveCommand));
                OnPropertyChanged(nameof(SaveAsCommand));
                OnPropertyChanged(nameof(SaveToSlotCommand));
                OnPropertyChanged(nameof(ExportKh3PcCommand));
                OnPropertyChanged(nameof(ExportKh3DecryptedCommand));
                OnPropertyChanged(nameof(ImportCommand));
                OnControlChanged?.Invoke(contentResponse.Control);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"An unhandled error has occurred:\n{ex.Message}\n\n{ex.StackTrace}",
                    "Fatal error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool IsIssueSectionClosed(HttpResponseMessage response) =>
            (int)response.StatusCode >= 300 && (int)response.StatusCode < 400 &&
            response.Headers.TryGetValues("Location", out var location) &&
            location.Any() && location.First().EndsWith("/pulls");
    }
}
