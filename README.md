# Pastel Notes (WPF / .NET 8)

A pastel-themed desktop note-taking app using the Quicksand font.

## Run
Requirements: Windows 10/11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

    cd PastelNotes
    dotnet run

Or open `PastelNotes.csproj` in Visual Studio 2022 and press F5.

## Features
- Folder sidebar: add, rename, recolor (click the colored dot), delete; add and delete notes
- Collapsible sidebar (Ctrl+B or the arrow / menu buttons)
- Block editor with markdown-style input: headings, bullets, checklists, foldable branches
- Automatic checklist progress on every branch (for example 2/5)
- Markdown mode (edit raw text) and export to .md
- Autosave to `%AppData%\PastelNotes\notes.json`

## Editor shortcuts
| Action | How |
|---|---|
| New line | Enter |
| Heading / subheading | type `# ` / `## ` at the start of a line |
| Bullet / checklist / branch | type `- ` / `[] ` / `> ` at the start of a line |
| Change level (with children) | Tab / Shift+Tab |
| Move line (with children) | Alt+Up / Alt+Down |
| Check item / fold branch | Ctrl+Enter |
| Remove block formatting | Backspace at the start of a line |
| Paste multi-line markdown | Ctrl+V (becomes blocks) |
| Show / hide sidebar | Ctrl+B |

## Storage format (markdown)
    # Heading            ## Subheading
    - bullet             - [ ] todo       - [x] done
    > open branch        >+ collapsed branch
Two spaces of indentation equal one level.
