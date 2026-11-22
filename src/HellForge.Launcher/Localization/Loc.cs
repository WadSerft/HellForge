using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Text.Json;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace HellForge.Launcher.Localization;

// The launcher's localization: the UI's text in every language it ships. A string's key is its English text, so an
// entry missing from a translation shows the English one (Strings.resx holds it; Strings.zh-CN.resx the Chinese), and
// untranslated additions never break a page. Loc.S is for text set from code; {loc:Tr '...'} in .axaml binds through a
// LocSource that refreshes when the language changes, so the Settings dropdown switches the whole window at once.
public static class Loc
{
    static readonly ResourceManager resources = new("HellForge.Launcher.Localization.Strings", typeof(Loc).Assembly);
    static readonly List<WeakReference<LocSource>> bound = [];

    // "en" and the cultures a Strings.<culture>.resx exists for, in display order.
    public static IReadOnlyList<string> Available { get; } = ["en", "zh-CN"];

    public static string Language { get; private set; } = "en";
    static CultureInfo Culture => Language == "en" ? CultureInfo.InvariantCulture : new CultureInfo(Language);

    static string? file;

    // Called once at startup, before the main window: remembers where the choice is saved and applies it.
    public static void Initialize(string? settingsFolder)
    {
        file = settingsFolder == null ? null : Path.Combine(settingsFolder, "language.json");
        try
        {
            if (file != null && File.Exists(file))
            {
                var saved = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
                if (saved != null && saved.TryGetValue("language", out var name) && Available.Contains(name))
                    Language = name;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { }
    }

    // The text for an English key in the current language. Keys are the English text lowercased with spaces folded to
    // underscores: MSBuild's resx writer drops case-insensitively equal names (MSB3568, "NEW PROFILE" vs "New profile"),
    // so every name is stored folded and lowercase, with _2 for a second text that folds alike.
    public static string S(string key)
    {
        if (Language == "en") return key;
        var name = key.Replace(' ', '_').ToLowerInvariant();
        var text = resources.GetString(name, Culture) ?? resources.GetString(name + "_2", Culture);
        return text ?? key;
    }

    // A bindable source for one key (TrExtension), kept alive by the bindings that use it.
    public static LocSource Bind(string key) => new(key);

    internal static void Register(LocSource source)
    {
        for (var i = bound.Count - 1; i >= 0; i--)
            if (!bound[i].TryGetTarget(out _))
                bound.RemoveAt(i);
        bound.Add(new WeakReference<LocSource>(source));
    }

    public static void SetLanguage(string name)
    {
        if (!Available.Contains(name) || name == Language)
            return;
        Language = name;
        try
        {
            if (file != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllText(file, JsonSerializer.Serialize(new Dictionary<string, string> { ["language"] = name }) + "\n");
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }  // Only a convenience.
        foreach (var reference in bound)
            if (reference.TryGetTarget(out var source))
                source.Refresh();
    }
}

// One localized string as a binding source: Value is the key's text now, and Refresh (on a language change) makes
// every control bound to it show the new language.
public sealed class LocSource : INotifyPropertyChanged
{
    public string Key { get; }
    public string Value => Loc.S(Key);

    internal LocSource(string key) => Key = key;

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
}

// {loc:Tr 'English text'} in .axaml: a one-way binding to that text in the current language.
public sealed class TrExtension : MarkupExtension
{
    readonly string key;

    public TrExtension(string key) => this.key = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding("Value") { Source = Loc.Bind(key), Mode = BindingMode.OneWay };
}
