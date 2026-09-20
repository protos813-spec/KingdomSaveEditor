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

using System.Collections.Generic;
using System.Linq;
using System.Windows;
using KHSave.Lib3;

namespace KHSave.SaveEditor.Views
{
    /// <summary>
    /// Asks the user for the Steam / Epic account ID used to encrypt a KH3 PC save.
    /// </summary>
    public partial class Kh3AccountIdWindow : Window
    {
        private const string DecryptDescription =
            "This save is encrypted with a key derived from the account ID it was saved with. Enter the SteamID64 (17 digits) or Epic Games account ID: it is the name of the folder that contains 'SaveGames\\kh3sv2' under Documents.";

        private const string EncryptDescription =
            "The save will be encrypted for the account that is going to load it, so enter the SteamID64 (17 digits) or Epic Games account ID of the PC copy of the game: it is the name of the folder that contains 'SaveGames\\kh3sv2' under Documents.";

        public Kh3AccountIdWindow()
        {
            InitializeComponent();
            DataContext = this;
            AccountIds = SaveKh3PcCrypto.FindLocalAccountIds().ToList();
            AccountId = AccountIds.FirstOrDefault();
            Description = DecryptDescription;
            Loaded += (s, e) => AccountIdComboBox.Focus();
        }

        public string AccountId { get; set; }

        /// <summary>Accounts with a local installation, offered as suggestions.</summary>
        public IList<string> AccountIds { get; }

        public string Description { get; private set; }

        /// <summary>Switches the window from asking how to read a save to asking how to write one.</summary>
        public void AskForEncryption(string suggestedAccountId)
        {
            Title = "Convert to Kingdom Hearts III PC save";
            Description = EncryptDescription;
            if (!string.IsNullOrEmpty(suggestedAccountId))
                AccountId = suggestedAccountId;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            AccountId = AccountId?.Trim();
            if (!SaveKh3PcCrypto.IsValidAccountId(AccountId))
            {
                MessageBox.Show(this,
                    "The account ID must not be empty and may only contain letters, digits, '-' and '_'.",
                    "Invalid account ID", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }
    }
}
