using System.Windows;

namespace OnPass.Presentation.Dialogs
{
    // Collects the current master password when a sensitive settings action
    // needs one more confirmation before changing biometric or key state.
    public partial class PasswordConfirmationDialog : Window
    {
        public string Password { get; private set; } = string.Empty;

        public PasswordConfirmationDialog()
        {
            InitializeComponent();
        }

        // Returns the entered password to the caller only when the field is not empty.
        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(PasswordInput.Password))
            {
                Password = PasswordInput.Password;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Please enter your password.", "Password Required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Closes the dialog without providing a password to the caller.
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}


