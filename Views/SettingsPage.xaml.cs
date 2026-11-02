using System.Windows;
using System.Windows.Controls;
using OASX.Services;

namespace OASX.Views;

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
        BackendBox.Text = OasSettings.BackendAddress;
        ThemeSelector.SelectedIndex = OasSettings.ThemeMode switch
        {
            "light" => 1,
            "dark" => 2,
            _ => 0,
        };
    }

    private void SaveBackend_Click(object sender, RoutedEventArgs e)
    {
        var addr = BackendBox.Text.Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(addr)) return;
        if (!addr.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !addr.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            addr = "http://" + addr;
        }
        OasSettings.BackendAddress = addr;
        BackendBox.Text = addr;
        OasApi.Reset();
    }

    private void ThemeSelector_SelectionChanged(object sender,
        SelectionChangedEventArgs e)
    {
        if (ThemeSelector.SelectedItem is ComboBoxItem { Tag: string mode })
        {
            OasSettings.ThemeMode = mode;
            App.ApplySavedTheme();
        }
    }
}
