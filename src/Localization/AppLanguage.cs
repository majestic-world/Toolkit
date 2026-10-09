using System;
using System.Globalization;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Threading;

namespace L2Toolkit.Localization;

/// <summary>UI languages. Tags: "pt-BR" (default) and "en".</summary>
public enum UiLanguage { PtBr, En }

/// <summary>
/// Active UI language. Texts come from Localization/Strings*.resx through the generated
/// <see cref="Loc"/>, <see cref="LocKey"/> and <see cref="LocCatalog"/>: AXAML uses
/// <c>{DynamicResource Area.Key}</c>, code uses <c>Loc.Area.Key</c>, and <see cref="Apply"/> switches both live.
/// </summary>
public static class AppLanguage
{
    private static string[] _values = LocCatalog.PtBr;
    private static IResourceProvider? _resources;

    public static UiLanguage Current { get; private set; } = UiLanguage.PtBr;

    /// <summary>Raised on the UI thread after a language is applied.</summary>
    public static event Action? Changed;

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

        Volatile.Write(ref _values, values);
        Current = language;
        Changed?.Invoke();
    }

    /// <summary>
    /// Binding to a text that follows language changes, for controls built in code:
    /// <c>new TextBlock { [!TextBlock.TextProperty] = AppLanguage.Bind(LocKey.Common.Cancel) }</c>.
    /// </summary>
    public static IBinding Bind(string key) => new DynamicResourceExtension(key);

    internal static string Text(int index) => Volatile.Read(ref _values)[index];

    internal static string Format(int index, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, Volatile.Read(ref _values)[index], args);

    /// <summary>Formats the One or Other variant; <paramref name="count"/> is placeholder 0, then <paramref name="args"/>.</summary>
    internal static string Plural(int one, int other, int count, params object?[] args)
    {
        var values = Volatile.Read(ref _values);
        var all = new object?[args.Length + 1];
        all[0] = count;
        args.CopyTo(all, 1);
        return string.Format(CultureInfo.InvariantCulture, values[IsOne(Current, count) ? one : other], all);
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
