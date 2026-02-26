using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace M_A_G_A.Helpers
{
    /// <summary>
    /// Attached property that renders simple Markdown into a TextBlock's Inlines.
    /// Supported syntax:
    ///   **bold**    *italic*    ~~strike~~    `code`
    ///   [text](url) — rendered as clickable hyperlink
    ///   Lines starting with > are block-quoted (gray italic)
    /// </summary>
    public static class MarkdownText
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.RegisterAttached(
                "Text",
                typeof(string),
                typeof(MarkdownText),
                new PropertyMetadata(null, OnTextChanged));

        public static void SetText(TextBlock element, string value)
            => element.SetValue(TextProperty, value);

        public static string GetText(TextBlock element)
            => (string)element.GetValue(TextProperty);

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBlock tb)
            {
                tb.Inlines.Clear();
                var text = e.NewValue as string ?? "";
                foreach (var inline in ParseMarkdown(text))
                    tb.Inlines.Add(inline);
            }
        }

        // ─── Parser ──────────────────────────────────────────────────────

        private static IEnumerable<Inline> ParseMarkdown(string text)
        {
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            for (int li = 0; li < lines.Length; li++)
            {
                if (li > 0)
                    yield return new LineBreak();

                var line = lines[li];
                bool isQuote = line.StartsWith("> ");
                if (isQuote) line = line.Substring(2);

                foreach (var span in ParseInline(line, isQuote))
                    yield return span;
            }
        }

        private static readonly Regex InlinePattern = new Regex(
            @"(\*\*(.+?)\*\*)|(\*(.+?)\*)|(\~\~(.+?)\~\~)|(`(.+?)`)|(\[([^\]]+)\]\(([^\)]+)\))",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static IEnumerable<Inline> ParseInline(string line, bool isQuote)
        {
            int pos = 0;
            foreach (Match m in InlinePattern.Matches(line))
            {
                if (m.Index > pos)
                    yield return MakeRun(line.Substring(pos, m.Index - pos), isQuote);

                if (m.Groups[1].Success)            // **bold**
                {
                    var r = MakeRun(m.Groups[2].Value, isQuote);
                    yield return new Bold(r);
                }
                else if (m.Groups[3].Success)       // *italic*
                {
                    var r = MakeRun(m.Groups[4].Value, isQuote);
                    yield return new Italic(r);
                }
                else if (m.Groups[5].Success)       // ~~strike~~
                {
                    var r = MakeRun(m.Groups[6].Value, isQuote);
                    r.TextDecorations = TextDecorations.Strikethrough;
                    yield return r;
                }
                else if (m.Groups[7].Success)       // `code`
                {
                    var r = new Run(m.Groups[8].Value)
                    {
                        FontFamily  = new FontFamily("Consolas,Courier New"),
                        Background  = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                        Foreground  = new SolidColorBrush(Color.FromRgb(200, 200, 200))
                    };
                    yield return r;
                }
                else if (m.Groups[9].Success)       // [text](url)
                {
                    var linkText = m.Groups[10].Value;
                    var url      = m.Groups[11].Value;
                    var hl = new Hyperlink(new Run(linkText));
                    try { hl.NavigateUri = new Uri(url); } catch { }
                    hl.RequestNavigate += (s, ev) =>
                    {
                        try { System.Diagnostics.Process.Start(ev.Uri.AbsoluteUri); } catch { }
                        ev.Handled = true;
                    };
                    yield return hl;
                }

                pos = m.Index + m.Length;
            }

            if (pos < line.Length)
                yield return MakeRun(line.Substring(pos), isQuote);
        }

        private static Run MakeRun(string text, bool isQuote)
        {
            var r = new Run(text);
            if (isQuote)
            {
                r.Foreground  = new SolidColorBrush(Color.FromRgb(160, 160, 160));
                r.FontStyle   = FontStyles.Italic;
            }
            return r;
        }
    }
}
