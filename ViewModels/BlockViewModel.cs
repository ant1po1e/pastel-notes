using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using PastelNotes.Infrastructure;

namespace PastelNotes.ViewModels;

public enum BlockType { Text, Heading1, Heading2, Bullet, Checklist, Branch }

public class BlockViewModel : ObservableObject
{
    private string _text = "";
    private BlockType _type = BlockType.Text;
    private bool _isChecked;
    private bool _isExpanded = true;
    private int _level;
    private bool _isVisible = true;
    private string _progress = "";

    public string Text { get => _text; set => Set(ref _text, value ?? ""); }
    public BlockType Type { get => _type; set => Set(ref _type, value); }
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value); }
    public bool IsExpanded { get => _isExpanded; set => Set(ref _isExpanded, value); }

    public int Level
    {
        get => _level;
        set
        {
            if (Set(ref _level, Math.Max(0, value)))
                OnPropertyChanged(nameof(IndentMargin));
        }
    }

    public bool IsVisible { get => _isVisible; set => Set(ref _isVisible, value); }

    public string Progress { get => _progress; set => Set(ref _progress, value); }

    public Thickness IndentMargin => new(Level * 26, 0, 0, 0);
}
