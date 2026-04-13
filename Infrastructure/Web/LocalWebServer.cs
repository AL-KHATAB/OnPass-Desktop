using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;

using OnPass.Domain;
using OnPass.Infrastructure.Storage;

namespace OnPass.Infrastructure.Web;
// Hosts the localhost API that the browser extension uses to validate the
// current desktop session and request decrypted passwords on demand.
public class LocalWebServer
{
    private const int PrimaryPort = 9876;
    private const int FallbackPort = 9877;

    private HttpListener _listener;
    private string _username;
    private byte[] _encryptionKey;
    private static string? _accessToken;
    private static bool _isRunning = false;
    private Task? _processingTask;
    private CancellationTokenSource _cancellationTokenSource;

    public LocalWebServer(string username, byte[] encryptionKey)
    {
        _username = username;
        _encryptionKey = encryptionKey;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{PrimaryPort}/");
        _cancellationTokenSource = new CancellationTokenSource();

        // The extension expects one token per running desktop session, so reuse it
        // until the local server is stopped and a new session is started.
        if (_accessToken == null || !_isRunning)
        {
            _accessToken = GenerateAccessToken();
        }
    }

    // Starts listening on localhost and keeps processing extension requests until shutdown.
    public void Start()
    {
        if (_isRunning) return;

        try
        {
            try
            {
                _listener.Start();
            }
            catch (HttpListenerException)
            {
                // Some systems already bind the primary port, so fall back to a
                // secondary localhost port instead of failing the whole login flow.
                _listener.Close();
                _listener = new HttpListener();
                _listener.Prefixes.Clear();
                _listener.Prefixes.Add($"http://localhost:{FallbackPort}/");
                _listener.Start();
            }

            _isRunning = true;

            _processingTask = Task.Run(async () =>
            {
                var token = _cancellationTokenSource.Token;

                while (_isRunning && !token.IsCancellationRequested)
                {
                    try
                    {
                        var getContextTask = _listener.GetContextAsync();
                        HttpListenerContext context = await getContextTask.ConfigureAwait(false);

                        if (token.IsCancellationRequested)
                            break;

                        ProcessRequest(context);
                    }
                    catch (HttpListenerException)
                    {
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"WebServer error: {ex.Message}");
                    }
                }
            }, _cancellationTokenSource.Token);
        }
        catch (HttpListenerException ex)
        {
            Console.WriteLine($"Failed to start web server: {ex.Message}");
            _isRunning = false;
        }
    }

    // Stops the listener, cancels the background loop, and invalidates the current session token.
    public void Stop()
    {
        if (!_isRunning) return;

        try
        {
            _isRunning = false;
            _cancellationTokenSource.Cancel();

            try
            {
                if (_processingTask != null)
                {
                    Task.WaitAny(new[] { _processingTask }, 1000);
                }
            }
            catch { }

            _listener.Stop();
            _listener.Close();
            _accessToken = null;
        }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping web server: {ex.Message}");
        }
    }

    // Routes extension requests, handles CORS preflight, and guards endpoints with the bearer token.
    private void ProcessRequest(HttpListenerContext context)
    {
        try
        {
            // Browser fetches to localhost still need explicit CORS headers so the
            // extension can send Authorization headers and preflight requests.
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
            context.Response.Headers.Add("Access-Control-Allow-Headers", "Authorization");

            if (context.Request.HttpMethod == "OPTIONS")
            {
                context.Response.StatusCode = 200;
                context.Response.Close();
                return;
            }

            if (context.Request.HttpMethod == "GET" && context.Request.Url!.LocalPath == "/validate")
            {
                // The extension uses a cheap validation endpoint so it can confirm
                // the desktop session is still alive before asking for passwords.
                string authHeaderVal = context.Request.Headers["Authorization"] ?? string.Empty;
                if (string.IsNullOrEmpty(authHeaderVal) || !authHeaderVal.StartsWith("Bearer ") || authHeaderVal.Substring(7) != _accessToken)
                {
                    context.Response.StatusCode = 401;
                }
                else
                {
                    context.Response.StatusCode = 200;
                    byte[] responseBuffer = Encoding.UTF8.GetBytes("{\"valid\": true}");
                    context.Response.ContentType = "application/json";
                    context.Response.ContentLength64 = responseBuffer.Length;
                    context.Response.OutputStream.Write(responseBuffer, 0, responseBuffer.Length);
                }
                context.Response.Close();
                return;
            }

            if (context.Request.HttpMethod != "GET" || context.Request.Url!.LocalPath != "/passwords")
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            string authHeader = context.Request.Headers["Authorization"] ?? string.Empty;
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ") || authHeader.Substring(7) != _accessToken)
            {
                context.Response.StatusCode = 401;
                context.Response.Close();
                return;
            }

            // Passwords are decrypted on demand inside the trusted desktop process
            // and only returned to the extension after bearer-token validation.
            List<PasswordItem> passwords = PasswordStorage.LoadPasswords(_username, _encryptionKey);
            string jsonPasswords = JsonSerializer.Serialize(passwords);
            byte[] buffer = Encoding.UTF8.GetBytes(jsonPasswords);

            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing request: {ex.Message}");
            try
            {
                context.Response.StatusCode = 500;
            }
            catch { }
        }
        finally
        {
            try
            {
                context.Response.Close();
            }
            catch { }
        }
    }

    // Generates a high-entropy token that represents the currently logged-in desktop session.
    private static string GenerateAccessToken()
    {
        byte[] tokenData = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(tokenData);
    }

    public string GetAccessToken() => _accessToken!;
}
