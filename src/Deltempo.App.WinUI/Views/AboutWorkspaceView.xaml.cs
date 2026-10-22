using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Deltempo.App.WinUI.Views;

/// <summary>
/// Workspace panel for the about screen. The version line is read from the running
/// assembly rather than from a resource, so it can never disagree with what
/// actually shipped.
/// </summary>
public sealed partial class AboutWorkspaceView : UserControl
{
    private const string RepositoryUrl = "https://github.com/Beso1227/Deltempo";
    private const string DocumentationUrl = "https://beso1227.github.io/Deltempo/";
    private const string LicenseUrl = "https://github.com/Beso1227/Deltempo/blob/main/LICENSE";

    public AboutWorkspaceView()
    {
        InitializeComponent();

        VersionText.Text = BuildVersionLine();
    }

    private static string BuildVersionLine()
    {
        Assembly assembly = typeof(AboutWorkspaceView).Assembly;
        string version = assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        string informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? version;

        return $"{version} · {informational}";
    }

    private void OnOpenRepositoryClick(object sender, RoutedEventArgs e)
        => Open(RepositoryUrl);

    private void OnOpenDocumentationClick(object sender, RoutedEventArgs e)
        => Open(DocumentationUrl);

    private void OnOpenLicenseClick(object sender, RoutedEventArgs e)
        => Open(LicenseUrl);

    private static void Open(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // A machine with no browser registered is not worth crashing over: the
            // URLs are on screen and the user can read them.
        }
    }
}
