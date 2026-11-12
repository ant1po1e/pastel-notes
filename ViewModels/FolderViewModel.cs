using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;
using PastelNotes.Infrastructure;
using PastelNotes.Models;
using PastelNotes.Services;

namespace PastelNotes.ViewModels;

public class FolderViewModel : ObservableObject
{
    private string _name;
    private string _colorHex;
    private bool _isExpanded;
    private bool _isEditing;
    private bool _isPaletteOpen;
    private bool _isListShown;
    private bool _isListCollapsing;
    private DispatcherTimer? _hideTimer;

    public FolderViewModel(FolderData data)
    {
        Id = data.Id;
        _name = data.Name;
        _colorHex = data.ColorHex;
        _isExpanded = data.IsExpanded;
        _isListShown = data.IsExpanded;

        ToggleCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        RenameCommand = new RelayCommand(() => IsEditing = true);
        SetColorCommand = new RelayCommand(p =>
        {
            if (p is string hex) ColorHex = hex;
            IsPaletteOpen = false;
        });

        ThemeService.Changed += () => OnPropertyChanged(nameof(SoftBrush));
    }

    public Guid Id { get; }
    public ObservableCollection<NoteViewModel> Notes { get; } = new();
    public string[] Palette => Palettes.Colors;

    public string Name
    {
        get => _name;
        set
        {
            if (Set(ref _name, value ?? ""))
                OnPropertyChanged(nameof(Initial));
        }
    }

    public string Initial => string.IsNullOrWhiteSpace(_name) ? "?" : _name.Trim().Substring(0, 1).ToUpperInvariant();

    public string ColorHex
    {
        get => _colorHex;
        set
        {
            if (!Set(ref _colorHex, value)) return;
            OnPropertyChanged(nameof(Brush));
            OnPropertyChanged(nameof(SoftBrush));
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (Set(ref _isExpanded, value)) OnExpandedChanged();
        }
    }

    public bool IsListShown { get => _isListShown; private set => Set(ref _isListShown, value); }

    public bool IsListCollapsing { get => _isListCollapsing; private set => Set(ref _isListCollapsing, value); }

    private void OnExpandedChanged()
    {
        _hideTimer?.Stop();

        if (_isExpanded)
        {
            IsListCollapsing = false;
            IsListShown = true;
            return;
        }

        IsListCollapsing = true;
        if (_hideTimer == null)
        {
            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
            _hideTimer.Tick += (_, _) =>
            {
                _hideTimer.Stop();
                if (_isExpanded) return;
                IsListShown = false;
                IsListCollapsing = false;
            };
        }
        _hideTimer.Start();
    }
    public bool IsEditing { get => _isEditing; set => Set(ref _isEditing, value); }
    public bool IsPaletteOpen { get => _isPaletteOpen; set => Set(ref _isPaletteOpen, value); }

    public Brush Brush => Make(ColorHex, 255);

    public Brush SoftBrush => Make(ColorHex, ThemeService.IsDark ? (byte)0x55 : (byte)0x8C);

    public RelayCommand ToggleCommand { get; }
    public RelayCommand RenameCommand { get; }
    public RelayCommand SetColorCommand { get; }

    private static Brush Make(string hex, byte alpha)
    {
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            c.A = alpha;
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }
        catch { return Brushes.Transparent; }
    }

    public FolderData ToData() => new()
    {
        Id = Id,
        Name = Name,
        ColorHex = ColorHex,
        IsExpanded = IsExpanded,
        Notes = Notes.Select(n => n.ToData()).ToList()
    };
}
