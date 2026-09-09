using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace WinTempCleaner;

// Update discovery, release notes modal and in-place update application.
public partial class MainWindow
{

    private async Task CheckForUpdatesInternalAsync(bool silent)
    {
        try
        {
            bool includePrereleases = string.Equals(SettingsService.Current.UpdateChannel, "PreRelease", StringComparison.OrdinalIgnoreCase);
            var release = await UpdateService.CheckForUpdatesAsync(includePrereleases: includePrereleases);

            SettingsService.Update(s => s.LastUpdateCheckTimestamp = DateTime.Now.ToString("g"));

            Dispatcher.Invoke(() =>
            {
                SettingsLastCheckedText.Text = $"Last checked: {SettingsService.Current.LastUpdateCheckTimestamp}";
            });

            if (release != null && release.IsNewer && !string.IsNullOrEmpty(release.DownloadUrl))
            {
                // Suppress repeated prompt on startup if user previously dismissed this exact release version
                if (silent && !string.IsNullOrEmpty(release.VersionString) &&
                    release.VersionString.Equals(SettingsService.Current.DismissedVersion, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _pendingRelease = release;
                Dispatcher.Invoke(() =>
                {
                    ManualCheckStatusText.Text = $"New release ready: {release.TagName}";
                    ManualCheckStatusText.Foreground = (Brush)FindResource("ElectricCyanBrush");

                    UpdateVersionTagText.Text = release.TagName;
                    UpdateSubtitleText.Text = "A new official release of Deltempo is ready";

                    if (!string.IsNullOrWhiteSpace(release.Body))
                    {
                        UpdateChangelogText.Text = release.Body;
                    }
                    else
                    {
                        UpdateChangelogText.Text = "• Performance optimizations & precision engine enhancements\n• Direct in-place hot-swap update (zero installer leftovers)";
                    }
                    UpdateProgressContainer.Visibility = Visibility.Collapsed;
                    ApplyUpdateBtn.IsEnabled = true;
                    ApplyUpdateBtn.Content = "Update Now";
                    UpdateLaterBtn.IsEnabled = true;
                    UpdateModalOverlay.Visibility = Visibility.Visible;
                    SoundService.PlayClickSound();
                    AddLog($"New version available: {release.TagName}", LogLevel.Info);
                });
            }
            else if (!silent)
            {
                Dispatcher.Invoke(() =>
                {
                    ManualCheckStatusText.Text = $"Official Release (Up to date)";
                    ManualCheckStatusText.Foreground = (Brush)FindResource("EmeraldGreenBrush");
                    MessageBox.Show(
                        $"You are running the latest build of Deltempo ({BuildInfo.VersionWithPatchDisplay}).\n\nChannel: {(includePrereleases ? "Beta / Pre-Release" : "Stable Official")}\nNo newer updates are currently available.",
                        "Deltempo is Up to Date",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                });
            }
        }
        catch (Exception ex)
        {
            if (!silent)
            {
                Dispatcher.Invoke(() =>
                {
                    ManualCheckStatusText.Text = "Update check failed";
                    ManualCheckStatusText.Foreground = (Brush)FindResource("RoseErrorBrush");
                    MessageBox.Show(
                        $"Unable to check for updates: {ex.Message}",
                        "Update Check Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                });
            }
        }
    }

    private async void ManualCheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        ManualCheckStatusText.Text = "Checking GitHub Releases...";
        ManualCheckStatusText.Foreground = (Brush)FindResource("TextMediumBrush");
        ManualCheckBtnText.Text = "Checking...";
        ManualCheckUpdateBtn.IsEnabled = false;
        try
        {
            await CheckForUpdatesInternalAsync(silent: false);
        }
        finally
        {
            ManualCheckBtnText.Text = "Check for Updates";
            ManualCheckUpdateBtn.IsEnabled = true;
        }
    }

    private async void ApplyUpdateBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingRelease == null || string.IsNullOrEmpty(_pendingRelease.DownloadUrl))
            return;

        ApplyUpdateBtn.IsEnabled = false;
        UpdateLaterBtn.IsEnabled = false;
        ApplyUpdateBtn.Content = "Updating...";
        UpdateProgressContainer.Visibility = Visibility.Visible;
        UpdateProgressBar.Value = 0;
        UpdatePercentText.Text = "0%";
        UpdateDownloadStatusText.Text = "Streaming update...";

        var progress = new Progress<double>(val =>
        {
            Dispatcher.Invoke(() =>
            {
                UpdateProgressBar.Value = val;
                UpdatePercentText.Text = $"{val:F0}%";
                if (_pendingRelease != null && _pendingRelease.FileSizeBytes > 0)
                {
                    double currentMb = (val / 100.0 * _pendingRelease.FileSizeBytes) / (1024.0 * 1024.0);
                    double totalMb = _pendingRelease.FileSizeBytes / (1024.0 * 1024.0);
                    UpdateDownloadStatusText.Text = $"Downloading update ({currentMb:F1} MB / {totalMb:F1} MB)...";
                }
                else
                {
                    UpdateDownloadStatusText.Text = $"Downloading update ({val:F0}%)...";
                }
            });
        });

        try
        {
            AddLog($"Starting atomic in-place update to {_pendingRelease.TagName}...", LogLevel.Info);
            await UpdateService.DownloadAndApplyUpdateAsync(
                _pendingRelease.DownloadUrl,
                progress);
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateProgressContainer.Visibility = Visibility.Collapsed;
                ApplyUpdateBtn.IsEnabled = true;
                UpdateLaterBtn.IsEnabled = true;
                ApplyUpdateBtn.Content = "Retry Update";
                MessageBox.Show(
                    $"Update failed: {ex.Message}\n\nYou can manually download the latest version from GitHub Releases.",
                    "Update Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                AddLog($"Update failed: {ex.Message}", LogLevel.Error);
            });
        }
    }

    private void CloseUpdateModal_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingRelease != null && !string.IsNullOrEmpty(_pendingRelease.VersionString))
        {
            SettingsService.Update(s => s.DismissedVersion = _pendingRelease.VersionString);
        }
        UpdateModalOverlay.Visibility = Visibility.Collapsed;
        SoundService.PlayClickSound();
    }

    // ==========================================
    // ELITE PC PERFORMANCE TOOLKIT HANDLERS
    // ==========================================
}
