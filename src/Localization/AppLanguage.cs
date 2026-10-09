using System;
using System.Globalization;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Threading;
using L2Toolkit.Settings;

namespace L2Toolkit.Localization;

/// <summary>UI languages. Tags: "pt-BR" (default) and "en"; the choice persists as <c>app_language</c>.</summary>
public enum UiLanguage { PtBr, En }

/// <summary>
/// Active UI language. Texts come from Localization/Strings*.resx through the generated
/// <see cref="Loc"/>, <see cref="LocKey"/> and <see cref="LocCatalog"/>: AXAML uses
/// <c>{DynamicResource Area.Key}</c>, code uses <c>Loc.Area.Key</c>, and <see cref="Apply"/> switches both live.
/// </summary>
public static class AppLanguage
{
    private static Snapshot _state = new(UiLanguage.PtBr, LocCatalog.PtBr);
    private static IResourceProvider? _resources;

    /// <summary>Active language and its texts, published together so readers on any thread see a matching pair.</summary>
    private sealed class Snapshot(UiLanguage language, string[] values)
    {
        public readonly UiLanguage Language = language;
        public readonly string[] Values = values;
    }

    public static UiLanguage Current => Volatile.Read(ref _state).Language;

    /// <summary>Raised on the UI thread after a language is applied.</summary>
    public static event Action? Changed;

    private const string SettingKey = "app_language";
    private const string PtBrTag = "pt-BR";
    private const string EnTag = "en";

    /// <summary>Saved language: <c>en</c> → En; <c>pt-BR</c>, absent or anything else → PtBr. Never writes.</summary>
    public static UiLanguage Saved => Parse(AppDatabase.GetInstance().GetValue(SettingKey, PtBrTag));

    /// <summary>Language for a tag (<c>en</c>, <c>pt-BR</c>); unknown or absent → PtBr.</summary>
    public static UiLanguage Parse(string? tag) => tag == EnTag ? UiLanguage.En : UiLanguage.PtBr;

    /// <summary>Tag of <paramref name="language"/>, as persisted and used by the language selector.</summary>
    public static string Tag(UiLanguage language) => language switch
    {
        UiLanguage.En => EnTag,
        _ => PtBrTag,
    };

    public static void ApplySaved() => Apply(Saved);

    /// <summary>Applies <paramref name="language"/> and persists it. UI thread only.</summary>
    public static void Set(UiLanguage language)
    {
        Apply(language);
        AppDatabase.GetInstance().UpdateValue(SettingKey, Tag(language));
    }

    /// <summary>
    /// Makes <paramref name="language"/> active: replaces the language slot of
    /// <c>Application.Resources.MergedDictionaries</c> (one notification for every DynamicResource),
    /// publishes the texts read by <see cref="Loc"/>, then raises <see cref="Changed"/>. UI thread only.
    /// </summary>
    public static void Apply(UiLanguage language)
    {
        Dispatcher.UIThread.VerifyAccess();
        var values = Values(language);
        var dictionary = new ResourceDictionary();
        foreach (var index in LocCatalog.ResourceIndexes)
            dictionary.Add(LocCatalog.Keys[index], values[index]);

        var merged = Application.Current!.Resources.MergedDictionaries;
        var slot = _resources is null ? -1 : merged.IndexOf(_resources);
        if (slot < 0)
            merged.Add(dictionary);
        else
            merged[slot] = dictionary;
        _resources = dictionary;

        Volatile.Write(ref _state, new Snapshot(language, values));
        Changed?.Invoke();
    }

    /// <summary>
    /// Binding to a text that follows language changes, for controls built in code:
    /// <c>new TextBlock { [!TextBlock.TextProperty] = AppLanguage.Bind(LocKey.Common.Cancel) }</c>.
    /// </summary>
    public static IBinding Bind(string key) => new DynamicResourceExtension(key);

    internal static string Text(int index) => Volatile.Read(ref _state).Values[index];

    internal static string Format(int index, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, Volatile.Read(ref _state).Values[index], args);

    /// <summary>
    /// Formats the One or Other variant chosen by <paramref name="count"/> under the active language's rule.
    /// <paramref name="args"/> holds every placeholder value, <paramref name="count"/> first (placeholder 0).
    /// </summary>
    internal static string Plural(int one, int other, int count, params object?[] args)
    {
        var state = Volatile.Read(ref _state);
        return string.Format(CultureInfo.InvariantCulture, state.Values[IsOne(state.Language, count) ? one : other], args);
    }

    private static string[] Values(UiLanguage language) => language switch
    {
        UiLanguage.En => LocCatalog.En,
        _ => LocCatalog.PtBr,
    };

    // Plural rule per language; both currently use the singular for exactly one.
    private static bool IsOne(UiLanguage language, int count) => language switch
    {
        UiLanguage.En => count == 1,
        _ => count == 1,
    };
}
