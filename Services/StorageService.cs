using System.IO;
using System.Text.Json;
using VPSManager.Models;

namespace VPSManager.Services;

public class StorageService
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public StorageService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appData, "VPSManager");
        Directory.CreateDirectory(appFolder);
        _filePath = Path.Combine(appFolder, "servers.json");
    }

    public List<Server> LoadServers()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<List<Server>>(json) ?? [];
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading servers: {ex.Message}");
        }
        return [];
    }

    public void SaveServers(List<Server> servers)
    {
        try
        {
            var json = JsonSerializer.Serialize(servers, _jsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving servers: {ex.Message}");
        }
    }

    public void AddServer(Server server)
    {
        var servers = LoadServers();
        servers.Add(server);
        SaveServers(servers);
    }

    public void UpdateServer(Server server)
    {
        var servers = LoadServers();
        var index = servers.FindIndex(s => s.Id == server.Id);
        if (index >= 0)
        {
            servers[index] = server;
            SaveServers(servers);
        }
    }

    public void DeleteServer(string serverId)
    {
        var servers = LoadServers();
        servers.RemoveAll(s => s.Id == serverId);
        SaveServers(servers);
    }
}
