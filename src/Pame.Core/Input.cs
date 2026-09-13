namespace Pame.Core;

public enum ShellInput { None, Up, Down, Left, Right, Confirm, Back, Search, Favorite, PreviousPage, NextPage, Overlay, PageUp, PageDown }
public readonly record struct PadState(uint Buttons, short X, short Y, short LeftTrigger, short RightTrigger,short RightX=0,short RightY=0);

public sealed class InputInterpreter
{
    private uint previous;
    private ShellInput held;
    private long nextRepeat;
    private long? guideSince;
    private bool guideFired;
    public const int Deadzone = 13000;
    public static bool IsActive(PadState s) => s.Buttons != 0 || Math.Abs((int)s.X) > Deadzone || Math.Abs((int)s.Y) > Deadzone || Math.Abs((int)s.RightX)>Deadzone || Math.Abs((int)s.RightY)>Deadzone || s.LeftTrigger > 6000 || s.RightTrigger > 6000;
    public IEnumerable<ShellInput> Read(PadState s, long ms)
    {
        var results = new List<ShellInput>();
        bool Down(int b) => (s.Buttons & (1u << b)) != 0;
        bool Press(int b) => Down(b) && (previous & (1u << b)) == 0;
        if (Down(5)) { guideSince ??= ms; if (!guideFired && ms - guideSince >= 650) { results.Add(ShellInput.Overlay); guideFired = true; } }
        else { guideSince = null; guideFired = false; }
        // Start + Back is a fallback when Steam/Game Bar reserves Guide.
        if (Down(4) && Press(6)) results.Add(ShellInput.Overlay);
        if (Press(0)) results.Add(ShellInput.Confirm);
        if (Press(1)) results.Add(ShellInput.Back);
        if (Press(2)) results.Add(ShellInput.Favorite);
        if (Press(3)) results.Add(ShellInput.Search);
        if (Press(9)) results.Add(ShellInput.PreviousPage);
        if (Press(10)) results.Add(ShellInput.NextPage);
        var direction = Down(11) || s.Y < -Deadzone ? ShellInput.Up : Down(12) || s.Y > Deadzone ? ShellInput.Down : Down(13) || s.X < -Deadzone ? ShellInput.Left : Down(14) || s.X > Deadzone ? ShellInput.Right : s.LeftTrigger > 22000 ? ShellInput.PageUp : s.RightTrigger > 22000 ? ShellInput.PageDown : ShellInput.None;
        if (direction != ShellInput.None && (direction != held || ms >= nextRepeat)) { results.Add(direction); nextRepeat = ms + (direction != held ? 340 : 105); }
        held = direction; previous = s.Buttons;
        return results;
    }
}

public static class PlayerAllocator
{
    public static int Assign(string identity, IReadOnlyDictionary<string, int> preferred, IEnumerable<int> occupied)
    {
        var used = occupied.ToHashSet();
        if (preferred.TryGetValue(identity, out int slot) && slot is >= 1 and <= 4 && !used.Contains(slot)) return slot;
        return Enumerable.Range(1, 4).FirstOrDefault(i => !used.Contains(i));
    }
}

public static class SafetyPolicy
{
    // A store cannot be closed when synchronization state is unknown.
    public static bool CanCloseStore(bool startedByPame, bool gameActive, bool? downloading, bool? cloudSyncing) => startedByPame && !gameActive && downloading == false && cloudSyncing == false;
    public static bool IsWithin(string candidate, string root)
    {
        if (!Path.IsPathFullyQualified(candidate) || !Path.IsPathFullyQualified(root)) return false;
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(candidate).StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
    }
    public static bool IsSafeGameProcess(string path, string gameRoot)
    {
        if(!IsWithin(path,gameRoot))return false;
        string name=Path.GetFileNameWithoutExtension(path);
        return !new[]{"crash","unins","setup","reporter","installer","updater","anticheat","battleye","bootstrapper","cefsubprocess","webhelper"}.Any(x=>name.Contains(x,StringComparison.OrdinalIgnoreCase))
            &&!name.Contains("_EAC_",StringComparison.OrdinalIgnoreCase)&&!name.EndsWith("_EAC",StringComparison.OrdinalIgnoreCase)&&!name.EndsWith("_BE",StringComparison.OrdinalIgnoreCase)
            &&!name.Equals("FortniteLauncher",StringComparison.OrdinalIgnoreCase);
    }
}
