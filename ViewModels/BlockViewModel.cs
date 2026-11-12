using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using PastelNotes.Infrastructure;
using PastelNotes.Services;

namespace PastelNotes.ViewModels;

public enum BlockType { Text, Heading1, Heading2, Bullet, Checklist, Branch, CodeBlock, Image, Table }

public class BlockViewModel : ObservableObject
{
    private string _text = "";
    private BlockType _type = BlockType.Text;
    private bool _isChecked;
    private bool _isExpanded = true;
    private int _level;
    private bool _isVisible = true;
    private string _progress = "";
    private string _language = "";
    private string _imageFile = "";
    private TableViewModel? _table;
    private bool _isSelected;
    private int _guideMask;
    private bool _hasChildren;

    public string Text { get => _text; set => Set(ref _text, value ?? ""); }
    public BlockType Type { get => _type; set => Set(ref _type, value); }
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value); }
    public bool IsExpanded { get => _isExpanded; set => Set(ref _isExpanded, value); }
    public string Language { get => _language; set => Set(ref _language, value ?? ""); }

    public string ImageFile
    {
        get => _imageFile;
        set
        {
            if (Set(ref _imageFile, value ?? ""))
                OnPropertyChanged(nameof(ImageSource));
        }
    }

    public ImageSource? ImageSource => string.IsNullOrEmpty(_imageFile) ? null : ImageService.Load(_imageFile);

    public TableViewModel? Table
    {
        get => _table;
        set
        {
            if (_table != null) _table.Changed -= OnTableChanged;
            _table = value;
            if (_table != null) _table.Changed += OnTableChanged;
            OnPropertyChanged();
        }
    }

    public int TableRevision { get; private set; }

    public int Level
    {
        get => _level;
        set
        {
            if (Set(ref _level, Math.Max(0, value)))
                OnPropertyChanged(nameof(IndentMargin));
        }
    }

    public int GuideMask { get => _guideMask; set => Set(ref _guideMask, value); }

    public bool HasChildren { get => _hasChildren; set => Set(ref _hasChildren, value); }

    public Thickness IndentMargin => new(Level * 26, 0, 0, 0);

    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

    public bool IsVisible { get => _isVisible; set => Set(ref _isVisible, value); }

    public string Progress { get => _progress; set => Set(ref _progress, value); }

    private void OnTableChanged()
    {
        TableRevision++;
        OnPropertyChanged(nameof(TableRevision));
    }
}
