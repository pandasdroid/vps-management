using System.IO;
using System.Text;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using VPSManager.Models;

namespace VPSManager.Services;

public class SshService : IDisposable
{
    private readonly Dictionary<string, SshClient> _connections = [];
    private readonly Dictionary<string, SftpClient> _sftpClients = [];
    private readonly Dictionary<string, ShellStream> _shells = [];

    // Editable file extensions
    private static readonly HashSet<string> EditableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".log", ".md", ".json", ".xml", ".yaml", ".yml",
        ".sh", ".bash", ".zsh", ".fish",
        ".py", ".js", ".ts", ".jsx", ".tsx", ".css", ".html", ".htm",
        ".c", ".cpp", ".h", ".hpp", ".cs", ".java", ".go", ".rs", ".rb",
        ".php", ".pl", ".lua", ".sql", ".conf", ".cfg", ".ini", ".env",
        ".toml", ".properties", ".gitignore", ".dockerignore", ".editorconfig",
        "Dockerfile", "Makefile", ".htaccess", ".nginx", ".service"
    };

    public static bool IsEditable(string filename)
    {
        var ext = Path.GetExtension(filename);
        if (string.IsNullOrEmpty(ext))
        {
            // Check for extensionless files that are editable
            var name = Path.GetFileName(filename);
            return EditableExtensions.Contains(name);
        }
        return EditableExtensions.Contains(ext);
    }

    public async Task<bool> ConnectAsync(Server server)
    {
        try
        {
            Disconnect(server.Id);

            var connectionInfo = CreateConnectionInfo(server);
            var client = new SshClient(connectionInfo);
            var sftpClient = new SftpClient(connectionInfo);

            await Task.Run(() =>
            {
                client.Connect();
                sftpClient.Connect();
            });

            if (client.IsConnected)
            {
                _connections[server.Id] = client;
                _sftpClients[server.Id] = sftpClient;
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SSH Connect Error: {ex.Message}");
        }
        return false;
    }

    public void Disconnect(string serverId)
    {
        StopShell(serverId);

        if (_sftpClients.TryGetValue(serverId, out var sftpClient))
        {
            try
            {
                sftpClient.Disconnect();
                sftpClient.Dispose();
            }
            catch { }
            _sftpClients.Remove(serverId);
        }

        if (_connections.TryGetValue(serverId, out var client))
        {
            try
            {
                client.Disconnect();
                client.Dispose();
            }
            catch { }
            _connections.Remove(serverId);
        }
    }

    public bool IsConnected(string serverId)
    {
        return _connections.TryGetValue(serverId, out var client) && client.IsConnected;
    }

    public async Task<string> ExecuteCommandAsync(string serverId, string command)
    {
        if (!_connections.TryGetValue(serverId, out var client) || !client.IsConnected)
            throw new InvalidOperationException("Not connected to server");

        return await Task.Run(() =>
        {
            using var cmd = client.CreateCommand(command);
            cmd.CommandTimeout = TimeSpan.FromSeconds(30);
            var result = cmd.Execute();
            return string.IsNullOrEmpty(result) ? cmd.Error : result;
        });
    }

    // SFTP Operations
    public async Task<string> ReadFileAsync(string serverId, string remotePath)
    {
        if (!_sftpClients.TryGetValue(serverId, out var sftp) || !sftp.IsConnected)
            throw new InvalidOperationException("SFTP not connected");

        return await Task.Run(() =>
        {
            using var stream = sftp.OpenRead(remotePath);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        });
    }

    public async Task WriteFileAsync(string serverId, string remotePath, string content)
    {
        if (!_sftpClients.TryGetValue(serverId, out var sftp) || !sftp.IsConnected)
            throw new InvalidOperationException("SFTP not connected");

        await Task.Run(() =>
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            using var stream = new MemoryStream(bytes);
            sftp.UploadFile(stream, remotePath, true);
        });
    }

    public async Task DownloadFileAsync(string serverId, string remotePath, string localPath)
    {
        if (!_sftpClients.TryGetValue(serverId, out var sftp) || !sftp.IsConnected)
            throw new InvalidOperationException("SFTP not connected");

        await Task.Run(() =>
        {
            using var fileStream = File.Create(localPath);
            sftp.DownloadFile(remotePath, fileStream);
        });
    }

    public async Task UploadFileAsync(string serverId, string localPath, string remotePath)
    {
        if (!_sftpClients.TryGetValue(serverId, out var sftp) || !sftp.IsConnected)
            throw new InvalidOperationException("SFTP not connected");

        await Task.Run(() =>
        {
            using var fileStream = File.OpenRead(localPath);
            sftp.UploadFile(fileStream, remotePath, true);
        });
    }

    public async Task ChangePermissionsAsync(string serverId, string remotePath, short permissions)
    {
        if (!_sftpClients.TryGetValue(serverId, out var sftp) || !sftp.IsConnected)
            throw new InvalidOperationException("SFTP not connected");

        await Task.Run(() =>
        {
            sftp.ChangePermissions(remotePath, permissions);
        });
    }

    public async Task<SftpFileAttributes?> GetFileAttributesAsync(string serverId, string remotePath)
    {
        if (!_sftpClients.TryGetValue(serverId, out var sftp) || !sftp.IsConnected)
            return null;

        return await Task.Run(() =>
        {
            try
            {
                return sftp.GetAttributes(remotePath);
            }
            catch
            {
                return null;
            }
        });
    }

    public async Task<ServerStats> GetServerStatsAsync(string serverId)
    {
        var stats = new ServerStats();

        try
        {
            // Hostname
            stats.Hostname = (await ExecuteCommandAsync(serverId, "hostname")).Trim();

            // OS
            var os = await ExecuteCommandAsync(serverId, "cat /etc/os-release 2>/dev/null | grep PRETTY_NAME | cut -d= -f2 | tr -d '\"'");
            stats.Os = string.IsNullOrWhiteSpace(os) ? "Unknown" : os.Trim();

            // Kernel
            stats.Kernel = (await ExecuteCommandAsync(serverId, "uname -r")).Trim();

            // Uptime
            stats.Uptime = (await ExecuteCommandAsync(serverId, "uptime -p 2>/dev/null || uptime")).Trim();

            // IP
            stats.IpAddress = (await ExecuteCommandAsync(serverId, "hostname -I | awk '{print $1}'")).Trim();

            // CPU Cores
            var cores = await ExecuteCommandAsync(serverId, "nproc");
            if (int.TryParse(cores.Trim(), out var coreCount))
                stats.CpuCores = coreCount;

            // CPU Usage
            var cpuStr = await ExecuteCommandAsync(serverId, "top -bn1 | grep 'Cpu(s)' | awk '{print $2}'");
            if (double.TryParse(cpuStr.Trim().Replace(",", "."), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var cpu))
                stats.CpuPercent = cpu;

            // Memory
            var memStr = await ExecuteCommandAsync(serverId, "free -b | awk 'NR==2{print $3, $2}'");
            var memParts = memStr.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (memParts.Length >= 2)
            {
                if (long.TryParse(memParts[0], out var memUsed))
                    stats.MemoryUsed = memUsed;
                if (long.TryParse(memParts[1], out var memTotal))
                    stats.MemoryTotal = memTotal;
                if (stats.MemoryTotal > 0)
                    stats.MemoryPercent = (double)stats.MemoryUsed / stats.MemoryTotal * 100;
            }

            // Disk
            var diskStr = await ExecuteCommandAsync(serverId, "df -B1 / | awk 'NR==2{print $3, $2}'");
            var diskParts = diskStr.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (diskParts.Length >= 2)
            {
                if (long.TryParse(diskParts[0], out var diskUsed))
                    stats.DiskUsed = diskUsed;
                if (long.TryParse(diskParts[1], out var diskTotal))
                    stats.DiskTotal = diskTotal;
                if (stats.DiskTotal > 0)
                    stats.DiskPercent = (double)stats.DiskUsed / stats.DiskTotal * 100;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting stats: {ex.Message}");
        }

        return stats;
    }

    public async Task<(bool Success, string Message)> TestConnectionAsync(Server server)
    {
        try
        {
            var connectionInfo = CreateConnectionInfo(server);
            using var client = new SshClient(connectionInfo);

            await Task.Run(() => client.Connect());

            if (client.IsConnected)
            {
                var hostname = client.CreateCommand("hostname").Execute().Trim();
                client.Disconnect();
                return (true, $"Connected! Hostname: {hostname}");
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
        return (false, "Connection failed");
    }

    public ShellStream? StartShell(string serverId, Action<string> onDataReceived)
    {
        if (!_connections.TryGetValue(serverId, out var client) || !client.IsConnected)
            return null;

        try
        {
            StopShell(serverId);

            var shell = client.CreateShellStream("xterm-256color", 120, 30, 800, 600, 65536);
            _shells[serverId] = shell;

            shell.DataReceived += (sender, e) =>
            {
                var data = System.Text.Encoding.UTF8.GetString(e.Data);
                onDataReceived(data);
            };

            return shell;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error starting shell: {ex.Message}");
            return null;
        }
    }

    public void WriteToShell(string serverId, string data)
    {
        if (_shells.TryGetValue(serverId, out var shell))
        {
            shell.Write(data);
        }
    }

    public void StopShell(string serverId)
    {
        if (_shells.TryGetValue(serverId, out var shell))
        {
            try
            {
                shell.Close();
                shell.Dispose();
            }
            catch { }
            _shells.Remove(serverId);
        }
    }

    private static ConnectionInfo CreateConnectionInfo(Server server)
    {
        AuthenticationMethod authMethod;

        if (server.UsePrivateKey && !string.IsNullOrEmpty(server.PrivateKey))
        {
            var keyStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(server.PrivateKey));
            var keyFile = string.IsNullOrEmpty(server.Passphrase)
                ? new PrivateKeyFile(keyStream)
                : new PrivateKeyFile(keyStream, server.Passphrase);
            authMethod = new PrivateKeyAuthenticationMethod(server.Username, keyFile);
        }
        else
        {
            authMethod = new PasswordAuthenticationMethod(server.Username, server.Password);
        }

        return new ConnectionInfo(server.Host, server.Port, server.Username, authMethod)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public void Dispose()
    {
        foreach (var serverId in _shells.Keys.ToList())
            StopShell(serverId);

        foreach (var serverId in _sftpClients.Keys.ToList())
        {
            try
            {
                _sftpClients[serverId].Disconnect();
                _sftpClients[serverId].Dispose();
            }
            catch { }
        }
        _sftpClients.Clear();

        foreach (var serverId in _connections.Keys.ToList())
            Disconnect(serverId);
    }
}
