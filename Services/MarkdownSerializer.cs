using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PastelNotes.ViewModels;

namespace PastelNotes.Services;

public static class MarkdownSerializer
{
    public static string Serialize(IEnumerable<BlockViewModel> blocks)
    {
        var sb = new StringBuilder();
        foreach (var b in blocks)
        {
            sb.Append(' ', b.Level * 2);
            sb.Append(b.Type switch
            {
                BlockType.Heading1 => "# ",
                BlockType.Heading2 => "## ",
                BlockType.Bullet => "- ",
                BlockType.Checklist => b.IsChecked ? "- [x] " : "- [ ] ",
                BlockType.Branch => b.IsExpanded ? "> " : ">+ ",
                _ => ""
            });
            sb.Append(b.Text).Append('\n');
        }
        return sb.ToString().TrimEnd('\n');
    }

    public static List<BlockViewModel> Parse(string? markdown)
    {
        var result = new List<BlockViewModel>();
        if (string.IsNullOrEmpty(markdown)) return result;

        foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            int spaces = 0, i = 0;
            while (i < raw.Length && (raw[i] == ' ' || raw[i] == '\t'))
            {
                spaces += raw[i] == '\t' ? 2 : 1;
                i++;
            }
            var line = raw.Substring(i);
            var block = new BlockViewModel { Level = spaces / 2 };

            string rest;
            if (TryPrefix(line, "- [ ]", out rest)) { block.Type = BlockType.Checklist; block.Text = rest; }
            else if (TryPrefix(line, "- [x]", out rest) || TryPrefix(line, "- [X]", out rest))
            { block.Type = BlockType.Checklist; block.IsChecked = true; block.Text = rest; }
            else if (TryPrefix(line, "-", out rest) || TryPrefix(line, "*", out rest))
            { block.Type = BlockType.Bullet; block.Text = rest; }
            else if (TryPrefix(line, "##", out rest)) { block.Type = BlockType.Heading2; block.Text = rest; }
            else if (TryPrefix(line, "#", out rest)) { block.Type = BlockType.Heading1; block.Text = rest; }
            else if (TryPrefix(line, ">+", out rest)) { block.Type = BlockType.Branch; block.IsExpanded = false; block.Text = rest; }
            else if (TryPrefix(line, ">", out rest)) { block.Type = BlockType.Branch; block.Text = rest; }
            else block.Text = line;

            result.Add(block);
        }

        for (int k = 0; k < result.Count; k++)
        {
            int max = k == 0 ? 0 : result[k - 1].Level + 1;
            if (result[k].Level > max) result[k].Level = max;
        }
        return result;
    }

    private static bool TryPrefix(string line, string prefix, out string rest)
    {
        if (line == prefix) { rest = ""; return true; }
        if (line.StartsWith(prefix + " ", StringComparison.Ordinal))
        {
            rest = line.Substring(prefix.Length + 1);
            return true;
        }
        rest = line;
        return false;
    }
}
