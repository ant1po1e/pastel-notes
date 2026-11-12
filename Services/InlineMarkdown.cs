using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace PastelNotes.Services;

public static class InlineMarkdown
{
    private static readonly Regex Token = new(
        @"(?<code>`[^`\n]+`)" +
        @"|(?<link>\[(?<ltext>[^\]\n]+)\]\((?<lurl>[^)\s]+)\))" +
        @"|(?<bold>\*\*(?<btext>.+?)\*\*)" +
        @"|(?<strike>~~(?<stext>.+?)~~)" +
        @"|(?<italic>\*(?<itext>[^*\n]+?)\*)" +
        @"|(?<url>https?://[^\s<>]+)",
        RegexOptions.Compiled);

    private static readonly FontFamily Mono = new("Cascadia Mono, Consolas, Courier New");

    public static readonly DependencyProperty SourceProperty = DependencyProperty.RegisterAttached(
        "Source", typeof(string), typeof(InlineMarkdown), new PropertyMetadata("", OnSourceChanged));

    public static string GetSource(DependencyObject element) => (string)element.GetValue(SourceProperty);

    public static void SetSource(DependencyObject element, string value) => element.SetValue(SourceProperty, value);

    public static bool HasMarkup(string? text) => !string.IsNullOrEmpty(text) && Token.IsMatch(text);

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock textBlock) return;
        textBlock.Inlines.Clear();

        string text = e.NewValue as string ?? "";
        if (!HasMarkup(text)) return;

        Build(textBlock.Inlines, text, 0, false);
    }

    private static void Build(InlineCollection target, string text, int depth, bool insideLink)
    {
        int position = 0;
        foreach (Match match in Token.Matches(text))
        {
            if (match.Index > position)
                target.Add(new Run(text.Substring(position, match.Index - position)));

            if (match.Groups["code"].Success)
            {
                var run = new Run(match.Value.Substring(1, match.Length - 2)) { FontFamily = Mono };
                run.SetResourceReference(TextElement.BackgroundProperty, "CodeBgBrush");
                run.SetResourceReference(TextElement.ForegroundProperty, "CodeInlineBrush");
                target.Add(run);
            }
            else if (match.Groups["link"].Success)
            {
                if (insideLink) target.Add(new Run(match.Groups["ltext"].Value));
                else target.Add(CreateLink(match.Groups["ltext"].Value, match.Groups["lurl"].Value, depth));
            }
            else if (match.Groups["bold"].Success)
            {
                var span = new Bold();
                AddNested(span.Inlines, match.Groups["btext"].Value, depth, insideLink);
                target.Add(span);
            }
            else if (match.Groups["strike"].Success)
            {
                var span = new Span { TextDecorations = TextDecorations.Strikethrough };
                AddNested(span.Inlines, match.Groups["stext"].Value, depth, insideLink);
                target.Add(span);
            }
            else if (match.Groups["italic"].Success)
            {
                var span = new Italic();
                AddNested(span.Inlines, match.Groups["itext"].Value, depth, insideLink);
                target.Add(span);
            }
            else
            {
                if (insideLink) target.Add(new Run(match.Value));
                else target.Add(CreateLink(match.Value, match.Value, depth));
            }

            position = match.Index + match.Length;
        }

        if (position < text.Length)
            target.Add(new Run(text.Substring(position)));
    }

    private static void AddNested(InlineCollection target, string text, int depth, bool insideLink)
    {
        if (depth >= 3) target.Add(new Run(text));
        else Build(target, text, depth + 1, insideLink);
    }

    private static Hyperlink CreateLink(string label, string url, int depth)
    {
        var link = new Hyperlink { ToolTip = url, Cursor = System.Windows.Input.Cursors.Hand };
        if (depth >= 3) link.Inlines.Add(new Run(label));
        else Build(link.Inlines, label, depth + 1, true);

        link.SetResourceReference(TextElement.ForegroundProperty, "LinkBrush");
        link.Click += (_, _) => Open(url);
        return link;
    }

    private static void Open(string url)
    {
        if (!url.Contains("://") && !url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeMailto) return;

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch
        {
        }
    }
}
