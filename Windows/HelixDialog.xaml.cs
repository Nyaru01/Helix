using System.Windows;
using Wpf.Ui.Controls;

namespace LocalBlast.Windows;

public partial class HelixDialog : FluentWindow
{
    private HelixDialog(string title, string message, string primaryText, string? secondaryText)
    {
        InitializeComponent();
        Title = title;
        DialogTitleBar.Title = title;
        HeadingText.Text = title;
        MessageText.Text = message;
        PrimaryButton.Content = primaryText;
        SecondaryButton.Content = secondaryText;
        SecondaryButton.Visibility = secondaryText is null ? Visibility.Collapsed : Visibility.Visible;
        PrimaryButton.IsCancel = secondaryText is null;
    }

    public static Task<bool> ShowAsync(Window owner, string title, string message,
        string primaryText = "OK", string? secondaryText = null)
    {
        var dialog = new HelixDialog(title, message, primaryText, secondaryText)
        {
            Owner = owner
        };
        return Task.FromResult(dialog.ShowDialog() == true);
    }

    private void PrimaryButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void SecondaryButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
