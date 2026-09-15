using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Pame.Core;
using Pame.Windows;

namespace Pame.App;

public partial class MainWindow
{
    static readonly Version CurrentVersion = new(typeof(App).Assembly.GetName().Version!.ToString(3));
    UpdateService? updater;
    PameRelease? availableUpdate;
    PendingUpdate? pendingUpdate;
    CancellationTokenSource? updateDownload;
    bool updateBusy, installingUpdate;
    double updatePercent;
    string updateStatus = "Ready to check GitHub Releases.";
    bool CanApplyUpdate => !closing && !cleanupBusy && !preparingGame && sessions.ActiveGame is null;
    DateTimeOffset nextAutomaticCheck;

    async Task InitializeUpdates()
    {
        if (smoke || safeMode) return;
        updater = new(dataRoot);
        try
        {
            pendingUpdate = await updater.ReadPendingAsync(CurrentVersion, settings.PreviewUpdates, lifetime.Token);
            availableUpdate = pendingUpdate?.Release;
            if (pendingUpdate is not null) updateStatus = $"Pame {pendingUpdate.Release.Version} is ready to install.";
            var resultPath = Path.Combine(updater.Root, "result.json");
            if (File.Exists(resultPath))
            {
                using var result = JsonDocument.Parse(File.ReadAllText(resultPath));
                updateStatus = result.RootElement.GetProperty("Message").GetString() ?? updateStatus;
                Toast(updateStatus); File.Delete(resultPath);
            }
        }
        catch (OperationCanceledException) { return; }
        catch (Exception error) { Log.Error("update.initialize", error); updateStatus = "Open Updates to check again."; }
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), lifetime.Token);
            while (!closing)
            {
                if (settings.AutomaticUpdates && CanApplyUpdate && !updateBusy) await CheckUpdates(true);
                await Task.Delay(TimeSpan.FromMinutes(30), lifetime.Token);
            }
        }
        catch (OperationCanceledException) { }
    }
    async Task CheckUpdates(bool automatic = false)
    {
        if (updateBusy || closing || automatic && DateTimeOffset.UtcNow < nextAutomaticCheck) return;
        updater ??= new(dataRoot);
        if (!CanApplyUpdate) { if (!automatic) Toast("Finish your game or the current operation before checking for updates."); return; }
        updateBusy = true; updateStatus = "Checking GitHub Releases…"; updatePercent = 0;
        bool checkedSuccessfully = false;
        try
        {
            var release = await updater.CheckAsync(CurrentVersion, settings.PreviewUpdates, lifetime.Token);
            nextAutomaticCheck = DateTimeOffset.UtcNow.AddHours(6);
            availableUpdate = release;
            if (release is null)
            {
                pendingUpdate = null;
                if (File.Exists(updater.PendingFile)) File.Delete(updater.PendingFile);
                updateStatus = $"Pame {CurrentVersion} is up to date for your selected channel.";
                return;
            }
            pendingUpdate = await updater.ReadPendingAsync(CurrentVersion, settings.PreviewUpdates, lifetime.Token);
            if (pendingUpdate?.Release != release)
            {
                pendingUpdate = null;
                if (File.Exists(updater.PendingFile)) File.Delete(updater.PendingFile);
            }
            updateStatus = pendingUpdate is null ? $"Pame {release.Version} is available · {release.Installer.Size / 1048576d:0} MB." : $"Pame {release.Version} is ready to install.";
            checkedSuccessfully = true;
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { return; }
        catch (Exception error) { nextAutomaticCheck = DateTimeOffset.UtcNow.AddMinutes(30); updateStatus = "Could not check for updates. " + FriendlyUpdateError(error); Log.Error("update.check", error); }
        finally { updateBusy = false; }
        if (checkedSuccessfully && availableUpdate is not null && pendingUpdate is null && settings.AutomaticUpdates && CanApplyUpdate) await DownloadUpdate();
    }
    async Task DownloadUpdate()
    {
        if (updateBusy || availableUpdate is null || closing) return;
        if (!CanApplyUpdate) { Toast("The update will wait until you finish playing."); return; }
        updater ??= new(dataRoot);
        updateBusy = true; updatePercent = 0; updateStatus = $"Downloading Pame {availableUpdate.Version}…";
        updateDownload = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        try
        {
            var progress = new Progress<double>(value => updatePercent = value);
            pendingUpdate = await updater.DownloadAsync(availableUpdate, progress, updateDownload.Token);
            updateStatus = $"Pame {pendingUpdate.Release.Version} is ready. Restart & update now, or install next time Pame opens with automatic updates on.";
            if (!closing && CanApplyUpdate) Toast($"Pame {pendingUpdate.Release.Version} is ready · Settings → Updates.");
        }
        catch (OperationCanceledException) { updateStatus = "Download cancelled. It can be downloaded again when you're ready."; nextAutomaticCheck = DateTimeOffset.UtcNow.AddMinutes(30); }
        catch (Exception error) { updateStatus = "Download failed. " + FriendlyUpdateError(error); Log.Error("update.download", error); nextAutomaticCheck = DateTimeOffset.UtcNow.AddMinutes(30); }
        finally { updateBusy = false; updateDownload.Dispose(); updateDownload = null; }
    }
    static string FriendlyUpdateError(Exception error) => error is OperationCanceledException ? "The connection timed out. Try again later." : error.Message;
    async Task InstallUpdate()
    {
        if (installingUpdate || updateBusy || pendingUpdate is null || updater is null) return;
        if (!CanApplyUpdate) { Toast("Finish your game or the current operation before installing the update."); return; }
        installingUpdate = true; updateBusy = true; updateStatus = "Preparing the update. Pame will close and reopen automatically…";
        try { await UpdateInstaller.StartAsync(updater, pendingUpdate, AppContext.BaseDirectory, lifetime.Token); Close(); }
        catch (Exception error) { installingUpdate = false; updateBusy = false; updateStatus = "Could not start the update. " + FriendlyUpdateError(error); Log.Error("update.install", error); }
    }
    void ShowUpdates()
    {
        modalButtons.Clear(); var body = new StackPanel();
        var status = Text(updateStatus, 19, Colors.White); status.TextWrapping = TextWrapping.Wrap; status.Margin = new(0, 0, 0, 14); body.Children.Add(status);
        var progress = new ProgressBar { Height = 5, Maximum = 100, Foreground = Brush("#79CFFF"), Background = Brush("#263B4E"), Margin = new(0, 0, 0, 18) }; body.Children.Add(progress);
        Button Add(string label, string id, Action action)
        {
            var button = Button(label, "update:" + id, action, true); button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.MinHeight = 59; button.Margin = new(0, 0, 0, 11); body.Children.Add(button); return button;
        }
        var install = Add("Restart & update", "install", () => _ = InstallUpdate());
        var check = Add("Check for updates", "check", () => _ = CheckUpdates());
        var download = Add("Download update", "download", () => _ = DownloadUpdate());
        var cancel = Add("Cancel download", "cancel", () => updateDownload?.Cancel());
        var automatic = Add("", "automatic", () => { settings.AutomaticUpdates = !settings.AutomaticUpdates; if (!settings.AutomaticUpdates) updateDownload?.Cancel(); SaveSettings(); });
        var preview = Add("", "preview", () => { settings.PreviewUpdates = !settings.PreviewUpdates; availableUpdate = null; pendingUpdate = null; if (updater is not null && File.Exists(updater.PendingFile)) File.Delete(updater.PendingFile); nextAutomaticCheck = default; SaveSettings(); updateStatus = "Channel changed. Check for updates to refresh."; });
        Add("What's new on GitHub", "notes", () => OpenProjectPage(availableUpdate?.PageUrl ?? ProjectUrl + "/releases"));
        Add("Back", "back", HideModal);
        ShowDialog("Pame updates", body, $"Installed: {CurrentVersion} · GitHub Releases\nAutomatic updates download in the background and install next time Pame opens. Games are never interrupted.", 860);
        liveModalUpdate = () =>
        {
            status.Text = updateStatus; progress.Value = updatePercent; progress.IsIndeterminate = updateBusy && updateDownload is null;
            progress.Visibility = updateBusy ? Visibility.Visible : Visibility.Collapsed;
            install.IsEnabled = pendingUpdate is not null && !updateBusy && CanApplyUpdate && UpdateInstaller.IsInstalled(AppContext.BaseDirectory);
            check.IsEnabled = !updateBusy && CanApplyUpdate;
            download.Visibility = availableUpdate is not null && pendingUpdate is null ? Visibility.Visible : Visibility.Collapsed;
            download.IsEnabled = !updateBusy && CanApplyUpdate;
            cancel.Visibility = updateDownload is not null ? Visibility.Visible : Visibility.Collapsed;
            automatic.Content = "Automatic updates: " + (settings.AutomaticUpdates ? "On" : "Off"); automatic.IsEnabled = !installingUpdate;
            preview.Content = "Include public alpha releases: " + (settings.PreviewUpdates ? "On" : "Off"); preview.IsEnabled = !updateBusy;
        };
        liveModalUpdate();
    }
}
