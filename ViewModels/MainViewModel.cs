using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using Microsoft.Win32;
using PastelNotes.Infrastructure;
using PastelNotes.Models;
using PastelNotes.Services;

namespace PastelNotes.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly StorageService _storage = new();
    private readonly DialogService _dialogs = new();
    private readonly DispatcherTimer _saveTimer;
    private NoteViewModel? _selectedNote;
    private bool _dirty;
    private bool _cleanupEnabled;
    private bool _isSidebarOpen = true;
    private bool _isDarkMode;

    public MainViewModel()
    {
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _saveTimer.Tick += (_, _) => SaveNow();

        AddFolderCommand = new RelayCommand(AddFolder);
        AddNoteCommand = new RelayCommand(p => { if (p is FolderViewModel f) AddNote(f); });
        DeleteFolderCommand = new RelayCommand(p => { if (p is FolderViewModel f) DeleteFolder(f); });
        DeleteNoteCommand = new RelayCommand(p => { if (p is NoteViewModel n) DeleteNote(n); });
        SelectNoteCommand = new RelayCommand(p => { if (p is NoteViewModel n) SelectedNote = n; });
        OpenFolderCommand = new RelayCommand(p =>
        {
            if (p is not FolderViewModel f) return;
            IsSidebarOpen = true;
            f.IsExpanded = true;
        });
        ExportCommand = new RelayCommand(Export);
        ToggleSidebarCommand = new RelayCommand(() => IsSidebarOpen = !IsSidebarOpen);
        ToggleThemeCommand = new RelayCommand(() => IsDarkMode = !IsDarkMode);

        Editor.ContentChanged += md =>
        {
            if (SelectedNote != null) SelectedNote.Content = md;
        };

        Editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NoteEditorViewModel.IsRawMode) && !Editor.IsRawMode)
                RequestSave();
        };

        LoadData();
    }

    public ObservableCollection<FolderViewModel> Folders { get; } = new();
    public NoteEditorViewModel Editor { get; } = new();

    public RelayCommand AddFolderCommand { get; }
    public RelayCommand AddNoteCommand { get; }
    public RelayCommand DeleteFolderCommand { get; }
    public RelayCommand DeleteNoteCommand { get; }
    public RelayCommand SelectNoteCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand ToggleSidebarCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }

    public event Action? NoteCreated;

    public bool HasSelection => SelectedNote != null;

    public bool IsSidebarOpen
    {
        get => _isSidebarOpen;
        set
        {
            if (Set(ref _isSidebarOpen, value)) RequestSave();
        }
    }

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (!Set(ref _isDarkMode, value)) return;
            ThemeService.Apply(value);
            OnPropertyChanged(nameof(ThemeIcon));
            OnPropertyChanged(nameof(ThemeLabel));
            RequestSave();
        }
    }

    public string ThemeIcon => IsDarkMode ? "\uE706" : "\uE708";
    public string ThemeLabel => IsDarkMode ? "Light mode" : "Dark mode";

    public NoteViewModel? SelectedNote
    {
        get => _selectedNote;
        set
        {
            if (_selectedNote == value) return;
            if (_selectedNote != null) _selectedNote.IsSelected = false;
            _selectedNote = value;
            if (value != null)
            {
                value.IsSelected = true;
                value.Folder.IsExpanded = true;
            }
            Editor.Load(value?.Content ?? "");
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
            RequestSave();
        }
    }

    private void LoadData()
    {
        var loaded = _storage.Load();
        _cleanupEnabled = loaded != null || !File.Exists(_storage.FilePath);
        var data = loaded ?? SeedData.Create();

        _isSidebarOpen = data.IsSidebarOpen;
        _isDarkMode = data.IsDarkMode;
        ThemeService.Apply(_isDarkMode);

        foreach (var fd in data.Folders)
        {
            var folder = new FolderViewModel(fd);
            HookFolder(folder);
            foreach (var nd in fd.Notes)
            {
                var note = new NoteViewModel(folder, nd);
                HookNote(note);
                folder.Notes.Add(note);
            }
            Folders.Add(folder);
        }

        var all = Folders.SelectMany(f => f.Notes).ToList();
        SelectedNote = all.FirstOrDefault(n => n.Id == data.LastNoteId) ?? all.FirstOrDefault();
        _dirty = false;
    }

    private void HookFolder(FolderViewModel f) => f.PropertyChanged += OnItemChanged;
    private void HookNote(NoteViewModel n) => n.PropertyChanged += OnItemChanged;

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(FolderViewModel.Name):
            case nameof(FolderViewModel.ColorHex):
            case nameof(FolderViewModel.IsExpanded):
            case nameof(NoteViewModel.Title):
            case nameof(NoteViewModel.Content):
                RequestSave();
                break;
        }
    }

    private void RequestSave()
    {
        _dirty = true;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void SaveNow()
    {
        _saveTimer.Stop();
        if (!_dirty) return;
        try
        {
            _storage.Save(new AppData
            {
                Folders = Folders.Select(f => f.ToData()).ToList(),
                LastNoteId = SelectedNote?.Id,
                IsSidebarOpen = IsSidebarOpen,
                IsDarkMode = IsDarkMode
            });
            _dirty = false;
            CleanupImages();
        }
        catch (Exception ex)
        {
            _dialogs.Alert("Could not save", $"Failed to save your notes.\n{ex.Message}");
        }
    }

    private void CleanupImages()
    {
        if (!_cleanupEnabled) return;
        if (Editor.IsRawMode) return;

        var used = Folders
            .SelectMany(f => f.Notes)
            .SelectMany(n => ImageService.ExtractReferences(n.Content))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        ImageService.DeleteUnreferenced(used);
    }

    private void AddFolder()
    {
        var folder = new FolderViewModel(new FolderData
        {
            Name = "New folder",
            ColorHex = Palettes.Colors[Folders.Count % Palettes.Colors.Length]
        });
        HookFolder(folder);
        Folders.Add(folder);
        folder.IsEditing = true;
        RequestSave();
    }

    private void AddNote(FolderViewModel folder)
    {
        var note = new NoteViewModel(folder, new NoteData { Title = "New note" });
        HookNote(note);
        folder.IsExpanded = true;
        folder.Notes.Add(note);
        SelectedNote = note;
        RequestSave();
        NoteCreated?.Invoke();
    }

    private void DeleteFolder(FolderViewModel folder)
    {
        var msg = folder.Notes.Count == 0
            ? $"Delete folder \"{folder.Name}\"?"
            : $"Delete folder \"{folder.Name}\" and its {folder.Notes.Count} note(s)? Images used only by these notes will be removed too.";
        if (!_dialogs.Confirm("Delete folder", msg))
            return;

        bool selectedInside = SelectedNote != null && folder.Notes.Contains(SelectedNote);
        Folders.Remove(folder);
        if (selectedInside)
            SelectedNote = Folders.SelectMany(f => f.Notes).FirstOrDefault();
        RequestSave();
    }

    private void DeleteNote(NoteViewModel note)
    {
        if (!_dialogs.Confirm("Delete note", $"Delete note \"{note.Title}\"? Images used only by this note will be removed too."))
            return;

        var folder = note.Folder;
        int index = folder.Notes.IndexOf(note);
        folder.Notes.Remove(note);

        if (SelectedNote == note)
        {
            SelectedNote = folder.Notes.Count > 0
                ? folder.Notes[Math.Min(index, folder.Notes.Count - 1)]
                : Folders.SelectMany(f => f.Notes).FirstOrDefault();
        }
        RequestSave();
    }

    private void Export()
    {
        if (SelectedNote == null) return;

        var safe = string.Concat(SelectedNote.Title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var dlg = new SaveFileDialog
        {
            Filter = "Markdown (*.md)|*.md|Text (*.txt)|*.txt",
            FileName = string.IsNullOrWhiteSpace(safe) ? "note" : safe
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            File.WriteAllText(dlg.FileName, SelectedNote.Content);

            var images = ImageService.ExtractReferences(SelectedNote.Content).Distinct().ToList();
            if (images.Count > 0)
            {
                var target = Path.Combine(Path.GetDirectoryName(dlg.FileName)!, "images");
                foreach (var image in images) ImageService.CopyTo(image, target);
            }
        }
        catch (Exception ex)
        {
            _dialogs.Alert("Could not export", ex.Message);
        }
    }
}
