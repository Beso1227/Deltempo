using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace Deltempo.App.WinUI.Views;

/// <summary>
/// WinUI 3 host for the update offer.
///
/// Presentation only: it renders the <see cref="ReleaseInfo"/> that
/// <see cref="UpdateService"/> returned, so the version, subtitle and changelog cannot
/// drift from what the release feed actually said. The download is handed to the
/// browser rather than performed here - see the note in the XAML.
/// </summary>
public sealed partial class UpdateOfferView : UserControl
{
    /// <summary>Fallback changelog, verbatim from the WPF modal.</summary>
    private const string FallbackChangelog =
        "• Performance optimizations & precision engine enhancements\n" +
        "• Direct in-place hot-swap update (zero installer leftovers)";

    private ReleaseInfo? _release;

    public UpdateOfferView()
    {
        InitializeComponent();
    }

    /// <summary>Raised when the offer is dismissed. Carries the release so the shell can
    /// remember that this exact version was dismissed, which is what WPF's close handler
    /// stored in DismissedVersion.</summary>
    public event Action<ReleaseInfo>? Dismissed;

    /// <summary>Fills and shows the offer from a checked release.</summary>
    public void Show(ReleaseInfo release, string currentVersionText)
    {
        ArgumentNullException.ThrowIfNull(release);

        _release = release;

        VersionText.Text = string.IsNullOrWhiteSpace(release.TagName)
            ? release.VersionString
            : release.TagName;

        // WPF picked the subtitle from whether a binary asset was attached. Here the
        // button always opens the release page, so the only distinction that still
        // matters is whether the feed says there is something to download.
        SubtitleText.Text = string.IsNullOrWhiteSpace(release.DownloadUrl)
            ? "A new official release of Deltempo is available on GitHub"
            : "A new official release of Deltempo is ready";

        ChangelogText.Text = string.IsNullOrWhiteSpace(release.Body)
            ? FallbackChangelog
            : release.Body;

        SubtitleText.Text += $" (this build: {currentVersionText})";

        StatusLine.Text = string.Empty;
        StatusLine.Visibility = Visibility.Collapsed;
    }

    /// <summary>The release this offer is showing, if any.</summary>
    public ReleaseInfo? Release => _release;

    private void OnDownloadClicked(object sender, RoutedEventArgs e)
    {
        if (_release is null)
        {
            return;
        }

        string tag = string.IsNullOrWhiteSpace(_release.TagName)
            ? $"v{_release.VersionString}"
            : _release.TagName;

        // The tag page, not the asset: the user chooses the download. Nothing is
        // fetched or written by this process.
        string url = $"https://github.com/Beso1227/Deltempo/releases/tag/{tag}";

        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });

            StatusLine.Text = $"Opened {url} in your browser.";
            StatusLine.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            StatusLine.Text = $"Could not open the release page: {ex.Message}";
            StatusLine.Visibility = Visibility.Visible;
        }
    }

    private void OnLaterClicked(object sender, RoutedEventArgs e)
    {
        if (_release is not null)
        {
            Dismissed?.Invoke(_release);
        }
    }
}
