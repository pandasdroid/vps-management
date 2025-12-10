using System.Windows;

namespace VPSManager.Views;

public partial class InputDialog : Window
{
    public string InputValue => InputBox.Text;

    public InputDialog(string title, string label, string defaultValue = "")
    {
        InitializeComponent();
        TitleText.Text = title;
        Title = title;
        LabelText.Text = label;
        InputBox.Text = defaultValue;

        Loaded += (s, e) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
