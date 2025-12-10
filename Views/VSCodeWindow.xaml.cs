using System.IO;
using System.Net.Http;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Renci.SshNet;
using VPSManager.Models;

namespace VPSManager.Views;

public partial class VSCodeWindow : Window
{
    private readonly Server _server;
    private readonly string _remotePath;
    private readonly int _port;
    private SshClient? _sshClient;
    private ForwardedPortLocal? _tunnel;
    private bool _isClosing;

    public VSCodeWindow(Server server, string remotePath)
    {
        InitializeComponent();

        _server = server;
        _remotePath = remotePath;
        // Use consistent port based on server host hash (so same server = same port = same session)
        _port = 10000 + Math.Abs(server.Host.GetHashCode() % 50000);

        TitleText.Text = $"VS Code - {server.DisplayName} - {remotePath}";
        Title = $"VS Code - {server.DisplayName}";

        // Set owner to prevent closing main window
        Owner = Application.Current.MainWindow;

        Loaded += VSCodeWindow_Loaded;
        Closed += VSCodeWindow_Closed;
    }

    private async void VSCodeWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await InitializeWebView();
            await StartCodeServer();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            MessageBox.Show($"Failed to start code-server:\n{ex.Message}\n\nMake sure code-server is installed on the server:\ncurl -fsSL https://code-server.dev/install.sh | sh",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task InitializeWebView()
    {
        StatusText.Text = "Initializing WebView...";
        await WebView.EnsureCoreWebView2Async();

        WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
        WebView.CoreWebView2.Settings.AreHostObjectsAllowed = true;
        WebView.CoreWebView2.Settings.IsWebMessageEnabled = true;

        // Handle popup windows (OAuth login flows) - open in system browser
        WebView.CoreWebView2.NewWindowRequested += (s, e) =>
        {
            var uri = e.Uri;
            System.Diagnostics.Debug.WriteLine($"New window requested: {uri}");

            // Check if this is an OAuth/auth URL that needs system browser
            if (uri.Contains("oauth") || uri.Contains("auth") || uri.Contains("login") ||
                uri.Contains("claude.ai") || uri.Contains("anthropic.com") ||
                uri.Contains("github.com/login") || uri.Contains("accounts.google"))
            {
                // Open in system default browser
                e.Handled = true;
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = uri,
                    UseShellExecute = true
                });
            }
            else
            {
                // Allow internal popups within WebView
                e.Handled = false;
            }
        };

        WebView.CoreWebView2.NavigationStarting += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"Navigation starting: {e.Uri}");
        };

        WebView.CoreWebView2.NavigationCompleted += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"Navigation completed. Success: {e.IsSuccess}");
            if (!e.IsSuccess)
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = $"Failed to load: {e.WebErrorStatus}";
                    LoadingOverlay.Visibility = Visibility.Visible;
                });
            }
        };
    }

    private async Task StartCodeServer()
    {
        StatusText.Text = "Connecting to server...";

        var connectionInfo = CreateConnectionInfo();
        _sshClient = new SshClient(connectionInfo);

        await Task.Run(() => _sshClient.Connect());

        if (!_sshClient.IsConnected)
        {
            throw new Exception("Failed to connect to server");
        }

        StatusText.Text = $"Setting up SSH tunnel on port {_port}...";

        try
        {
            _tunnel = new ForwardedPortLocal("127.0.0.1", (uint)_port, "127.0.0.1", (uint)_port);
            _sshClient.AddForwardedPort(_tunnel);
            _tunnel.Start();
            System.Diagnostics.Debug.WriteLine($"Tunnel started: local {_port} -> remote {_port}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to create SSH tunnel on port {_port}: {ex.Message}");
        }

        StatusText.Text = "Finding code-server...";

        using var findCmd = _sshClient.CreateCommand(
            "for p in /usr/bin/code-server /usr/local/bin/code-server /snap/bin/code-server $HOME/.local/bin/code-server; do " +
            "  if [ -x \"$p\" ]; then echo $p; exit 0; fi; " +
            "done; " +
            "which code-server 2>/dev/null || echo 'not found'");
        var findResult = await Task.Run(() => findCmd.Execute());
        var codeServerPath = findResult.Trim().Split('\n')[0];

        System.Diagnostics.Debug.WriteLine($"code-server search result: '{findResult}'");
        System.Diagnostics.Debug.WriteLine($"code-server path: '{codeServerPath}'");

        if (codeServerPath == "not found" || string.IsNullOrEmpty(codeServerPath))
        {
            throw new Exception($"code-server is not installed on the server.\n\nSearch result: {findResult}\n\nInstall it with:\ncurl -fsSL https://code-server.dev/install.sh | sh");
        }

        StatusText.Text = $"Starting code-server from {codeServerPath}...";

        // Create persistent config directories and default settings
        using var mkdirCmd = _sshClient.CreateCommand("mkdir -p ~/.code-server/data/User ~/.code-server/extensions");
        await Task.Run(() => mkdirCmd.Execute());

        // Create default settings if not exists (disable welcome tab, restore session)
        var defaultSettings = @"
{
    ""workbench.startupEditor"": ""none"",
    ""window.restoreWindows"": ""all"",
    ""workbench.colorTheme"": ""Default Dark Modern"",
    ""editor.fontSize"": 14,
    ""editor.wordWrap"": ""on"",
    ""files.autoSave"": ""afterDelay"",
    ""files.autoSaveDelay"": 1000
}";
        using var settingsCmd = _sshClient.CreateCommand(
            $"[ -f ~/.code-server/data/User/settings.json ] || echo '{defaultSettings.Replace("'", "'\\''")}' > ~/.code-server/data/User/settings.json");
        await Task.Run(() => settingsCmd.Execute());

        // Check if code-server is already running on this port (reuse existing session)
        StatusText.Text = "Checking for existing code-server session...";
        using var checkExistingCmd = _sshClient.CreateCommand($"curl -s -o /dev/null -w '%{{http_code}}' http://127.0.0.1:{_port}/ 2>/dev/null || echo '000'");
        var existingStatus = (await Task.Run(() => checkExistingCmd.Execute())).Trim();

        bool alreadyRunning = existingStatus == "200" || existingStatus == "302" || existingStatus == "304";

        if (!alreadyRunning)
        {
            StatusText.Text = "Starting new code-server instance...";

            // Kill any stale code-server on this specific port only
            using var killCmd = _sshClient.CreateCommand($"pkill -f 'code-server.*--port {_port}' 2>/dev/null; sleep 1");
            await Task.Run(() => killCmd.Execute());

            // Use persistent user-data-dir and extensions-dir so settings/extensions are saved
            var startCommand = $"bash -c 'nohup {codeServerPath} --port {_port} --auth none --bind-addr 127.0.0.1:{_port} --user-data-dir ~/.code-server/data --extensions-dir ~/.code-server/extensions \"{_remotePath}\" > /tmp/code-server-{_port}.log 2>&1 & echo $!'";

            using var cmd = _sshClient.CreateCommand(startCommand);
            var pidResult = await Task.Run(() => cmd.Execute());
            var pid = pidResult.Trim();
            System.Diagnostics.Debug.WriteLine($"code-server started with PID: {pid}");

            await Task.Delay(2000);

            using var checkPidCmd = _sshClient.CreateCommand($"ps -p {pid} > /dev/null 2>&1 && echo 'running' || echo 'stopped'");
            var pidStatus = (await Task.Run(() => checkPidCmd.Execute())).Trim();

            if (pidStatus == "stopped")
            {
                using var logCmd = _sshClient.CreateCommand($"cat /tmp/code-server-{_port}.log 2>/dev/null");
                var logResult = await Task.Run(() => logCmd.Execute());
                throw new Exception($"code-server exited immediately.\n\nPID: {pid}\nLog:\n{logResult}");
            }
        }
        else
        {
            StatusText.Text = "Connecting to existing code-server session...";
            System.Diagnostics.Debug.WriteLine($"Reusing existing code-server on port {_port}");
        }

        StatusText.Text = "Waiting for code-server to be ready...";

        var maxRetries = 30;
        var started = false;

        for (int i = 0; i < maxRetries; i++)
        {
            await Task.Delay(1000);

            try
            {
                using var psCmd = _sshClient.CreateCommand($"pgrep -f 'code-server.*--port {_port}' || echo 'not running'");
                var psResult = await Task.Run(() => psCmd.Execute());

                if (!psResult.Contains("not running"))
                {
                    using var checkCmd = _sshClient.CreateCommand($"curl -s -o /dev/null -w '%{{http_code}}' http://127.0.0.1:{_port}/ 2>/dev/null || echo '000'");
                    var result = await Task.Run(() => checkCmd.Execute());
                    var code = result.Trim();

                    if (code == "200" || code == "302" || code == "304")
                    {
                        started = true;
                        break;
                    }
                }
            }
            catch { }

            StatusText.Text = $"Waiting for code-server to start... ({i + 1}/{maxRetries})";
        }

        if (!started)
        {
            using var logCmd = _sshClient.CreateCommand($"cat /tmp/code-server-{_port}.log 2>/dev/null | tail -30");
            var logResult = await Task.Run(() => logCmd.Execute());

            throw new Exception($"code-server failed to start.\n\nPath: {codeServerPath}\nPort: {_port}\n\nLog output:\n{logResult}");
        }

        StatusText.Text = "Verifying tunnel connection...";
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            var response = await httpClient.GetAsync($"http://127.0.0.1:{_port}/");
            System.Diagnostics.Debug.WriteLine($"Local tunnel test: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            throw new Exception($"SSH tunnel not working. Cannot reach code-server through tunnel.\n\nPort: {_port}\nError: {ex.Message}");
        }

        var url = $"http://127.0.0.1:{_port}/?folder={Uri.EscapeDataString(_remotePath)}";
        System.Diagnostics.Debug.WriteLine($"Loading URL: {url}");

        await Dispatcher.InvokeAsync(() =>
        {
            StatusText.Text = $"Loading {url}...";
            WebView.Source = new Uri(url);
        });

        await Task.Delay(500);
        await Dispatcher.InvokeAsync(() =>
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        });
    }

    private ConnectionInfo CreateConnectionInfo()
    {
        AuthenticationMethod authMethod;

        if (_server.UsePrivateKey && !string.IsNullOrEmpty(_server.PrivateKey))
        {
            var keyStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_server.PrivateKey));
            var keyFile = string.IsNullOrEmpty(_server.Passphrase)
                ? new PrivateKeyFile(keyStream)
                : new PrivateKeyFile(keyStream, _server.Passphrase);
            authMethod = new PrivateKeyAuthenticationMethod(_server.Username, keyFile);
        }
        else
        {
            authMethod = new PasswordAuthenticationMethod(_server.Username, _server.Password);
        }

        return new ConnectionInfo(_server.Host, _server.Port, _server.Username, authMethod)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        WebView.Reload();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // Use Closed event (not Closing) to cleanup - runs synchronously after window closes
    private void VSCodeWindow_Closed(object? sender, EventArgs e)
    {
        if (_isClosing) return;
        _isClosing = true;

        // Only cleanup SSH tunnel, DO NOT kill code-server (keep session alive for next time)
        try
        {
            _tunnel?.Stop();
            _sshClient?.Disconnect();
            _sshClient?.Dispose();
        }
        catch { }
    }
}
