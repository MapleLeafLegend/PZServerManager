using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace PZServerManager;

public sealed class LanguagePack
{
    public string Code { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public Dictionary<string, string> Translations { get; set; } = new();
}

public sealed record LanguageOption(string Code, string DisplayName);

internal static class LocalizationService
{
    private const string SourceLanguage = "zh-TW";
    private static readonly ConditionalWeakTable<DependencyObject, OriginalTextState> Originals = new();
    private static readonly Dictionary<string, LanguagePack> Packs =
        new(StringComparer.OrdinalIgnoreCase);

    public static string LanguageDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Languages");

    public static string CurrentLanguage { get; private set; } = SourceLanguage;

    public static IReadOnlyList<LanguageOption> AvailableLanguages { get; private set; } =
        new ReadOnlyCollection<LanguageOption>(
            new[] { new LanguageOption(SourceLanguage, "繁體中文") });

    public static void Reload()
    {
        Packs.Clear();
        Packs[SourceLanguage] = new LanguagePack
        {
            Code = SourceLanguage,
            DisplayName = "繁體中文"
        };

        try
        {
            Directory.CreateDirectory(LanguageDirectory);
            foreach (var path in Directory.EnumerateFiles(LanguageDirectory, "*.json",
                         SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var pack = JsonSerializer.Deserialize<LanguagePack>(
                        File.ReadAllText(path),
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            ReadCommentHandling = JsonCommentHandling.Skip,
                            AllowTrailingCommas = true
                        });
                    if (pack == null || string.IsNullOrWhiteSpace(pack.Code) ||
                        string.IsNullOrWhiteSpace(pack.DisplayName))
                        continue;
                    pack.Translations = new Dictionary<string, string>(
                        pack.Translations ?? new Dictionary<string, string>(),
                        StringComparer.Ordinal);
                    Packs[pack.Code.Trim()] = pack;
                }
                catch
                {
                    // A broken community translation must not prevent the manager from starting.
                }
            }
        }
        catch
        {
            // Read-only application directories still retain the built-in source language.
        }

        AvailableLanguages = Packs.Values
            .OrderBy(pack => pack.Code.Equals(SourceLanguage, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(pack => pack.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(pack => new LanguageOption(pack.Code, pack.DisplayName))
            .ToList()
            .AsReadOnly();

        if (!Packs.ContainsKey(CurrentLanguage))
            CurrentLanguage = SourceLanguage;
    }

    public static bool SetLanguage(string? code)
    {
        var selected = string.IsNullOrWhiteSpace(code) ? SourceLanguage : code.Trim();
        if (!Packs.ContainsKey(selected))
            selected = SourceLanguage;
        var changed = !CurrentLanguage.Equals(selected, StringComparison.OrdinalIgnoreCase);
        CurrentLanguage = selected;
        return changed;
    }

    public static string Translate(string source)
    {
        if (CurrentLanguage.Equals(SourceLanguage, StringComparison.OrdinalIgnoreCase) ||
            !Packs.TryGetValue(CurrentLanguage, out var pack))
            return source;
        return pack.Translations.TryGetValue(source, out var translated) &&
               !string.IsNullOrWhiteSpace(translated)
            ? translated
            : source;
    }

    public static string Format(string source, params object?[] arguments) =>
        string.Format(Translate(source), arguments);

    public static void SetText(TextBlock target, string source)
    {
        var state = Originals.GetOrCreateValue(target);
        state.Text = source;
        state.HasText = true;
        target.Text = Translate(source);
    }

    public static void SetFormattedText(TextBlock target, string source, params object?[] arguments)
    {
        var state = Originals.GetOrCreateValue(target);
        state.Text = string.Format(source, arguments);
        state.HasText = true;
        target.Text = string.Format(Translate(source), arguments);
    }

    public static void SetTitle(Window target, string source)
    {
        var state = Originals.GetOrCreateValue(target);
        state.Title = source;
        state.HasTitle = true;
        target.Title = Translate(source);
    }

    public static void SetFormattedTitle(Window target, string source, params object?[] arguments)
    {
        var state = Originals.GetOrCreateValue(target);
        state.Title = string.Format(source, arguments);
        state.HasTitle = true;
        target.Title = string.Format(Translate(source), arguments);
    }

    public static void Apply(DependencyObject root)
    {
        ApplyNode(root);
    }

    private static void ApplyNode(DependencyObject node)
    {
        var state = Originals.GetOrCreateValue(node);

        if (node is Window window)
        {
            if (!state.HasTitle)
            {
                state.Title = window.Title;
                state.HasTitle = true;
            }
            window.Title = Translate(state.Title ?? "");
        }

        if (node is Run run)
        {
            if (!state.HasText)
            {
                state.Text = run.Text;
                state.HasText = true;
            }
            run.Text = Translate(state.Text ?? "");
        }
        else if (node is TextBlock textBlock &&
                 !textBlock.Inlines.OfType<LineBreak>().Any())
        {
            if (!state.HasText)
            {
                state.Text = textBlock.Text;
                state.HasText = true;
            }
            textBlock.Text = Translate(state.Text ?? "");
        }

        if (node is HeaderedContentControl headeredContent)
        {
            if (!state.HasHeader && headeredContent.Header is string header)
            {
                state.Header = header;
                state.HasHeader = true;
            }
            if (state.HasHeader)
                headeredContent.Header = Translate(state.Header ?? "");
        }
        else if (node is HeaderedItemsControl headeredItems)
        {
            if (!state.HasHeader && headeredItems.Header is string header)
            {
                state.Header = header;
                state.HasHeader = true;
            }
            if (state.HasHeader)
                headeredItems.Header = Translate(state.Header ?? "");
        }

        if (node is ContentControl contentControl &&
            node is not HeaderedContentControl)
        {
            if (!state.HasContent && contentControl.Content is string content)
            {
                state.Content = content;
                state.HasContent = true;
            }
            if (state.HasContent)
                contentControl.Content = Translate(state.Content ?? "");
        }

        if (node is FrameworkElement element)
        {
            if (!state.HasToolTip && element.ToolTip is string toolTip)
            {
                state.ToolTip = toolTip;
                state.HasToolTip = true;
            }
            if (state.HasToolTip)
                element.ToolTip = Translate(state.ToolTip ?? "");
        }

        if (node is DataGrid dataGrid)
        {
            foreach (var column in dataGrid.Columns)
            {
                var columnState = Originals.GetOrCreateValue(column);
                if (!columnState.HasHeader && column.Header is string header)
                {
                    columnState.Header = header;
                    columnState.HasHeader = true;
                }
                if (columnState.HasHeader)
                    column.Header = Translate(columnState.Header ?? "");
            }
        }

        if (node is ItemsControl itemsControl)
        {
            foreach (var item in itemsControl.Items)
                if (item is DependencyObject itemObject)
                    ApplyNode(itemObject);
        }

        foreach (var child in LogicalTreeHelper.GetChildren(node))
            if (child is DependencyObject childObject)
                ApplyNode(childObject);
    }

    private sealed class OriginalTextState
    {
        public string? Text;
        public string? Title;
        public string? Header;
        public string? Content;
        public string? ToolTip;
        public bool HasText;
        public bool HasTitle;
        public bool HasHeader;
        public bool HasContent;
        public bool HasToolTip;
    }
}
