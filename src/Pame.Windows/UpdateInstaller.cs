using System.Diagnostics;
using Pame.Core;

namespace Pame.Windows;

public static class UpdateInstaller
{
    public static bool IsInstalled(string directory) => File.Exists(Path.Combine(directory, "Pame.exe")) && File.Exists(Path.Combine(directory, "unins000.exe"));
    public static bool IsRunning
    {
        get { if (!Mutex.TryOpenExisting("Local\\Pame.Update", out var mutex)) return false; mutex.Dispose(); return true; }
    }
    public static async Task StartAsync(UpdateService service, PendingUpdate pending, string directory, CancellationToken cancel = default)
    {
        if (!IsInstalled(directory)) throw new InvalidOperationException("Install Pame with its Windows installer before applying in-app updates.");
        if (IsRunning) throw new InvalidOperationException("An update is already being installed.");
        var installer = service.InstallerPath(pending.Release);
        if (!pending.Sha256.Equals(pending.Release.Installer.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The update digest does not match its release.");
        if (!await UpdateService.VerifyFileAsync(installer, pending.Release.Installer.Size, pending.Sha256, cancel)) throw new InvalidDataException("The update file changed. Download it again.");
        Directory.CreateDirectory(service.Root);
        var runner = Path.Combine(service.Root, "install-update.ps1");
        using (var resource = typeof(UpdateInstaller).Assembly.GetManifestResourceStream("Pame.Windows.install-update.ps1")!)
        using (var reader = new StreamReader(resource)) await File.WriteAllTextAsync(runner, await reader.ReadToEndAsync(cancel), cancel);
        var token = Guid.NewGuid().ToString("N");
        var jobFile = Path.Combine(service.Root, "install-" + token + ".json");
        var readyFile = Path.Combine(service.Root, "ready-" + token);
        using var owner = Process.GetCurrentProcess();
        UpdateService.WriteAtomic(jobFile, new { Root = service.Root, Directory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar),
            Version = pending.Release.Version, Hash = pending.Sha256, Size = pending.Release.Installer.Size,
            Owner = owner.Id, OwnerStarted = owner.StartTime.ToUniversalTime().Ticks, ReadyFile = readyFile });
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe")) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", runner, "-JobFile", jobFile }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("Windows could not start the update installer.");
        // The helper owns the update mutex and validates its job before Pame exits.
        try
        {
            for (int i = 0; i < 200; i++)
            {
                if (File.Exists(readyFile)) { File.Delete(readyFile); return; }
                if (process.HasExited) break;
                await Task.Delay(50, cancel);
            }
            throw new IOException("The update helper did not become ready. Pame will stay open; try again later.");
        }
        catch { File.WriteAllText(jobFile + ".cancel", "cancel"); throw; }
    }
}
