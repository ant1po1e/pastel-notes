using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PastelNotes.Models;

public static class SeedData
{
    public static AppData Create()
    {
        var welcome = new NoteData
        {
            Title = "Welcome ✿",
            Content = string.Join("\n", new[]
            {
                "# Welcome to Pastel Notes",
                "Every line is freely editable. Press Enter for a new line.",
                "",
                "## Markdown shortcuts",
                "- Type \"# \" or \"## \" at the start of a line for a heading",
                "- Type \"- \" for a bullet, \"[] \" for a checklist item",
                "- Type \"> \" to create a branch you can fold",
                "- Tab / Shift+Tab changes the level, Alt+Up/Down moves a line",
                "- Ctrl+Enter checks a checklist item or folds a branch",
                "- Ctrl+B shows or hides the sidebar",
                "",
                "> This week's tasks",
                "  - [x] Create your first folder",
                "  - [ ] Try checking this item",
                "  - [ ] Change a folder color (click the colored dot in the sidebar)",
                "  > Sub-branch",
                "    - [ ] Branches can be nested too",
            })
        };

        var ideas = new NoteData
        {
            Title = "Shopping & ideas",
            Content = string.Join("\n", new[]
            {
                "## Shopping",
                "- [ ] Vegetables",
                "- [ ] Fruit",
                "",
                "> Project ideas",
                "  - Pastel notes app",
                "  - Habit tracker",
            })
        };

        return new AppData
        {
            Folders =
            {
                new FolderData { Name = "Work", ColorHex = "#FFC8DD", Notes = { welcome } },
                new FolderData { Name = "Personal", ColorHex = "#C7F0D8", Notes = { ideas } },
                new FolderData { Name = "Study", ColorHex = "#CDB4DB" },
            },
            LastNoteId = welcome.Id
        };
    }
}
