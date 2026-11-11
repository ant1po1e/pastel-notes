using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace PastelNotes.Models;

public class AppData
{
    public List<FolderData> Folders { get; set; } = new();
    public Guid? LastNoteId { get; set; }
    public bool IsSidebarOpen { get; set; } = true;
}

public class FolderData
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Folder";
    public string ColorHex { get; set; } = "#FFC8DD";
    public bool IsExpanded { get; set; } = true;
    public List<NoteData> Notes { get; set; } = new();
}

public class NoteData
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "New note";
    public string Content { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
