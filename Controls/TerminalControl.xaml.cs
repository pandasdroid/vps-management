using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;

namespace VPSManager.Controls;

public partial class TerminalControl : UserControl
{
    private bool _isInitialized;
    private readonly Queue<string> _pendingWrites = new();

    public event Action<string>? InputReceived;
    public event Action<int, int>? TerminalSizeChanged;

    public TerminalControl()
    {
        InitializeComponent();
        Loaded += TerminalControl_Loaded;
    }

    private async void TerminalControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized) return;

        try
        {
            // Initialize WebView2
            var env = await CoreWebView2Environment.CreateAsync();
            await WebView.EnsureCoreWebView2Async(env);

            // Set up message handler
            WebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

            // Disable context menu and dev tools in production
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            WebView.CoreWebView2.Settings.AreDevToolsEnabled = false;

            // Navigate to terminal HTML
            var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "terminal.html");
            if (File.Exists(htmlPath))
            {
                WebView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
            }
            else
            {
                // Fallback: load from embedded resource or show error
                LoadingOverlay.Visibility = Visibility.Visible;
                return;
            }

            WebView.CoreWebView2.NavigationCompleted += (s, args) =>
            {
                if (args.IsSuccess)
                {
                    _isInitialized = true;
                    LoadingOverlay.Visibility = Visibility.Collapsed;

                    // Write any pending data
                    while (_pendingWrites.TryDequeue(out var data))
                    {
                        WriteDataInternal(data);
                    }
                }
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebView2 init error: {ex.Message}");
        }
    }

    private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var message = e.WebMessageAsJson;
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;

            var type = root.GetProperty("type").GetString();

            switch (type)
            {
                case "input":
                    var data = root.GetProperty("data").GetString();
                    if (data != null)
                    {
                        InputReceived?.Invoke(data);
                    }
                    break;

                case "resize":
                    var cols = root.GetProperty("cols").GetInt32();
                    var rows = root.GetProperty("rows").GetInt32();
                    TerminalSizeChanged?.Invoke(cols, rows);
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Message parse error: {ex.Message}");
        }
    }

    public void WriteData(string data)
    {
        if (!_isInitialized)
        {
            _pendingWrites.Enqueue(data);
            return;
        }

        Dispatcher.BeginInvoke(() => WriteDataInternal(data));
    }

    private async void WriteDataInternal(string data)
    {
        if (WebView.CoreWebView2 == null) return;

        try
        {
            // Escape the data for JavaScript
            var escaped = JsonSerializer.Serialize(data);
            await WebView.CoreWebView2.ExecuteScriptAsync($"writeData({escaped})");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WriteData error: {ex.Message}");
        }
    }

    public async void Clear()
    {
        if (!_isInitialized || WebView.CoreWebView2 == null) return;

        try
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                await WebView.CoreWebView2.ExecuteScriptAsync("clearTerminal()");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Clear error: {ex.Message}");
        }
    }

    public async void FocusTerminal()
    {
        if (!_isInitialized || WebView.CoreWebView2 == null) return;

        try
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                WebView.Focus();
                await WebView.CoreWebView2.ExecuteScriptAsync("focusTerminal()");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Focus error: {ex.Message}");
        }
    }
}
