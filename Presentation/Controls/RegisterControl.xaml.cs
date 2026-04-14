using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using OnPass.Domain;
using OnPass.Infrastructure.Security;
using OnPass.Infrastructure.Storage;
using OnPass.Presentation.Windows;

namespace OnPass.Presentation.Controls
{
    // Handles account creation by validating credentials, creating the initial
    // encrypted vault, and writing the new user's settings and credential record.
    public partial class RegisterControl : UserControl
    {
        private MainWindow mainWindow;
        private bool _passwordVisible = false;

        public RegisterControl(MainWindow mw)
        {
            InitializeComponent();
            mainWindow = mw;
        }

        // Allows the registration screen to share the same custom window chrome behavior as login.
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

        // Pressing Enter on the registration form triggers the same flow as clicking Register.
        private void Input_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                RegisterButton_Click(this, new RoutedEventArgs());
            }
        }

        // Validates the new account, stores the credential record, and creates the initial encrypted vault.
        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameInput.Text;
            string password = _passwordVisible ? PasswordVisible.Text : PasswordInput.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Username and password cannot be empty!", "Registration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            const int MinPasswordLength = 8;
            if (password.Length < MinPasswordLength)
            {
                MessageBox.Show($"Password must be at least {MinPasswordLength} characters long!", "Registration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                byte[] salt = GenerateSalt();
                byte[] hashedPassword = HashPassword(password, salt);
                byte[] encryptionKey = KeyDerivation.DeriveKey(password, salt);
                string saltBase64 = Convert.ToBase64String(salt);
                string hashBase64 = Convert.ToBase64String(hashedPassword);

                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string OnPassPath = Path.Combine(appDataPath, "OnPass");
                if (!Directory.Exists(OnPassPath))
                    Directory.CreateDirectory(OnPassPath);

                string credentialsFilePath = Path.Combine(OnPassPath, "credentials.txt");
                string sserCredentials = $"{username},{saltBase64},{hashBase64}";

                if (File.Exists(credentialsFilePath))
                {
                    foreach (string line in File.ReadAllLines(credentialsFilePath))
                    {
                        if (line.StartsWith(username + ","))
                        {
                            MessageBox.Show("Account already exists!", "Registration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }
                    File.AppendAllText(credentialsFilePath, sserCredentials + Environment.NewLine);
                }
                else
                {
                    File.WriteAllText(credentialsFilePath, sserCredentials + Environment.NewLine);
                }

                PasswordStorage.SavePasswords(new List<PasswordItem>(), username, encryptionKey);

                CreateUserSettingsFile(username, OnPassPath);

                MessageBox.Show("Registration successful!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                UsernameInput.Clear();
                PasswordInput.Clear();
                PasswordVisible.Clear();
                mainWindow.Navigate(new LoginControl(mainWindow));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Creates a per-user settings file so new accounts start with a predictable baseline configuration.
        private void CreateUserSettingsFile(string username, string OnPassPath)
        {
            try
            {
                string settingsFileName = $"{username}_settings.ini";
                string settingsFilePath = Path.Combine(OnPassPath, settingsFileName);

                string defasltSettings =
                    "[Settings]\n" +
                    "TwoFactorEnabled=false\n" +
                    "StartWithWindows=false\n" +
                    "MinimizeToTray=false\n" +
                    "DarkModeEnabled=false\n" +
                    "ClosdSyncEnabled=false\n" +
                    "BiometricEnabled=false\n";

                File.WriteAllText(settingsFilePath, defasltSettings);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating settings file: {ex.Message}");
            }
        }

        // Returns to the login screen when the user decides not to create a new account.
        private void LoginTextBlock_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            mainWindow.Navigate(new LoginControl(mainWindow));
        }

        // Creates the salt stored beside the user's credential record.
        private byte[] GenerateSalt(int saltSize = 16)
        {
            byte[] salt = new byte[saltSize];
            RandomNumberGenerator.Fill(salt);
            return salt;
        }

        // Recreates the password hash format used by the credentials file.
        private byte[] HashPassword(string password, byte[] salt, int iterations = 10000, int hashSize = 32)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(hashSize);
            }
        }

        private void PasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
        {
            string password = PasswordInput.Password;
            int strength = CalculatePasswordStrength(password);
            AnimateProgressBar(strength);
            PasswordStrengthMessage.Text = GetStrengthMessage(strength);
        }

        private void PasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            string password = PasswordVisible.Text;
            int strength = CalculatePasswordStrength(password);
            AnimateProgressBar(strength);
            PasswordStrengthMessage.Text = GetStrengthMessage(strength);
        }

        private void TogglePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            _passwordVisible = !_passwordVisible;

            if (_passwordVisible)
            {
                PasswordVisible.Text = PasswordInput.Password;
                PasswordVisible.Visibility = Visibility.Visible;
                PasswordInput.Visibility = Visibility.Collapsed;
            }
            else
            {
                PasswordInput.Password = PasswordVisible.Text;
                PasswordVisible.Visibility = Visibility.Collapsed;
                PasswordInput.Visibility = Visibility.Visible;
            }
        }

        // Animates the progress bar so password-strength feedback feels responsive rather than abrupt.
        private void AnimateProgressBar(int targetValue)
        {
            DoubleAnimation animation = new DoubleAnimation
            {
                From = PasswordStrengthBar.Value,
                To = targetValue,
                Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = new QuadraticEase()
            };

            PasswordStrengthBar.BeginAnimation(ProgressBar.ValueProperty, animation);

            if (targetValue < 50)
                PasswordStrengthBar.Foreground = new SolidColorBrush(Colors.Red);
            else if (targetValue < 81)
                PasswordStrengthBar.Foreground = new SolidColorBrush(Colors.Yellow);
            else
                PasswordStrengthBar.Foreground = new SolidColorBrush(Colors.Green);
        }

        // Uses a simple heuristic to discourage short or low-variety passwords during registration.
        private int CalculatePasswordStrength(string password)
        {
            int strength = 0;

            if (password.Length >= 4)
                strength += 5;
            if (password.Length >= 6)
                strength += 10;
            if (password.Length >= 8)
                strength += 20;
            if (password.Length >= 15)
                strength += 50;

            if (Regex.IsMatch(password, @"[A-Z]"))
                strength += 20;
            if (Regex.IsMatch(password, @"[0-9]"))
                strength += 20;
            if (Regex.IsMatch(password, @"[\W_]"))
                strength += 20;

            return strength;
        }

        // Turns the numeric strength score into a short message suitable for the registration UI.
        private string GetStrengthMessage(int strength)
        {
            if (strength < 40)
                return "Weak password";
            else if (strength < 70)
                return "Moderate password";
            else
                return "Strong password";
        }
    }
}
