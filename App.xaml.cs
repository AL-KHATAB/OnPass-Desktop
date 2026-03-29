using OnPass.Infrastructure.Web;
using System.IO;
using System.Windows;
namespace OnPass;

public partial class App : Application
{
    // App-wide session state is shared across views so the current desktop session
    // and browser-extension bridge stay consistent for the lifetime of the process.
    public static bool MinimizeToTrayEnabled { get; set; } = false;
    public static string CurrentUsername { get; set; } = string.Empty;

    public static string? CurrentAccessToken { get; set; }
    public static LocalWebServer? WebServer { get; set; }

    public static void LoadUserSettings(string username)
    {
        try
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string OnPassPath = Path.Combine(appDataPath, "OnPass");
            string settingsFilePath = Path.Combine(OnPassPath, $"{username}_settings.ini");

            if (File.Exists(settingsFilePath))
            {
                // Settings are stored as a lightweight per-user ini file so login
                // can restore security behavior before the dashboard finishes loading.
                string[] lines = File.ReadAllLines(settingsFilePath);
                foreach (string line in lines)
                {
                    if (line.StartsWith("MinimizeToTray="))
                    {
                        MinimizeToTrayEnabled = bool.Parse(line.Substring("MinimizeToTray=".Length));
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading user settings: {ex.Message}");
        }
    }

    
}
