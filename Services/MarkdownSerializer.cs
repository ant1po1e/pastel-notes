using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PastelNotes.ViewModels;

namespace PastelNotes.Services;

public static class MarkdownSerializer
{
    private static readonly Regex ImageLine = new(@"^!\[(?<alt>.*?)\]\(images/(?<file>[^)\s]+)\)\s*$", RegexOptions.Compiled);
    private static readonly Regex SeparatorCell = new(@"^\s*:?-+:?\s*$", RegexOptions.Compiled);

    public static string Serialize(IEnumerable<BlockViewModel> blocks, int levelOffset = 0)
    {
        var sb = new StringBuilder();
        foreach (var b in blocks)
        {
            string pad = new string(' ', Math.Max(0, b.Level - levelOffset) * 2);

            switch (b.Type)
            {
                case BlockType.CodeBlock:
                    sb.Append(pad).Append("```").Append(b.Language.Trim()).Append('\n');
                    foreach (var line in b.Text.Replace("\r\n", "\n").Split('\n'))
                        sb.Append(pad).Append(line).Append('\n');
                    sb.Append(pad).Append("```").Append('\n');
                    break;

                case BlockType.Image:
                    sb.Append(pad).Append("![").Append(b.Text).Append("](images/").Append(b.ImageFile).Append(")\n");
                    break;

                case BlockType.Table:
                    if (b.Table != null) AppendTable(sb, pad, b.Table);
                    break;

                default:
                    sb.Append(pad);
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
                    break;
            }
        }
        return sb.ToString().TrimEnd('\n');
    }

    private static void AppendTable(StringBuilder sb, string pad, TableViewModel table)
    {
        var rows = table.ToRows();
        for (int r = 0; r < rows.Count; r++)
        {
            sb.Append(pad).Append("| ")
              .Append(string.Join(" | ", rows[r].Select(c => c.Replace("|", "\\|").Replace("\n", " "))))
              .Append(" |\n");

            if (r == 0)
            {
                sb.Append(pad).Append("| ")
                  .Append(string.Join(" | ", rows[0].Select(_ => "---")))
                  .Append(" |\n");
            }
        }
    }

    public static List<BlockViewModel> Parse(string? markdown)
    {
        var result = new List<BlockViewModel>();
        if (string.IsNullOrEmpty(markdown)) return result;

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        int i = 0;

        while (i < lines.Length)
        {
            string raw = lines[i];
            int spaces = 0, p = 0;
            while (p < raw.Length && (raw[p] == ' ' || raw[p] == '\t'))
            {
                spaces += raw[p] == '\t' ? 2 : 1;
                p++;
            }
            string line = raw.Substring(p);
            int level = spaces / 2;

            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                var code = new BlockViewModel
                {
                    Type = BlockType.CodeBlock,
                    Level = level,
                    Language = line.Substring(3).Trim()
                };
                var content = new List<string>();
                i++;
                while (i < lines.Length && lines[i].Trim() != "```")
                {
                    var l = lines[i];
                    int strip = 0;
                    while (strip < spaces && strip < l.Length && l[strip] == ' ') strip++;
                    content.Add(l.Substring(strip));
                    i++;
                }
                i++;
                code.Text = string.Join("\n", content);
                result.Add(code);
                continue;
            }

            if (IsTableLine(line))
            {
                var rows = new List<List<string>>();
                while (i < lines.Length && IsTableLine(lines[i].TrimStart()))
                {
                    var cells = SplitRow(lines[i].TrimStart());
                    if (!cells.All(c => SeparatorCell.IsMatch(c))) rows.Add(cells);
                    i++;
                }
                var tableBlock = new BlockViewModel { Type = BlockType.Table, Level = level };
                tableBlock.Table = TableViewModel.Create(tableBlock, rows);
                result.Add(tableBlock);
                continue;
            }

            var image = ImageLine.Match(line);
            if (image.Success)
            {
                result.Add(new BlockViewModel
                {
                    Type = BlockType.Image,
                    Level = level,
                    Text = image.Groups["alt"].Value,
                    ImageFile = Path.GetFileName(image.Groups["file"].Value)
                });
                i++;
                continue;
            }

            var block = new BlockViewModel { Level = level };
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
            i++;
        }

        for (int k = 0; k < result.Count; k++)
        {
            int max = k == 0 ? 0 : result[k - 1].Level + 1;
            if (result[k].Level > max) result[k].Level = max;
        }
        return result;
    }

    private static bool IsTableLine(string line)
        => line.StartsWith("|", StringComparison.Ordinal) && line.Length > 1 && line.IndexOf('|', 1) >= 0;

    private static List<string> SplitRow(string line)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        string body = line.Trim();
        if (body.StartsWith("|")) body = body.Substring(1);
        if (body.EndsWith("|") && !body.EndsWith("\\|")) body = body.Substring(0, body.Length - 1);

        for (int i = 0; i < body.Length; i++)
        {
            if (body[i] == '\\' && i + 1 < body.Length && body[i + 1] == '|')
            {
                current.Append('|');
                i++;
            }
            else if (body[i] == '|')
            {
                cells.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(body[i]);
            }
        }
        cells.Add(current.ToString().Trim());
        return cells;
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
