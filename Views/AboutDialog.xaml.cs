using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace VPSManager.Views;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();

        // Set version from assembly
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"Version {version?.Major}.{version?.Minor}.{version?.Build}";

        // Set copyright year
        CopyrightText.Text = $"© {DateTime.Now.Year} Pandasdroid Pvt Ltd. All rights reserved.";
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
