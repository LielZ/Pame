using System.Text.RegularExpressions;

namespace Pame.Core;

// Independent parser for Valve text KeyValues. Bounded recursion and input size.
public sealed class Vdf
{
    public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Vdf> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string Get(string key, string fallback = "") => Values.GetValueOrDefault(key, fallback);
    public long Number(string key) => long.TryParse(Get(key), out var v) ? v : 0;
    public Vdf? Child(string key) => Children.GetValueOrDefault(key);
    public static Vdf Parse(string source)
    {
        if (source.Length > 32 * 1024 * 1024) throw new FormatException("Manifest exceeds size limit");
        var matches = Regex.Matches(source, "\"((?:\\\\.|[^\"\\\\])*)\"|([{}])|//[^\\r\\n]*|([^\\s{}\"]+)", RegexOptions.None, TimeSpan.FromSeconds(2));
        var tokens = matches.Cast<Match>().Where(m => !m.Value.StartsWith("//")).Select(m => m.Groups[1].Success ? m.Groups[1].Value.Replace("\\\"", "\"").Replace("\\\\", "\\") : m.Value).ToArray();
        int pos = 0;
        Vdf Read(int depth, bool nested)
        {
            if (depth > 48) throw new FormatException("Manifest nesting limit");
            var node = new Vdf();
            while (pos < tokens.Length)
            {
                var key = tokens[pos++];
                if (key == "}") { if (!nested) throw new FormatException("Unexpected brace"); return node; }
                if (key == "{" || pos >= tokens.Length) throw new FormatException("Invalid KeyValues pair");
                var value = tokens[pos++];
                if (value == "{") node.Children[key] = Read(depth + 1, true);
                else if (value == "}") throw new FormatException("Missing value");
                else node.Values[key] = value;
            }
            if (nested) throw new FormatException("Unclosed object");
            return node;
        }
        return Read(0, false);
    }
}
