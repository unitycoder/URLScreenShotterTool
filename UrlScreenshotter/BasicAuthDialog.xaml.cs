using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace UrlScreenshotter;

public partial class BasicAuthDialog : Window
{
    public string UserName => UsernameText.Text;
    public string Password => PasswordInput.Password;

    public BasicAuthDialog(string origin, string? previousUserName = null)
    {
        InitializeComponent();
        OriginText.Text = origin;
        UsernameText.Text = previousUserName ?? string.Empty;
        Loaded += (_, _) => UsernameText.Focus();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UserName))
        {
            MessageBox.Show(this, "Enter a username.", "Authentication", MessageBoxButton.OK, MessageBoxImage.Information);
            UsernameText.Focus();
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
