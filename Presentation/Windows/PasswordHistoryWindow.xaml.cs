using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using OnPass.Domain;
using OnPass.Presentation.Controls;
namespace OnPass.Presentation.Windows
{
    // Displays the saved password-history entries for one vault record and lets
    // the user restore or clear that history without leaving the vault screen.

    public partial class PasswordHistoryWindow : Window

    {

        public ObservableCollection<PasswordHistoryEntry> HistoryEntries { get; set; }

        private PasswordItem _passwordItem;

        private string _username;

        private PasswordVaultControl _parentControl;

        public bool RestoreRequested { get; private set; }

        public int SelectedHistoryIndex { get; private set; }

        public bool HistoryCleared { get; private set; }



        public PasswordHistoryWindow(PasswordItem passwordItem, string username, PasswordVaultControl parentControl)

        {

            InitializeComponent();

            _passwordItem = passwordItem;

            _username = username;

            _parentControl = parentControl;

            HistoryEntries = new ObservableCollection<PasswordHistoryEntry>();

            DataContext = this;

            LoadHistory();

        }



        // Rebuilds the list from the domain model so the dialog always reflects the latest stored history.
        private void LoadHistory()

        {

            HistoryEntries.Clear();

            foreach (var entry in _passwordItem.GetPasswordHistory())

            {

                HistoryEntries.Add(entry);

            }

        }

        // Confirms the selected historical entry and reports the chosen index back to the vault screen.
        private void RestoreButton_Click(object sender, RoutedEventArgs e)

        {

            if (HistoryListBox.SelectedItem is PasswordHistoryEntry selectedEntry)

            {

                var result = MessageBox.Show(

                    $"Are you sure you want to restore the password from {selectedEntry.DateChanged:yyyy-MM-dd HH:mm:ss}?",

                    "Confirm Restore",

                    MessageBoxButton.YesNo,

                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)

                {

                    SelectedHistoryIndex = HistoryEntries.IndexOf(selectedEntry);

                    RestoreRequested = true;

                    DialogResult = true;

                    Close();

                }

            }

            else

            {

                MessageBox.Show("Please select a password history entry to restore.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);

            }

        }

        // Clears all stored history entries for the current password item and persists the change immediately.
        private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)

        {

            var result = MessageBox.Show(

                "Are you sure you want to clear all password history? This action cannot be undone.",

                "Confirm Clear History",

                MessageBoxButton.YesNo,

                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)

            {

                _passwordItem.ClearHistory();

                _parentControl.SaveAllPasswordsPublic();

                LoadHistory();

                HistoryCleared = true;

                MessageBox.Show("Password history cleared successfully.", "History Cleared", MessageBoxButton.OK, MessageBoxImage.Information);



            }

            if (HistoryEntries.Count == 0)

            {

                ClearHistoryButton.IsEnabled = false;

            }

        }

        // Closes the history dialog without requesting any restore action.
        private void CloseButton_Click(object sender, RoutedEventArgs e)

        {

            DialogResult = false;

            Close();

        }

        // Enables restore only when the user has actively selected one history record.
        private void HistoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)

        {

            RestoreButton.IsEnabled = HistoryListBox.SelectedItem != null;

        }

    }

}



