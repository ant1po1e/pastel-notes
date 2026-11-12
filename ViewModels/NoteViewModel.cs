using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using PastelNotes.Infrastructure;
using PastelNotes.Models;

namespace PastelNotes.ViewModels;

public class NoteViewModel : ObservableObject
{
    private static readonly CultureInfo UiCulture = CultureInfo.InvariantCulture;

    private string _title;
    private string _content;
    private DateTime _updatedAt;
    private bool _isSelected;

    public NoteViewModel(FolderViewModel folder, NoteData data)
    {
        Folder = folder;
        Id = data.Id;
        _title = data.Title;
        _content = data.Content;
        _updatedAt = data.UpdatedAt;
    }

    public Guid Id { get; }
    public FolderViewModel Folder { get; }

    public string Title
    {
        get => _title;
        set { if (Set(ref _title, value ?? "")) Touch(); }
    }

    public string Content
    {
        get => _content;
        set { if (Set(ref _content, value ?? "")) Touch(); }
    }

    public DateTime UpdatedAt => _updatedAt;
    public string UpdatedText => _updatedAt.ToString("d MMM yyyy, HH:mm", UiCulture);

    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

    private void Touch()
    {
        _updatedAt = DateTime.Now;
        OnPropertyChanged(nameof(UpdatedAt));
        OnPropertyChanged(nameof(UpdatedText));
    }

    public NoteData ToData() => new()
    {
        Id = Id,
        Title = Title,
        Content = Content,
        UpdatedAt = UpdatedAt
    };
}
