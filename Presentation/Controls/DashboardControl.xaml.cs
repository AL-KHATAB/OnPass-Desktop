using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OnPass.Presentation.Windows;


namespace OnPass.Presentation.Controls
{
    // Acts as the authenticated dashboard shell and swaps the main content area
    // between the home, vault, generator, authenticator, and settings screens.
    public partial class DashboardControl : UserControl
    {
        private MainWindow mainWindow;
        private string username;
        private byte[] encryptionKey;
        public Border TopBar => Dashboardtopbar;

        public DashboardControl(MainWindow mw, string user, byte[] key)
        {
            InitializeComponent();
            mainWindow = mw;
            username = user;
            encryptionKey = key;
            WelcomeText.Text = $"Welcome to OnPass, {username}!";
            DashboardContent.Content = new HomeDashboardControl(username, encryptionKey);
        }

        // Shares the same custom top-bar behavior as the outer window shell.
        private void TopBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    mainWindow.ToggleWindowState();
                }
                else if (e.LeftButton == MouseButtonState.Pressed)
                {
                    Window.GetWindow(this).DragMove();
                }
            }
        }

        // Lets the shell adjust the internal top bar when the main window changes size state.
        public void SetTopBarHeight(double height)
        {
            Dashboardtopbar.Height = height;
        }

        // Restores the home summary screen for the current user session.
        private void Home_Click(object sender, RoutedEventArgs e)
        {
            WelcomeText.Text = $"Welcome to OnPass, {username}!";
            DashboardContent.Content = new HomeDashboardControl(username, encryptionKey);
        }

        // Opens the vault workflow while preserving the current session key and shell context.
        private void PasswordManagerButton_Click(object sender, RoutedEventArgs e)
        {
            WelcomeText.Text = "Password Manager";
            DashboardContent.Content = new PasswordVaultControl(mainWindow, username, encryptionKey);
        }

        // Navigates to the password generator without leaving the authenticated dashboard shell.
        private void GeneratePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            WelcomeText.Text = "Generate Password";
            DashboardContent.Content = new GeneratePasswordControl(username, encryptionKey);
        }

        // Creates the authenticator view and injects the active user and session context before showing it.
        private void AuthenticatorButton_Click(object sender, RoutedEventArgs e)
        {
            DashboardContent.Content = null;

            WelcomeText.Text = "Two-Factor Authentication";

            var authenticatorControl = new AuthenticatorControl();
            authenticatorControl.Initialize(username, encryptionKey);

            DashboardContent.Content = authenticatorControl;
        }

        // Opens settings from within the same dashboard shell so session state is preserved.
        public void Settings_Click(object sender, RoutedEventArgs e)
        {
            WelcomeText.Text = "Settings";

            var settings = new Settings(username, encryptionKey);
            DashboardContent.Content = settings;
        }


        // Performs a full logout by clearing session state and stopping the localhost bridge.
        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                App.MinimizeToTrayEnabled = false;
                App.CurrentUsername = null!;
                App.CurrentAccessToken = null;
                App.WebServer?.Stop();
                App.WebServer = null;
                


                typeof(LoginControl).GetProperty("CurrentEncryptionKey",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Static)?.SetValue(null, null);

                mainWindow.Navigate(new LoginControl(mainWindow));
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error during logout: {ex.Message}",
                    "Logout Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

       
    }
}


