using System;
using System.Collections.Generic;
using System.Text;
using Dalamud.Bindings.ImGui;

namespace GatherBuddy.Gui;

internal static class FishStrategyTextRenderer
{
    public static void DrawParagraph(string paragraph, float width)
    {
        var line         = new StringBuilder();
        var lines        = new List<string>();
        var prefixWidth  = ImGui.CalcTextSize("• ").X;
        var contentWidth = Math.Max(width - prefixWidth, ImGui.GetFontSize() * 4f);
        var lineWidth    = 0f;
        var pendingSpace = false;

        foreach (var token in Tokens(paragraph))
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                pendingSpace = line.Length > 0;
                continue;
            }

            var tokenWidth = ImGui.CalcTextSize(token).X;
            var spaceWidth = pendingSpace ? ImGui.CalcTextSize(" ").X : 0f;
            if (line.Length > 0 && lineWidth + spaceWidth + tokenWidth > contentWidth)
            {
                lines.Add(line.ToString());
                line.Clear();
                lineWidth    = 0f;
                pendingSpace = false;
            }

            if (pendingSpace && line.Length > 0)
            {
                line.Append(' ');
                lineWidth += spaceWidth;
            }
            line.Append(token);
            lineWidth += tokenWidth;
            pendingSpace = false;
        }

        if (line.Length > 0)
            lines.Add(line.ToString());

        if (lines.Count == 0)
            return;

        ImGui.TextUnformatted("• " + lines[0]);
        if (lines.Count == 1)
            return;

        ImGui.Indent(prefixWidth);
        try
        {
            for (var index = 1; index < lines.Count; ++index)
                ImGui.TextUnformatted(lines[index]);
        }
        finally
        {
            ImGui.Unindent(prefixWidth);
        }
    }

    private static IEnumerable<string> Tokens(string text)
    {
        for (var index = 0; index < text.Length;)
        {
            var closing = text[index] switch
            {
                '[' => ']',
                '<' => '>',
                '{' => '}',
                _   => '\0',
            };
            if (closing != '\0')
            {
                var end = text.IndexOf(closing, index + 1);
                if (end >= 0)
                {
                    yield return text[index..(end + 1)];
                    index = end + 1;
                    continue;
                }
            }

            if (text[index] <= 0x7F && IsAsciiWordCharacter(text[index]))
            {
                var end = index + 1;
                while (end < text.Length && text[end] <= 0x7F && IsAsciiWordCharacter(text[end]))
                    ++end;
                yield return text[index..end];
                index = end;
                continue;
            }

            var runeLength = Rune.GetRuneAt(text, index).Utf16SequenceLength;
            yield return text.Substring(index, runeLength);
            index += runeLength;
        }
    }

    private static bool IsAsciiWordCharacter(char character)
        => char.IsAsciiLetterOrDigit(character) || character is '~' or ':' or '.' or '%' or '+' or '-' or '/';
}
