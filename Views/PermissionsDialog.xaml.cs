using System.Windows;
using System.Windows.Controls;

namespace VPSManager.Views;

public partial class PermissionsDialog : Window
{
    private bool _updating;
    private bool _initialized;

    public short Permissions { get; private set; }

    public PermissionsDialog(string fileName, string currentPermissions)
    {
        InitializeComponent();

        FileNameText.Text = fileName;

        _updating = true; // Prevent events during initialization

        // Parse current permissions (e.g., "-rw-r--r--" or "644")
        if (!string.IsNullOrEmpty(currentPermissions) && currentPermissions.Length >= 9 && !char.IsDigit(currentPermissions[0]))
        {
            // Symbolic format
            var perms = currentPermissions.Length == 10 ? currentPermissions[1..] : currentPermissions;
            SetCheckboxesFromSymbolic(perms);
        }
        else if (!string.IsNullOrEmpty(currentPermissions) && int.TryParse(currentPermissions.TrimStart('-'), out var octal))
        {
            // Octal format
            OctalBox.Text = octal.ToString();
            SetCheckboxesFromOctal(octal);
        }
        else
        {
            // Default to 644
            OctalBox.Text = "644";
            SetCheckboxesFromOctal(644);
        }

        _updating = false;
        _initialized = true;
        UpdateSymbolic();

        Loaded += (s, e) => OctalBox.Focus();
    }

    private void SetCheckboxesFromSymbolic(string perms)
    {
        if (perms.Length < 9) return;

        _updating = true;

        // Owner
        OwnerRead.IsChecked = perms[0] == 'r';
        OwnerWrite.IsChecked = perms[1] == 'w';
        OwnerExecute.IsChecked = perms[2] == 'x' || perms[2] == 's';

        // Group
        GroupRead.IsChecked = perms[3] == 'r';
        GroupWrite.IsChecked = perms[4] == 'w';
        GroupExecute.IsChecked = perms[5] == 'x' || perms[5] == 's';

        // Others
        OthersRead.IsChecked = perms[6] == 'r';
        OthersWrite.IsChecked = perms[7] == 'w';
        OthersExecute.IsChecked = perms[8] == 'x' || perms[8] == 't';

        _updating = false;

        UpdateOctalFromCheckboxes();
    }

    private void SetCheckboxesFromOctal(int octal)
    {
        _updating = true;

        // Parse octal (e.g., 755 -> owner=7, group=5, others=5)
        var owner = (octal / 100) % 10;
        var group = (octal / 10) % 10;
        var others = octal % 10;

        OwnerRead.IsChecked = (owner & 4) != 0;
        OwnerWrite.IsChecked = (owner & 2) != 0;
        OwnerExecute.IsChecked = (owner & 1) != 0;

        GroupRead.IsChecked = (group & 4) != 0;
        GroupWrite.IsChecked = (group & 2) != 0;
        GroupExecute.IsChecked = (group & 1) != 0;

        OthersRead.IsChecked = (others & 4) != 0;
        OthersWrite.IsChecked = (others & 2) != 0;
        OthersExecute.IsChecked = (others & 1) != 0;

        _updating = false;

        UpdateSymbolic();
    }

    private void OctalBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updating || !_initialized) return;

        if (int.TryParse(OctalBox.Text, out var octal) && octal >= 0 && octal <= 777)
        {
            SetCheckboxesFromOctal(octal);
        }
    }

    private void Permission_Changed(object sender, RoutedEventArgs e)
    {
        if (_updating || !_initialized) return;
        UpdateOctalFromCheckboxes();
    }

    private void UpdateOctalFromCheckboxes()
    {
        var owner = (OwnerRead.IsChecked == true ? 4 : 0) +
                    (OwnerWrite.IsChecked == true ? 2 : 0) +
                    (OwnerExecute.IsChecked == true ? 1 : 0);

        var group = (GroupRead.IsChecked == true ? 4 : 0) +
                    (GroupWrite.IsChecked == true ? 2 : 0) +
                    (GroupExecute.IsChecked == true ? 1 : 0);

        var others = (OthersRead.IsChecked == true ? 4 : 0) +
                     (OthersWrite.IsChecked == true ? 2 : 0) +
                     (OthersExecute.IsChecked == true ? 1 : 0);

        _updating = true;
        OctalBox.Text = $"{owner}{group}{others}";
        _updating = false;

        UpdateSymbolic();
    }

    private void UpdateSymbolic()
    {
        if (SymbolicText == null || OwnerRead == null) return;

        var symbolic = "-" +
            (OwnerRead.IsChecked == true ? "r" : "-") +
            (OwnerWrite.IsChecked == true ? "w" : "-") +
            (OwnerExecute.IsChecked == true ? "x" : "-") +
            (GroupRead.IsChecked == true ? "r" : "-") +
            (GroupWrite.IsChecked == true ? "w" : "-") +
            (GroupExecute.IsChecked == true ? "x" : "-") +
            (OthersRead.IsChecked == true ? "r" : "-") +
            (OthersWrite.IsChecked == true ? "w" : "-") +
            (OthersExecute.IsChecked == true ? "x" : "-");

        SymbolicText.Text = symbolic;
    }

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string preset)
        {
            OctalBox.Text = preset;
            if (int.TryParse(preset, out var octal))
            {
                SetCheckboxesFromOctal(octal);
            }
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(OctalBox.Text, out var octal) && octal >= 0 && octal <= 777)
        {
            // Convert decimal representation of octal to actual octal value
            // e.g., 755 decimal -> 493 (which is 0755 in octal)
            var owner = (octal / 100) % 10;
            var group = (octal / 10) % 10;
            var others = octal % 10;

            Permissions = (short)((owner << 6) | (group << 3) | others);
            DialogResult = true;
        }
        else
        {
            MessageBox.Show("Invalid permission value", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
