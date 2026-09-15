using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Pame.Core;
using Pame.Windows;

namespace Pame.App;
public partial class MainWindow
{
    async Task SmokeUpdates()
    {
        inputTimer.Stop(); settings.ConsoleMode = false; await consoleShell.LeaveAsync();
        var report = new Dictionary<string, object>();
        try
        {
            Navigate("Settings"); pageButtons.Single(b => b.Tag?.ToString() == "setting:updates").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            SaveVisual(design, Path.Combine(dataRoot, "updates.png"), 1920, 1080);
            report["firstFocusIsCheck"] = modalButtons.Single(b => b.Tag?.ToString() == "update:check").IsKeyboardFocused;
            report["installDisabledWithoutPackage"] = !modalButtons.Single(b => b.Tag?.ToString() == "update:install").IsEnabled;
            var original = settings.AutomaticUpdates;
            modalButtons.Single(b => b.Tag?.ToString() == "update:automatic").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            report["automaticChoiceSaved"] = db.Get("shell", new ShellSettings()).AutomaticUpdates != original;
            settings.AutomaticUpdates = original; SaveSettings();
            preparingGame = true; liveModalUpdate?.Invoke();
            report["gameBlocksCheck"] = !modalButtons.Single(b => b.Tag?.ToString() == "update:check").IsEnabled;
            preparingGame = false;
            updateStatus = "Update verification failed. Download the update again."; liveModalUpdate?.Invoke();
            var last = modalButtons.Last(); last.Focus(); last.BringIntoView(); await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            var pos = last.TransformToAncestor(design).Transform(new Point(0, 0));
            report["backVisible"] = pos.Y >= 0 && pos.Y + last.ActualHeight <= design.ActualHeight;
            SaveVisual(design, Path.Combine(dataRoot, "updates-bottom.png"), 1920, 1080);
            using var updates = new UpdateService(dataRoot);
            var release = await updates.CheckAsync(new Version(0, 0, 0), true);
            report["publicGitHubReleaseFound"] = release is not null;
            if (release is not null) report["publicVersion"] = release.Version;
            report["passed"] = report.Values.OfType<bool>().All(value => value);
        }
        catch (Exception error) { report["passed"] = false; report["error"] = error.ToString(); }
        finally
        {
            preparingGame = false;
            await File.WriteAllTextAsync(Path.Combine(dataRoot, "updates-report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Close();
        }
    }
}
