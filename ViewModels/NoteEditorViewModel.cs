using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using PastelNotes.Infrastructure;
using PastelNotes.Services;

namespace PastelNotes.ViewModels;

public class NoteEditorViewModel : ObservableObject
{
    private static readonly Dictionary<string, BlockType> Shortcuts = new()
    {
        ["# "] = BlockType.Heading1,
        ["## "] = BlockType.Heading2,
        ["- "] = BlockType.Bullet,
        ["* "] = BlockType.Bullet,
        ["[] "] = BlockType.Checklist,
        ["[ ] "] = BlockType.Checklist,
        ["- [ ] "] = BlockType.Checklist,
        ["> "] = BlockType.Branch,
    };

    private bool _loading;
    private bool _refreshing;
    private bool _isRawMode;
    private string _rawText = "";

    public NoteEditorViewModel()
    {
        Blocks.CollectionChanged += OnBlocksChanged;
    }

    public ObservableCollection<BlockViewModel> Blocks { get; } = new();

    public BlockViewModel? CurrentBlock { get; set; }

    public event Action<string>? ContentChanged;

    public string RawText
    {
        get => _rawText;
        set
        {
            if (Set(ref _rawText, value ?? "") && !_loading)
                ContentChanged?.Invoke(_rawText);
        }
    }

    public bool IsRawMode
    {
        get => _isRawMode;
        set
        {
            if (_isRawMode == value) return;
            _isRawMode = value;
            if (value)
            {
                _loading = true;
                RawText = Serialize();
                _loading = false;
            }
            else
            {
                Load(_rawText);
                RaiseChanged();
            }
            OnPropertyChanged();
        }
    }

    public string Serialize() => MarkdownSerializer.Serialize(Blocks);

    public void Load(string? markdown)
    {
        _loading = true;
        try
        {
            foreach (var b in Blocks) b.PropertyChanged -= OnBlockChanged;
            Blocks.Clear();
            foreach (var b in MarkdownSerializer.Parse(markdown)) Blocks.Add(b);
            if (Blocks.Count == 0) Blocks.Add(new BlockViewModel());
            CurrentBlock = null;
            if (_isRawMode) RawText = markdown ?? "";
        }
        finally { _loading = false; }
        RefreshStructure();
    }

    private void OnBlocksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (BlockViewModel b in e.OldItems) b.PropertyChanged -= OnBlockChanged;
        if (e.NewItems != null)
            foreach (BlockViewModel b in e.NewItems) b.PropertyChanged += OnBlockChanged;

        if (_loading || _refreshing) return;
        RefreshStructure();
        RaiseChanged();
    }

    private void OnBlockChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_loading || _refreshing || sender is not BlockViewModel b) return;

        switch (e.PropertyName)
        {
            case nameof(BlockViewModel.Text):
                if (Shortcuts.ContainsKey(b.Text))
                {
                    Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                        new Action(() => ApplyShortcut(b)));
                }
                RaiseChanged();
                break;
            case nameof(BlockViewModel.Type):
            case nameof(BlockViewModel.Level):
            case nameof(BlockViewModel.IsChecked):
            case nameof(BlockViewModel.IsExpanded):
                RefreshStructure();
                RaiseChanged();
                break;
        }
    }

    private void RaiseChanged()
    {
        if (_loading) return;
        ContentChanged?.Invoke(Serialize());
    }

    private void Batch(Action action)
    {
        _refreshing = true;
        try { action(); }
        finally { _refreshing = false; }
        RefreshStructure();
        RaiseChanged();
    }

    private void RefreshStructure()
    {
        _refreshing = true;
        try
        {
            for (int i = 0; i < Blocks.Count; i++)
            {
                int max = i == 0 ? 0 : Blocks[i - 1].Level + 1;
                if (Blocks[i].Level > max) Blocks[i].Level = max;
            }

            int? hideBelow = null;
            for (int i = 0; i < Blocks.Count; i++)
            {
                var b = Blocks[i];

                if (hideBelow is int h && b.Level > h)
                {
                    b.IsVisible = false;
                }
                else
                {
                    hideBelow = null;
                    b.IsVisible = true;
                    if (b.Type == BlockType.Branch && !b.IsExpanded) hideBelow = b.Level;
                }

                if (b.Type == BlockType.Branch)
                {
                    int done = 0, total = 0;
                    for (int j = i + 1; j < Blocks.Count && Blocks[j].Level > b.Level; j++)
                    {
                        if (Blocks[j].Type != BlockType.Checklist) continue;
                        total++;
                        if (Blocks[j].IsChecked) done++;
                    }
                    b.Progress = total > 0 ? $"{done}/{total}" : "";
                }
                else
                {
                    b.Progress = "";
                }
            }
        }
        finally { _refreshing = false; }
    }

    private void ApplyShortcut(BlockViewModel b)
    {
        if (Shortcuts.TryGetValue(b.Text, out var type))
        {
            b.Type = type;
            b.Text = "";
        }
    }

    private int SubtreeCount(int index)
    {
        int n = 1;
        while (index + n < Blocks.Count && Blocks[index + n].Level > Blocks[index].Level) n++;
        return n;
    }

    public BlockViewModel Split(BlockViewModel b, int caret)
    {
        caret = Math.Clamp(caret, 0, b.Text.Length);
        string tail = b.Text.Substring(caret);
        int index = Blocks.IndexOf(b);
        bool hasChildren = index + 1 < Blocks.Count && Blocks[index + 1].Level > b.Level;

        var nb = new BlockViewModel { Text = tail, Level = b.Level };
        switch (b.Type)
        {
            case BlockType.Checklist: nb.Type = BlockType.Checklist; break;
            case BlockType.Bullet: nb.Type = BlockType.Bullet; break;
            case BlockType.Branch:
                nb.Type = BlockType.Checklist;
                nb.Level = b.Level + 1;
                break;
            default: nb.Type = BlockType.Text; break;
        }
        if (hasChildren && b.Type != BlockType.Branch) nb.Level = b.Level + 1;

        Batch(() =>
        {
            if (b.Text != b.Text.Substring(0, caret)) b.Text = b.Text.Substring(0, caret);
            if (b.Type == BlockType.Branch) b.IsExpanded = true;
            Blocks.Insert(index + 1, nb);
        });
        return nb;
    }

    public BlockViewModel? MergeIntoPrevious(BlockViewModel b, out int caret)
    {
        caret = 0;
        var prev = PreviousVisible(b);
        if (prev == null) return null;

        caret = prev.Text.Length;
        Batch(() =>
        {
            prev.Text += b.Text;
            Blocks.Remove(b);
        });
        return prev;
    }

    public BlockViewModel? PreviousVisible(BlockViewModel b)
    {
        for (int i = Blocks.IndexOf(b) - 1; i >= 0; i--)
            if (Blocks[i].IsVisible) return Blocks[i];
        return null;
    }

    public BlockViewModel? NextVisible(BlockViewModel b)
    {
        int index = Blocks.IndexOf(b);
        if (index < 0) return null;
        for (int i = index + 1; i < Blocks.Count; i++)
            if (Blocks[i].IsVisible) return Blocks[i];
        return null;
    }

    public void Indent(BlockViewModel b)
    {
        int i = Blocks.IndexOf(b);
        if (i <= 0 || b.Level > Blocks[i - 1].Level) return;
        int n = SubtreeCount(i);
        Batch(() => { for (int k = i; k < i + n; k++) Blocks[k].Level++; });
    }

    public void Outdent(BlockViewModel b)
    {
        int i = Blocks.IndexOf(b);
        if (i < 0 || b.Level == 0) return;
        int n = SubtreeCount(i);
        Batch(() => { for (int k = i; k < i + n; k++) Blocks[k].Level--; });
    }

    public void MoveUp(BlockViewModel b)
    {
        int i = Blocks.IndexOf(b);
        if (i <= 0) return;
        int n = SubtreeCount(i);

        int p = i - 1;
        while (p >= 0 && Blocks[p].Level > b.Level) p--;
        if (p < 0 || Blocks[p].Level != b.Level) return;

        Batch(() => { for (int k = 0; k < n; k++) Blocks.Move(i + k, p + k); });
    }

    public void MoveDown(BlockViewModel b)
    {
        int i = Blocks.IndexOf(b);
        if (i < 0) return;
        int n = SubtreeCount(i);
        int j = i + n;
        if (j >= Blocks.Count || Blocks[j].Level != b.Level) return;
        int m = SubtreeCount(j);

        Batch(() => { for (int k = 0; k < m; k++) Blocks.Move(j + k, i + k); });
    }

    public void Remove(BlockViewModel b)
    {
        Batch(() =>
        {
            Blocks.Remove(b);
            if (Blocks.Count == 0) Blocks.Add(new BlockViewModel());
        });
    }

    public void SetType(BlockViewModel b, BlockType type) => b.Type = type;

    public void Toggle(BlockViewModel b)
    {
        if (b.Type == BlockType.Checklist) b.IsChecked = !b.IsChecked;
        else if (b.Type == BlockType.Branch) b.IsExpanded = !b.IsExpanded;
    }

    public void SetAllBranches(bool expanded)
        => Batch(() => { foreach (var b in Blocks.Where(x => x.Type == BlockType.Branch)) b.IsExpanded = expanded; });

    public BlockViewModel AppendBlock()
    {
        var last = Blocks.LastOrDefault();
        if (last != null && last.Type == BlockType.Text && last.Text.Length == 0 && last.Level == 0)
            return last;
        var nb = new BlockViewModel();
        Blocks.Add(nb);
        return nb;
    }

    public BlockViewModel? PasteMarkdown(BlockViewModel target, string markdown)
    {
        var parsed = MarkdownSerializer.Parse(markdown);
        if (parsed.Count == 0) return null;

        int index = Blocks.IndexOf(target);
        if (index < 0) index = Blocks.Count - 1;
        int baseLevel = target.Level;
        bool replace = string.IsNullOrEmpty(target.Text);

        Batch(() =>
        {
            if (replace) Blocks.RemoveAt(index); else index++;
            foreach (var p in parsed)
            {
                p.Level += baseLevel;
                Blocks.Insert(index++, p);
            }
        });
        return parsed[^1];
    }
}
