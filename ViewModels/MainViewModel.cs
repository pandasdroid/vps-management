using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using VPSManager.Models;
using VPSManager.Services;

namespace VPSManager.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly StorageService _storage = new();
    private readonly SshService _ssh = new();
    private readonly DispatcherTimer _statsTimer;

    [ObservableProperty]
    private ObservableCollection<Server> _servers = [];

    [ObservableProperty]
    private Server? _selectedServer;

    [ObservableProperty]
    private ConnectionStatus _connectionStatus = ConnectionStatus.Disconnected;

    [ObservableProperty]
    private ServerStats? _currentStats;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isTerminalConnected;

    // File Manager
    [ObservableProperty]
    private ObservableCollection<FileItem> _files = [];

    [ObservableProperty]
    private string _currentPath = "/";

    [ObservableProperty]
    private FileItem? _selectedFile;

    [ObservableProperty]
    private int _selectedTabIndex;

    // Events for terminal
    public event Action<string>? TerminalOutput;
    public event Action? TerminalClear;

    public MainViewModel()
    {
        // Load servers asynchronously
        Application.Current.Dispatcher.BeginInvoke(LoadServers, DispatcherPriority.Background);

        _statsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _statsTimer.Tick += async (s, e) => await RefreshStatsAsync();
    }

    private void LoadServers()
    {
        var servers = _storage.LoadServers();
        Servers = new ObservableCollection<Server>(servers);
    }

    partial void OnSelectedServerChanged(Server? value)
    {
        if (value != null)
        {
            ConnectionStatus = _ssh.IsConnected(value.Id)
                ? ConnectionStatus.Connected
                : ConnectionStatus.Disconnected;
            CurrentStats = null;
            IsTerminalConnected = false;
            TerminalClear?.Invoke();
            Files.Clear();
            CurrentPath = "/";
        }
        else
        {
            _statsTimer.Stop();
            ConnectionStatus = ConnectionStatus.Disconnected;
            CurrentStats = null;
            IsTerminalConnected = false;
            TerminalClear?.Invoke();
            Files.Clear();
        }
    }

    [RelayCommand]
    private void AddServer()
    {
        var dialog = new Views.ServerDialog();
        if (dialog.ShowDialog() == true && dialog.Server != null)
        {
            _storage.AddServer(dialog.Server);
            Servers.Add(dialog.Server);
            SelectedServer = dialog.Server;
            StatusMessage = $"Server '{dialog.Server.DisplayName}' added";
        }
    }

    [RelayCommand]
    private void EditServer()
    {
        if (SelectedServer == null) return;

        var dialog = new Views.ServerDialog(SelectedServer);
        if (dialog.ShowDialog() == true && dialog.Server != null)
        {
            _storage.UpdateServer(dialog.Server);
            var index = Servers.IndexOf(SelectedServer);
            if (index >= 0)
            {
                Servers[index] = dialog.Server;
                SelectedServer = dialog.Server;
            }
            StatusMessage = $"Server '{dialog.Server.DisplayName}' updated";
        }
    }

    [RelayCommand]
    private void DeleteServer()
    {
        if (SelectedServer == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete '{SelectedServer.DisplayName}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _ssh.Disconnect(SelectedServer.Id);
            _storage.DeleteServer(SelectedServer.Id);
            var name = SelectedServer.DisplayName;
            Servers.Remove(SelectedServer);
            SelectedServer = null;
            StatusMessage = $"Server '{name}' deleted";
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (SelectedServer == null) return;

        if (ConnectionStatus == ConnectionStatus.Connected)
        {
            await Task.Run(() => _ssh.Disconnect(SelectedServer.Id));
            _statsTimer.Stop();
            ConnectionStatus = ConnectionStatus.Disconnected;
            CurrentStats = null;
            IsTerminalConnected = false;
            TerminalClear?.Invoke();
            Files.Clear();
            StatusMessage = "Disconnected";
            return;
        }

        try
        {
            IsLoading = true;
            ConnectionStatus = ConnectionStatus.Connecting;
            StatusMessage = "Connecting...";

            var server = SelectedServer;
            var success = await Task.Run(() => _ssh.ConnectAsync(server)).ConfigureAwait(true);

            if (success && SelectedServer?.Id == server.Id)
            {
                ConnectionStatus = ConnectionStatus.Connected;
                StatusMessage = $"Connected to {SelectedServer.DisplayName}";

                // Start terminal first (quick operation)
                StartTerminal();

                // Then fetch stats in background
                _ = RefreshStatsAsync();
                _ = LoadFilesAsync();
                _statsTimer.Start();
            }
            else
            {
                ConnectionStatus = ConnectionStatus.Error;
                StatusMessage = "Connection failed";
            }
        }
        catch (Exception ex)
        {
            ConnectionStatus = ConnectionStatus.Error;
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshStatsAsync()
    {
        if (SelectedServer == null || ConnectionStatus != ConnectionStatus.Connected)
            return;

        try
        {
            var serverId = SelectedServer.Id;
            var stats = await Task.Run(() => _ssh.GetServerStatsAsync(serverId)).ConfigureAwait(true);

            if (SelectedServer?.Id == serverId)
            {
                CurrentStats = stats;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to get stats: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RebootServerAsync()
    {
        if (SelectedServer == null || ConnectionStatus != ConnectionStatus.Connected)
            return;

        // Show password dialog
        var dialog = new Views.PasswordDialog(
            "Reboot Server",
            $"Enter sudo password to reboot '{SelectedServer.DisplayName}'");

        if (dialog.ShowDialog() != true) return;

        try
        {
            IsLoading = true;
            StatusMessage = "Sending reboot command...";

            var serverId = SelectedServer.Id;
            var password = dialog.Password;

            var result = await Task.Run(async () =>
            {
                // Use echo to pipe password to sudo
                return await _ssh.ExecuteCommandAsync(serverId, $"echo '{password}' | sudo -S reboot");
            }).ConfigureAwait(true);

            // Check if there was an error
            if (result.Contains("incorrect password", StringComparison.OrdinalIgnoreCase) ||
                result.Contains("sorry", StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = "Reboot failed: incorrect password";
                return;
            }

            await Task.Run(() => _ssh.Disconnect(serverId));
            _statsTimer.Stop();
            ConnectionStatus = ConnectionStatus.Disconnected;
            CurrentStats = null;
            IsTerminalConnected = false;
            Files.Clear();
            StatusMessage = "Reboot command sent";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Reboot failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ShutdownServerAsync()
    {
        if (SelectedServer == null || ConnectionStatus != ConnectionStatus.Connected)
            return;

        // Show password dialog
        var dialog = new Views.PasswordDialog(
            "Shutdown Server",
            $"Enter sudo password to shutdown '{SelectedServer.DisplayName}'");

        if (dialog.ShowDialog() != true) return;

        try
        {
            IsLoading = true;
            StatusMessage = "Sending shutdown command...";

            var serverId = SelectedServer.Id;
            var password = dialog.Password;

            var result = await Task.Run(async () =>
            {
                return await _ssh.ExecuteCommandAsync(serverId, $"echo '{password}' | sudo -S shutdown now");
            }).ConfigureAwait(true);

            if (result.Contains("incorrect password", StringComparison.OrdinalIgnoreCase) ||
                result.Contains("sorry", StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = "Shutdown failed: incorrect password";
                return;
            }

            await Task.Run(() => _ssh.Disconnect(serverId));
            _statsTimer.Stop();
            ConnectionStatus = ConnectionStatus.Disconnected;
            CurrentStats = null;
            IsTerminalConnected = false;
            Files.Clear();
            StatusMessage = "Shutdown command sent";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Shutdown failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // File Manager Methods
    [RelayCommand]
    private async Task LoadFilesAsync()
    {
        if (SelectedServer == null || ConnectionStatus != ConnectionStatus.Connected)
            return;

        try
        {
            var serverId = SelectedServer.Id;
            var path = CurrentPath;

            var output = await Task.Run(async () =>
            {
                return await _ssh.ExecuteCommandAsync(serverId,
                    $"ls -la '{path}' 2>/dev/null | tail -n +2");
            }).ConfigureAwait(true);

            var files = new ObservableCollection<FileItem>();

            // Add parent directory if not at root
            if (path != "/")
            {
                files.Add(new FileItem
                {
                    Name = "..",
                    IsDirectory = true,
                    Path = GetParentPath(path)
                });
            }

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var item = ParseLsLine(line, path);
                if (item != null && item.Name != "." && item.Name != "..")
                {
                    files.Add(item);
                }
            }

            Files = files;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load files: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task NavigateToAsync(FileItem? item)
    {
        if (item == null || SelectedServer == null) return;

        if (item.IsDirectory)
        {
            CurrentPath = item.Path;
            await LoadFilesAsync();
        }
    }

    [RelayCommand]
    private async Task GoUpAsync()
    {
        if (CurrentPath == "/") return;
        CurrentPath = GetParentPath(CurrentPath);
        await LoadFilesAsync();
    }

    [RelayCommand]
    private async Task RefreshFilesAsync()
    {
        await LoadFilesAsync();
    }

    [RelayCommand]
    private async Task DeleteFileAsync()
    {
        if (SelectedFile == null || SelectedServer == null || SelectedFile.Name == "..")
            return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete '{SelectedFile.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            var cmd = SelectedFile.IsDirectory
                ? $"rm -rf '{SelectedFile.Path}'"
                : $"rm -f '{SelectedFile.Path}'";

            await Task.Run(async () =>
            {
                await _ssh.ExecuteCommandAsync(SelectedServer.Id, cmd);
            }).ConfigureAwait(true);

            await LoadFilesAsync();
            StatusMessage = $"Deleted: {SelectedFile.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CreateFolderAsync()
    {
        if (SelectedServer == null || ConnectionStatus != ConnectionStatus.Connected)
            return;

        var dialog = new Views.InputDialog("Create Folder", "Folder name:");
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.InputValue))
            return;

        try
        {
            var path = CurrentPath == "/" ? $"/{dialog.InputValue}" : $"{CurrentPath}/{dialog.InputValue}";
            await Task.Run(async () =>
            {
                await _ssh.ExecuteCommandAsync(SelectedServer.Id, $"mkdir -p '{path}'");
            }).ConfigureAwait(true);

            await LoadFilesAsync();
            StatusMessage = $"Created folder: {dialog.InputValue}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Create folder failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task EditFileAsync()
    {
        if (SelectedFile == null || SelectedServer == null || SelectedFile.IsDirectory || SelectedFile.Name == "..")
            return;

        if (!SshService.IsEditable(SelectedFile.Name))
        {
            MessageBox.Show($"File type '{Path.GetExtension(SelectedFile.Name)}' is not supported for editing.",
                "Not Editable", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Loading file...";

            var serverId = SelectedServer.Id;
            var remotePath = SelectedFile.Path;

            var content = await Task.Run(async () =>
            {
                return await _ssh.ReadFileAsync(serverId, remotePath);
            }).ConfigureAwait(true);

            IsLoading = false;
            StatusMessage = "Ready";

            var editor = new Views.FileEditorDialog(remotePath, content, async (newContent) =>
            {
                await _ssh.WriteFileAsync(serverId, remotePath, newContent);
            });
            editor.Owner = Application.Current.MainWindow;
            editor.ShowDialog();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to open file: {ex.Message}";
            MessageBox.Show($"Failed to open file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DownloadFileAsync()
    {
        if (SelectedFile == null || SelectedServer == null || SelectedFile.IsDirectory || SelectedFile.Name == "..")
            return;

        var saveDialog = new SaveFileDialog
        {
            FileName = SelectedFile.Name,
            Title = "Save File As"
        };

        if (saveDialog.ShowDialog() != true)
            return;

        try
        {
            IsLoading = true;
            StatusMessage = $"Downloading {SelectedFile.Name}...";

            var serverId = SelectedServer.Id;
            var remotePath = SelectedFile.Path;
            var localPath = saveDialog.FileName;

            await Task.Run(async () =>
            {
                await _ssh.DownloadFileAsync(serverId, remotePath, localPath);
            }).ConfigureAwait(true);

            StatusMessage = $"Downloaded: {SelectedFile.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Download failed: {ex.Message}";
            MessageBox.Show($"Download failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task UploadFileAsync()
    {
        if (SelectedServer == null || ConnectionStatus != ConnectionStatus.Connected)
            return;

        var openDialog = new OpenFileDialog
        {
            Title = "Select File to Upload",
            Multiselect = false
        };

        if (openDialog.ShowDialog() != true)
            return;

        try
        {
            IsLoading = true;
            var fileName = Path.GetFileName(openDialog.FileName);
            StatusMessage = $"Uploading {fileName}...";

            var serverId = SelectedServer.Id;
            var localPath = openDialog.FileName;
            var remotePath = CurrentPath == "/" ? $"/{fileName}" : $"{CurrentPath}/{fileName}";

            await Task.Run(async () =>
            {
                await _ssh.UploadFileAsync(serverId, localPath, remotePath);
            }).ConfigureAwait(true);

            await LoadFilesAsync();
            StatusMessage = $"Uploaded: {fileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Upload failed: {ex.Message}";
            MessageBox.Show($"Upload failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ChangePermissionsAsync()
    {
        if (SelectedFile == null || SelectedServer == null || SelectedFile.Name == "..")
            return;

        var dialog = new Views.PermissionsDialog(SelectedFile.Name, SelectedFile.Permissions);
        dialog.Owner = Application.Current.MainWindow;

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            IsLoading = true;
            StatusMessage = "Changing permissions...";

            var serverId = SelectedServer.Id;
            var remotePath = SelectedFile.Path;
            var permissions = dialog.Permissions;

            await Task.Run(async () =>
            {
                await _ssh.ChangePermissionsAsync(serverId, remotePath, permissions);
            }).ConfigureAwait(true);

            await LoadFilesAsync();
            StatusMessage = $"Permissions changed for: {SelectedFile.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to change permissions: {ex.Message}";
            MessageBox.Show($"Failed to change permissions:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RenameFileAsync()
    {
        if (SelectedFile == null || SelectedServer == null || SelectedFile.Name == "..")
            return;

        var dialog = new Views.InputDialog("Rename", "New name:", SelectedFile.Name);
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.InputValue))
            return;

        if (dialog.InputValue == SelectedFile.Name)
            return;

        try
        {
            IsLoading = true;
            StatusMessage = "Renaming...";

            var serverId = SelectedServer.Id;
            var oldPath = SelectedFile.Path;
            var newPath = CurrentPath == "/" ? $"/{dialog.InputValue}" : $"{CurrentPath}/{dialog.InputValue}";

            await Task.Run(async () =>
            {
                await _ssh.ExecuteCommandAsync(serverId, $"mv '{oldPath}' '{newPath}'");
            }).ConfigureAwait(true);

            await LoadFilesAsync();
            StatusMessage = $"Renamed to: {dialog.InputValue}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Rename failed: {ex.Message}";
            MessageBox.Show($"Rename failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenTerminalHere()
    {
        if (SelectedServer == null || ConnectionStatus != ConnectionStatus.Connected)
            return;

        // Switch to Terminal tab (index 1)
        SelectedTabIndex = 1;

        // Send cd command to terminal
        if (IsTerminalConnected)
        {
            _ssh.WriteToShell(SelectedServer.Id, $"cd '{CurrentPath}'\n");
        }
    }

    [RelayCommand]
    private void OpenInVSCode()
    {
        if (SelectedServer == null || ConnectionStatus != ConnectionStatus.Connected)
            return;

        // Determine path - use selected file/folder or current path
        var path = CurrentPath;
        if (SelectedFile != null && SelectedFile.Name != ".." && SelectedFile.IsDirectory)
        {
            path = SelectedFile.Path;
        }

        // Open VS Code window with code-server
        var vsCodeWindow = new Views.VSCodeWindow(SelectedServer, path);
        vsCodeWindow.Show();
    }

    private static string GetParentPath(string path)
    {
        if (path == "/") return "/";
        var parent = path.TrimEnd('/');
        var lastSlash = parent.LastIndexOf('/');
        return lastSlash <= 0 ? "/" : parent[..lastSlash];
    }

    private static FileItem? ParseLsLine(string line, string basePath)
    {
        try
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 9) return null;

            var permissions = parts[0];
            var size = long.TryParse(parts[4], out var s) ? s : 0;
            var name = string.Join(' ', parts.Skip(8));

            // Handle symlinks
            var linkIndex = name.IndexOf(" -> ");
            if (linkIndex > 0) name = name[..linkIndex];

            var isDir = permissions.StartsWith('d');
            var isLink = permissions.StartsWith('l');

            return new FileItem
            {
                Name = name,
                IsDirectory = isDir,
                IsSymlink = isLink,
                Size = size,
                Permissions = permissions,
                Path = basePath == "/" ? $"/{name}" : $"{basePath}/{name}",
                ModifiedDate = $"{parts[5]} {parts[6]} {parts[7]}"
            };
        }
        catch
        {
            return null;
        }
    }

    private void StartTerminal()
    {
        if (SelectedServer == null) return;

        TerminalClear?.Invoke();

        _ssh.StartShell(SelectedServer.Id, data =>
        {
            TerminalOutput?.Invoke(data);
        });

        IsTerminalConnected = true;
    }

    public void SendToShell(string input)
    {
        if (SelectedServer == null || !IsTerminalConnected) return;
        _ssh.WriteToShell(SelectedServer.Id, input);
    }

    public void SaveServers()
    {
        _storage.SaveServers(Servers.ToList());
    }

    public void Cleanup()
    {
        _statsTimer.Stop();
        _ssh.Dispose();
    }
}
