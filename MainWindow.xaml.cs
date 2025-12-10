using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using VPSManager.Models;
using VPSManager.Services;
using VPSManager.ViewModels;
using VPSManager.Views;

namespace VPSManager;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += MainWindow_Closing;

        // Wire up the terminal
        Loaded += (s, e) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.TerminalOutput += Terminal.WriteData;
                vm.TerminalClear += Terminal.Clear;
                Terminal.InputReceived += input => vm.SendToShell(input);
            }
        };
    }

    #region File Menu

    private void ImportServers_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            Title = "Import Servers"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = File.ReadAllText(dialog.FileName);
                var servers = JsonSerializer.Deserialize<List<Server>>(json);

                if (servers != null && DataContext is MainViewModel vm)
                {
                    foreach (var server in servers)
                    {
                        vm.Servers.Add(server);
                    }
                    vm.SaveServers();
                    MessageBox.Show($"Successfully imported {servers.Count} server(s).", "Import Complete",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import servers:\n{ex.Message}", "Import Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ExportServers_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.Servers.Count == 0)
        {
            MessageBox.Show("No servers to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json",
            Title = "Export Servers",
            FileName = "clouddeck-servers.json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = JsonSerializer.Serialize(vm.Servers.ToList(), new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dialog.FileName, json);
                MessageBox.Show($"Successfully exported {vm.Servers.Count} server(s).", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export servers:\n{ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region Tools Menu

    private void ClearTerminal_Click(object sender, RoutedEventArgs e)
    {
        Terminal.Clear();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Settings will be available in a future update.", "Settings",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    #endregion

    #region Help Menu

    private void Documentation_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/pandasdroid/clouddeck",
            UseShellExecute = true
        });
    }

    private void Shortcuts_Click(object sender, RoutedEventArgs e)
    {
        var shortcuts = @"Keyboard Shortcuts

Navigation:
  Ctrl+N     Add new server
  F5         Refresh files

Terminal:
  Ctrl+C     Copy selection / Cancel command
  Ctrl+V     Paste
  Ctrl+L     Clear terminal

File Manager:
  Enter      Open file/folder
  Delete     Delete selected file
  F2         Rename file

General:
  Alt+F4     Exit application";

        MessageBox.Show(shortcuts, "Keyboard Shortcuts", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("You are running the latest version (1.0.0).", "Check for Updates",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var aboutDialog = new AboutDialog { Owner = this };
        aboutDialog.ShowDialog();
    }

    #endregion

    #region Other Events

    private void FileItem_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem item && item.DataContext is FileItem file)
        {
            if (DataContext is MainViewModel vm)
            {
                if (file.IsDirectory || file.Name == "..")
                {
                    vm.NavigateToCommand.Execute(file);
                }
                else if (SshService.IsEditable(file.Name))
                {
                    vm.EditFileCommand.Execute(null);
                }
            }
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.TerminalOutput -= Terminal.WriteData;
            vm.TerminalClear -= Terminal.Clear;
            vm.Cleanup();
        }
    }

    #endregion
}
