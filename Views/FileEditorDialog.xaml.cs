using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace VPSManager.Views;

public partial class FileEditorDialog : Window
{
    private readonly string _filePath;
    private readonly Func<string, Task> _saveAction;
    private bool _hasChanges;
    private string _originalContent = "";

    public FileEditorDialog(string filePath, string content, Func<string, Task> saveAction)
    {
        InitializeComponent();

        _filePath = filePath;
        _saveAction = saveAction;
        _originalContent = content;

        FileNameText.Text = Path.GetFileName(filePath);
        FilePathText.Text = filePath;
        EditorBox.Text = content;
        _hasChanges = false;

        EditorBox.SelectionChanged += EditorBox_SelectionChanged;
        UpdateLineCol();

        Loaded += (s, e) => EditorBox.Focus();
    }

    private void EditorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _hasChanges = EditorBox.Text != _originalContent;
        UpdateTitle();
    }

    private void EditorBox_SelectionChanged(object sender, RoutedEventArgs e)
    {
        UpdateLineCol();
    }

    private void UpdateLineCol()
    {
        var caretIndex = EditorBox.CaretIndex;
        var text = EditorBox.Text;

        var line = 1;
        var col = 1;
        for (int i = 0; i < caretIndex && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                col = 1;
            }
            else
            {
                col++;
            }
        }

        LineColText.Text = $"Ln {line}, Col {col}";
    }

    private void UpdateTitle()
    {
        Title = _hasChanges ? $"File Editor - {Path.GetFileName(_filePath)} *" : $"File Editor - {Path.GetFileName(_filePath)}";
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveButton.IsEnabled = false;
            StatusText.Text = "Saving...";
            StatusText.Foreground = System.Windows.Media.Brushes.Orange;

            await _saveAction(EditorBox.Text);

            _originalContent = EditorBox.Text;
            _hasChanges = false;
            UpdateTitle();

            StatusText.Text = "Saved";
            StatusText.Foreground = System.Windows.Media.Brushes.LimeGreen;

            await Task.Delay(2000);
            StatusText.Text = "";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Save failed";
            StatusText.Foreground = System.Windows.Media.Brushes.Red;
            MessageBox.Show($"Failed to save file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (_hasChanges)
        {
            var result = MessageBox.Show(
                "You have unsaved changes. Do you want to save before closing?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                Save_Click(sender, e);
            }
            else if (result == MessageBoxResult.Cancel)
            {
                return;
            }
        }

        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_hasChanges)
        {
            var result = MessageBox.Show(
                "You have unsaved changes. Are you sure you want to close?",
                "Unsaved Changes",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }
        }

        base.OnClosing(e);
    }
}
