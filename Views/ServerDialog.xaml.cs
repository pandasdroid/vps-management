using System.Windows;
using System.Windows.Media;
using VPSManager.Models;
using VPSManager.Services;

namespace VPSManager.Views;

public partial class ServerDialog : Window
{
    public Server? Server { get; private set; }
    private readonly Server? _existingServer;
    private readonly SshService _ssh = new();

    public ServerDialog(Server? server = null)
    {
        InitializeComponent();
        _existingServer = server;

        if (server != null)
        {
            HeaderText.Text = "Edit Server";
            DeleteButton.Visibility = Visibility.Visible;

            NameBox.Text = server.Name;
            HostBox.Text = server.Host;
            PortBox.Text = server.Port.ToString();
            UsernameBox.Text = server.Username;

            if (server.UsePrivateKey)
            {
                KeyAuth.IsChecked = true;
                KeyBox.Text = server.PrivateKey;
            }
            else
            {
                PasswordAuth.IsChecked = true;
                PasswordBox.Password = server.Password;
            }
        }

        Owner = Application.Current.MainWindow;
    }

    private void AuthType_Changed(object sender, RoutedEventArgs e)
    {
        // Check if UI elements are initialized
        if (PasswordPanel == null || KeyPanel == null) return;

        if (KeyAuth.IsChecked == true)
        {
            PasswordPanel.Visibility = Visibility.Collapsed;
            KeyPanel.Visibility = Visibility.Visible;
        }
        else
        {
            PasswordPanel.Visibility = Visibility.Visible;
            KeyPanel.Visibility = Visibility.Collapsed;
        }
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateForm()) return;

        TestResultBorder.Visibility = Visibility.Visible;
        TestResultBorder.Background = new SolidColorBrush(Color.FromArgb(32, 234, 179, 8));
        TestResultText.Foreground = new SolidColorBrush(Color.FromRgb(234, 179, 8));
        TestResultText.Text = "Testing connection...";

        var server = CreateServerFromForm();
        var (success, message) = await _ssh.TestConnectionAsync(server);

        if (success)
        {
            TestResultBorder.Background = new SolidColorBrush(Color.FromArgb(32, 34, 197, 94));
            TestResultText.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
        }
        else
        {
            TestResultBorder.Background = new SolidColorBrush(Color.FromArgb(32, 239, 68, 68));
            TestResultText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }
        TestResultText.Text = message;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateForm()) return;

        Server = CreateServerFromForm();

        if (_existingServer != null)
        {
            Server.Id = _existingServer.Id;
            Server.CreatedAt = _existingServer.CreatedAt;
        }

        DialogResult = true;
        Close();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to delete this server?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes && _existingServer != null)
        {
            var storage = new StorageService();
            storage.DeleteServer(_existingServer.Id);
            DialogResult = false;
            Close();
        }
    }

    private bool ValidateForm()
    {
        if (string.IsNullOrWhiteSpace(HostBox.Text))
        {
            MessageBox.Show("Please enter a host or IP address.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            HostBox.Focus();
            return false;
        }

        if (string.IsNullOrWhiteSpace(UsernameBox.Text))
        {
            MessageBox.Show("Please enter a username.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            UsernameBox.Focus();
            return false;
        }

        if (PasswordAuth.IsChecked == true && string.IsNullOrEmpty(PasswordBox.Password))
        {
            MessageBox.Show("Please enter a password.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            PasswordBox.Focus();
            return false;
        }

        if (KeyAuth.IsChecked == true && string.IsNullOrWhiteSpace(KeyBox.Text))
        {
            MessageBox.Show("Please enter a private key.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            KeyBox.Focus();
            return false;
        }

        return true;
    }

    private Server CreateServerFromForm()
    {
        return new Server
        {
            Name = NameBox.Text.Trim(),
            Host = HostBox.Text.Trim(),
            Port = int.TryParse(PortBox.Text, out var port) ? port : 22,
            Username = UsernameBox.Text.Trim(),
            UsePrivateKey = KeyAuth.IsChecked == true,
            Password = PasswordAuth.IsChecked == true ? PasswordBox.Password : string.Empty,
            PrivateKey = KeyAuth.IsChecked == true ? KeyBox.Text : string.Empty,
            Passphrase = KeyAuth.IsChecked == true ? PassphraseBox.Password : string.Empty
        };
    }
}
