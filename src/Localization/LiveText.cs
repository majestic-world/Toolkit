using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace L2Toolkit.Localization;

/// <summary>
/// State text built in code (status, counters, composed titles) that is rebuilt when the language changes:
/// <c>LiveText.Set(StatusText, () =&gt; Loc.Geodata.Converted(count))</c>. Targets are held weakly, so closed
/// pages and windows are not kept alive. Once registered, every write to that property must go through
/// <see cref="Set(AvaloniaObject, AvaloniaProperty{string}, Func{string})"/> or <see cref="Clear"/>:
/// a direct assignment is overwritten on the next language change. UI thread only.
/// </summary>
public static class LiveText
{
    private sealed class Entry(AvaloniaProperty<string?> property, Func<string> text)
    {
        public AvaloniaProperty<string?> Property = property;
        public Func<string> Text = text;
    }

    private static readonly ConditionalWeakTable<AvaloniaObject, Entry> Entries = new();
    private static readonly List<WeakReference<AvaloniaObject>> Targets = [];
    private static int _pruneAt = 64;
    private static bool _subscribed;

    public static void Set(TextBlock target, Func<string> text) => Set(target, TextBlock.TextProperty, text);

    public static void Set(AvaloniaObject target, AvaloniaProperty<string?> property, Func<string> text)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (Entries.TryGetValue(target, out var entry))
        {
            if (entry.Property != property)
                target.ClearValue(entry.Property);
            entry.Property = property;
            entry.Text = text;
        }
        else
        {
            Entries.Add(target, new Entry(property, text));
            Targets.Add(new WeakReference<AvaloniaObject>(target));
            if (Targets.Count >= _pruneAt)
            {
                Prune();
                _pruneAt = Math.Max(64, Targets.Count * 2);
            }
        }

        if (!_subscribed)
        {
            AppLanguage.Changed += Refresh;
            _subscribed = true;
        }
        target.SetValue(property, text());
    }

    /// <summary>Stops updating <paramref name="target"/>; its current text stays until the caller writes another.</summary>
    public static void Clear(AvaloniaObject target)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (Entries.Remove(target))
            Targets.RemoveAll(reference => !reference.TryGetTarget(out var alive) || ReferenceEquals(alive, target));
    }

    private static void Refresh()
    {
        Prune();
        foreach (var reference in Targets)
        {
            if (reference.TryGetTarget(out var target) && Entries.TryGetValue(target, out var entry))
                target.SetValue(entry.Property, entry.Text());
        }
    }

    private static void Prune() =>
        Targets.RemoveAll(reference => !reference.TryGetTarget(out var target) || !Entries.TryGetValue(target, out _));
}
