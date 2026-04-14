using System;
using System.Text;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading.Tasks;

namespace OnPass.Presentation.Controls
{
    // Generates strong passwords from configurable character sets, estimates
    // strength, and supports secure clipboard handling after copy operations.
    public partial class GeneratePasswordControl : UserControl
    {
        private string _username;
        private byte[] _encryptionKey;

        private const string UppercaseChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string LowercaseChars = "abcdefghijklmnopqrstuvwxyz";
        private const string NumberChars = "0123456789";
        private const string SpecialChars = "!@#$%^&*()_+-=[]{}|;:,.<>?";
        private const string SimilarChars = "0O1lI";
        private const string AmbiguousChars = "{}[]()/'\"~,;.<>";
        public GeneratePasswordControl(string username, byte[] encryptionKey)
        {
            InitializeComponent();
            _username = username;
            _encryptionKey = encryptionKey;

            GenerateNewPassword();

            LengthSlider.ValueChanged += (s, e) => UpdatePasswordStrength();

            UppercaseToggle.Checked += (s, e) => UpdatePasswordStrength();
            UppercaseToggle.Unchecked += (s, e) => UpdatePasswordStrength();
            LowercaseToggle.Checked += (s, e) => UpdatePasswordStrength();
            LowercaseToggle.Unchecked += (s, e) => UpdatePasswordStrength();
            NumbersToggle.Checked += (s, e) => UpdatePasswordStrength();
            NumbersToggle.Unchecked += (s, e) => UpdatePasswordStrength();
            SpecialToggle.Checked += (s, e) => UpdatePasswordStrength();
            SpecialToggle.Unchecked += (s, e) => UpdatePasswordStrength();
        }

        // Regenerates a new password using the current slider and toggle selections.
        private void GeneratePassword_Click(object sender, RoutedEventArgs e)
        {
            GenerateNewPassword();
        }

        // Builds the password from the current UI options and updates the strength meter immediately afterwards.
        private void GenerateNewPassword()
        {
            try
            {
                int length = (int)LengthSlider.Value;
                string characterSet = BuildCharacterSet();

                if (string.IsNullOrEmpty(characterSet))
                {
                    MessageBox.Show("Please select at least one character type.", "Error",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string password = GenerateSecurePassword(characterSet, length);
                GeneratedPasswordTextBox.Text = password;
                UpdatePasswordStrength();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating password: {ex.Message}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Assembles the active character pool, optionally removing visually confusing characters.
        private string BuildCharacterSet()
        {
            StringBuilder charSet = new StringBuilder();

            if (UppercaseToggle.IsChecked == true)
                charSet.Append(UppercaseChars);

            if (LowercaseToggle.IsChecked == true)
                charSet.Append(LowercaseChars);

            if (NumbersToggle.IsChecked == true)
                charSet.Append(NumberChars);

            if (SpecialToggle.IsChecked == true)
                charSet.Append(SpecialChars);

            string result = charSet.ToString();

            if (ExcludeSimilarToggle.IsChecked == true)
            {
                foreach (char c in SimilarChars)
                {
                    result = result.Replace(c.ToString(), "");
                }
            }

            if (ExcludeAmbiguousToggle.IsChecked == true)
            {
                foreach (char c in AmbiguousChars)
                {
                    result = result.Replace(c.ToString(), "");
                }
            }

            return result;
        }

        // Uses a cryptographic RNG so generated passwords do not depend on predictable UI-thread randomness.
        private string GenerateSecurePassword(string characterSet, int length)
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                // RandomNumberGenerator keeps password generation on a cryptographic
                // source instead of relying on UI-thread pseudo-random state.
                StringBuilder password = new StringBuilder();
                byte[] randomBytes = new byte[4];

                for (int i = 0; i < length; i++)
                {
                    rng.GetBytes(randomBytes);
                    uint randomValue = BitConverter.ToUInt32(randomBytes, 0);
                    int index = (int)(randomValue % characterSet.Length);
                    password.Append(characterSet[index]);
                }

                return password.ToString();
            }
        }

        // Recomputes the strength meter whenever generation settings change.
        private void UpdatePasswordStrength()
        {
            try
            {
                int length = (int)LengthSlider.Value;
                int characterTypes = 0;

                if (UppercaseToggle.IsChecked == true) characterTypes++;
                if (LowercaseToggle.IsChecked == true) characterTypes++;
                if (NumbersToggle.IsChecked == true) characterTypes++;
                if (SpecialToggle.IsChecked == true) characterTypes++;

                int strengthScore = CalculatePasswordStrength(length, characterTypes);

                StrengthProgressBar.Value = strengthScore;

                UpdateStrengthDisplay(strengthScore);
            }
            catch (Exception)
            {
                StrengthProgressBar.Value = 0;
                StrengthLabel.Text = "Unknown";
            }
        }

        // Converts length and enabled character classes into a simple user-facing strength score.
        private int CalculatePasswordStrength(int length, int characterTypes)
        {
            int score = 0;

            if (length >= 8) score += 20;
            if (length >= 12) score += 15;
            if (length >= 16) score += 10;
            if (length >= 20) score += 5;

            score += characterTypes * 15;

            return Math.Min(score, 100);
        }

        // Updates the strength label and progress-bar color to match the current score.
        private void UpdateStrengthDisplay(int score)
        {
            if (score < 30)
            {
                StrengthLabel.Text = "Weak";
                StrengthLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xBF, 0x61, 0x6A)); // Red
                StrengthProgressBar.Foreground = new SolidColorBrush(Color.FromRgb(0xBF, 0x61, 0x6A));
            }
            else if (score < 60)
            {
                StrengthLabel.Text = "Fair";
                StrengthLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xD0, 0x8F, 0x70)); // Orange
                StrengthProgressBar.Foreground = new SolidColorBrush(Color.FromRgb(0xD0, 0x8F, 0x70));
            }
            else if (score < 80)
            {
                StrengthLabel.Text = "Good";
                StrengthLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xEB, 0xCB, 0x8B)); // Yellow
                StrengthProgressBar.Foreground = new SolidColorBrush(Color.FromRgb(0xEB, 0xCB, 0x8B));
            }
            else
            {
                StrengthLabel.Text = "Very Strong";
                StrengthLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xA3, 0xBE, 0x8C)); // Green
                StrengthProgressBar.Foreground = new SolidColorBrush(Color.FromRgb(0xA3, 0xBE, 0x8C));
            }
        }

        // Copies the generated password and uses the same delayed clipboard clearing policy as the rest of the app.
        private void CopyPassword_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(GeneratedPasswordTextBox.Text) &&
                    GeneratedPasswordTextBox.Text != "Click Generate to create password")
                {
                    Clipboard.SetText(GeneratedPasswordTextBox.Text);
                    _ = ClearClipboardIfUnchangedAsync(GeneratedPasswordTextBox.Text);

                    var bstton = sender as Button;
                    var originalBrush = bstton?.Foreground;

                    if (bstton != null)
                    {
                        bstton.Foreground = new SolidColorBrush(Color.FromRgb(0xA3, 0xBE, 0x8C)); // Green

                        var timer = new System.Windows.Threading.DispatcherTimer();
                        timer.Interval = TimeSpan.FromSeconds(2);
                        timer.Tick += (s, args) =>
                        {
                            bstton.Foreground = originalBrush;
                            timer.Stop();
                        };
                        timer.Start();
                    }

                    MessageBox.Show("Password copied to clipboard!", "Success",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Please generate a password first.", "No Password",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error copying password: {ex.Message}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // Clears the clipboard only if the generated password is still the current clipboard contents.
        private async Task ClearClipboardIfUnchangedAsync(string copiedText, int seconds = 30)
        {
            try
            {
                // Match the dashboard and authenticator behavior so generated
                // passwords do not linger in the clipboard after copy operations.
                await Task.Delay(TimeSpan.FromSeconds(seconds));

                if (Clipboard.ContainsText() && Clipboard.GetText() == copiedText)
                {
                    Clipboard.Clear();
                }
            }
            catch
            {
                // Ignore clipboard access failsres.
            }
        }
    }
}

