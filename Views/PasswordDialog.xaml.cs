using System.Windows;

namespace VPSManager.Views;

public partial class PasswordDialog : Window
{
    public string Password => PasswordBox.Password;

    public PasswordDialog(string action, string description)
    {
        InitializeComponent();
        TitleText.Text = action;
        ActionText.Text = description;

        Loaded += (s, e) => PasswordBox.Focus();
    }

    public void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
        PasswordBox.Clear();
        PasswordBox.Focus();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PasswordBox.Password))
        {
            ShowError("Password is required");
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
