using System;
using System.Collections.Generic;
using System.Text;
using Dalamud.Bindings.ImGui;

namespace GatherBuddy.Gui;

internal static class FishStrategyTextRenderer
{
    public static void DrawParagraph(string paragraph, float width)
    {
        var line      = new StringBuilder();
        var lineWidth = 0f;

        foreach (var token in Tokens(paragraph))
        {
            var tokenWidth = ImGui.CalcTextSize(token).X;
            if (line.Length > 0 && lineWidth + tokenWidth > width)
            {
                ImGui.TextUnformatted(line.ToString());
                line.Clear();
                lineWidth = 0f;
            }

            line.Append(token);
            lineWidth += tokenWidth;
        }

        if (line.Length > 0)
            ImGui.TextUnformatted(line.ToString());
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
