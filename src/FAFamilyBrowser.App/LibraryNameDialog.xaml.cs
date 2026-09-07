namespace FAFamilyBrowser.App;

using System.Windows;

public partial class LibraryNameDialog : Window
{
    public LibraryNameDialog(string? initialValue = null, string actionLabel = "Save")
    {
        Prompt = "Library name";
        ActionLabel = actionLabel;
        InitializeComponent();
        DataContext = this;
        Loaded += (_, _) => { ValueTextBox.Text = initialValue ?? string.Empty; ValueTextBox.SelectAll(); ValueTextBox.Focus(); };
    }

    public string Prompt { get; }
    public string ActionLabel { get; }
    public string Value => ValueTextBox.Text.Trim();

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(Value)) DialogResult = true;
    }
}
