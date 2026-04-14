using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OnPass.Domain;
using OnPass.Infrastructure.Security;
using OnPass.Infrastructure.Storage;
using OnPass.Infrastructure.Web;
using System.Threading.Tasks;

namespace OnPass.Presentation.Controls
{
    // Summarizes the current user's security posture, exposes the extension
    // access token, and keeps the localhost bridge available from the dashboard.
    public partial class HomeDashboardControl : UserControl
    {
        private string? _accessToken;
        private LocalWebServer? _webServer;
        private readonly string _username;
        private readonly byte[] _encryptionKey;
        private List<PasswordItem> _passwords;
        private int _authenticatorCount;

        public HomeDashboardControl(string username, byte[] encryptionKey)
        {
            InitializeComponent();

            _username = username;
            _encryptionKey = encryptionKey;
            _passwords = new List<PasswordItem>();
            _authenticatorCount = 0;

            if (App.WebServer != null)
            {
                // Reuse the existing server when the dashboard is revisited so the
                // extension keeps the same token for the active session.
                _webServer = App.WebServer;
                _accessToken = App.CurrentAccessToken;
            }
            else
            {
                InitializeWebServer(username, encryptionKey);
            }

            Loaded += UserControl_Loaded;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            ExtensionKeyDisplay();
            UpdateSecuritySummary();
        }

        // Starts the localhost bridge when no active dashboard session has done so yet.
        private void InitializeWebServer(string username, byte[] encryptionKey)
        {
            try
            {
                _webServer = new LocalWebServer(username, encryptionKey);
                _webServer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start web server: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Retrieves and masks the extension access token that the browser companion uses for the current session.
        private void ExtensionKeyDisplay()
        {
            if (string.IsNullOrEmpty(_accessToken))
            {
                _accessToken = App.CurrentAccessToken;

                if (string.IsNullOrEmpty(_accessToken) && _webServer != null)
                {
                    _accessToken = _webServer.GetAccessToken();
                    App.CurrentAccessToken = _accessToken;
                }
            }

            if (!string.IsNullOrEmpty(_accessToken))
            {
                ExtensionKeyText.Text = _accessToken;
                MaskedKeyText.Text = new string('*', _accessToken.Length);
            }
            else
            {
                ExtensionKeyText.Text = "Server not started";
                MaskedKeyText.Text = "Server not started";
            }

            ExtensionKeyText.Visibility = Visibility.Collapsed;
            MaskedKeyText.Visibility = Visibility.Visible;
        }

        // Copies the current extension token and schedules clipboard clearing if the user does not replace it.
        private void CopyKeyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_accessToken))
                {
                    Clipboard.SetText(_accessToken);
                    _ = ClearClipboardIfUnchangedAsync(_accessToken);
                    ShowCopySuccess();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy key: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ToggleKeyVisibilityButton_Click(object sender, RoutedEventArgs e)
        {
            if (ExtensionKeyText.Visibility == Visibility.Collapsed)
            {
                if (!string.IsNullOrEmpty(_accessToken))
                {
                    ExtensionKeyText.Text = _accessToken;
                }

                ExtensionKeyText.Visibility = Visibility.Visible;
                MaskedKeyText.Visibility = Visibility.Collapsed;
            }
            else
            {
                ExtensionKeyText.Visibility = Visibility.Collapsed;
                MaskedKeyText.Visibility = Visibility.Visible;
            }
        }

        private void ShowCopySuccess()
        {
            string? originalContent = CopyKeyButton.Content?.ToString();
            CopyKeyButton.Content = "Copied!";
            CopyKeyButton.Background = Brushes.Green;

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };

            timer.Tick += (s, args) =>
            {
                CopyKeyButton.Content = originalContent;
                CopyKeyButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3BA7FF"));
                timer.Stop();
            };

            timer.Start();
        }

        // Refreshes the password, authenticator, and security-score tiles from the latest stored data.
        private void UpdateSecuritySummary()
        {
            try
            {
                LoadPasswordsInfo();
                LoadAuthenticatorsInfo();

                PasswordCountText.Text = _passwords.Count.ToString();
                AuthenticatorCountText.Text = _authenticatorCount.ToString();

                int securityScore = CalculateSecurityScore();
                SecurityScoreText.Text = $"{securityScore}%";

                UpdateSecurityScoreColor(securityScore);
            }
            catch
            {
                PasswordCountText.Text = "0";
                AuthenticatorCountText.Text = "0";
                SecurityScoreText.Text = "0%";
            }
        }

        private void UpdateSecurityScoreColor(int securityScore)
        {
            SolidColorBrush scoreBrush;

            if (securityScore >= 80)
                scoreBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A3BE8C"));
            else if (securityScore >= 50)
                scoreBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EBCB8B"));
            else
                scoreBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BF616A"));

            if (FindName("SecurityScoreIcon") is TextBlock securityIcon)
            {
                securityIcon.Foreground = scoreBrush;
            }
        }

        private void LoadPasswordsInfo()
        {
            _passwords = PasswordStorage.LoadPasswords(_username, _encryptionKey);
        }

        // Reads the encrypted authenticator store and counts entries without exposing secrets in the UI.
        private void LoadAuthenticatorsInfo()
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string onPassPath = Path.Combine(appDataPath, "OnPass");
                string authFilePath = Path.Combine(onPassPath, $"{_username}_authenticators.enc");

                if (!File.Exists(authFilePath))
                {
                    _authenticatorCount = 0;
                    return;
                }

                byte[] encryptedData = File.ReadAllBytes(authFilePath);

                byte[] iv = new byte[16];
                Array.Copy(encryptedData, 0, iv, 0, 16);

                byte[] actualEncryptedData = new byte[encryptedData.Length - 16];
                Array.Copy(encryptedData, 16, actualEncryptedData, 0, actualEncryptedData.Length);

                string decryptedData = AesEncryption.Decrypt(actualEncryptedData, _encryptionKey, iv);
                string[] entries = decryptedData.Split(new[] { "||ENTRY||" }, StringSplitOptions.RemoveEmptyEntries);
                _authenticatorCount = entries.Length;
            }
            catch
            {
                _authenticatorCount = 0;
            }
        }

        // Combines password strength and authenticator coverage into a simple user-facing security score.
        private int CalculateSecurityScore()
        {
            if (_passwords.Count == 0)
            {
                return _authenticatorCount > 0 ? 70 : 30;
            }

            int passwordPoints = 0;
            int maxPasswordPoints = _passwords.Count * 100;

            foreach (var password in _passwords)
            {
                if (!string.IsNullOrEmpty(password.Password))
                {
                    passwordPoints += EvaluatePasswordStrength(password.Password);
                }
            }

            double passwordScore = (double)passwordPoints / maxPasswordPoints * 70;
            double twoFactorScore = 0;

            if (_authenticatorCount > 0)
            {
                double twoFactorRatio = Math.Min(1.0, (double)_authenticatorCount / _passwords.Count);
                twoFactorScore = twoFactorRatio * 30;
            }

            int score = (int)Math.Round(passwordScore + twoFactorScore);
            return Math.Max(0, Math.Min(100, score));
        }

        // Applies a simple strength heuristic that rewards length and character-set diversity.
        private int EvaluatePasswordStrength(string password)
        {
            int score = 0;

            if (password.Length >= 16) score += 40;
            else if (password.Length >= 12) score += 30;
            else if (password.Length >= 8) score += 20;
            else if (password.Length >= 6) score += 10;

            bool hasUpperCase = Regex.IsMatch(password, "[A-Z]");
            bool hasLowerCase = Regex.IsMatch(password, "[a-z]");
            bool hasDigit = Regex.IsMatch(password, "[0-9]");
            bool hasSpecialChar = Regex.IsMatch(password, "[^a-zA-Z0-9]");

            if (hasUpperCase) score += 15;
            if (hasLowerCase) score += 15;
            if (hasDigit) score += 15;
            if (hasSpecialChar) score += 15;

            return score;
        }

        // Clears the clipboard only if the copied value is still present after the timer expires.
        private async Task ClearClipboardIfUnchangedAsync(string copiedText, int seconds = 30)
        {
            try
            {
                // Only clear the clipboard if OnPass still owns the copied value so
                // newer clipboard content from the user is not wiped unexpectedly.
                await Task.Delay(TimeSpan.FromSeconds(seconds));

                if (Clipboard.ContainsText() && Clipboard.GetText() == copiedText)
                {
                    Clipboard.Clear();
                }
            }
            catch
            {
                // Ignore clipboard access failures.
            }
        }
    }
}


