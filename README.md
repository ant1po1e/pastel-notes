# Pastel Notes (WPF / .NET 8)

A pastel-themed desktop note-taking app using the Quicksand font, with light and dark modes.

## Run
Requirements: Windows 10/11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

    cd PastelNotes
    dotnet run

Or open `PastelNotes.csproj` in Visual Studio 2022 and press F5. NuGet restores SkiaSharp automatically.

## Memory notes
- Run a Release build outside the debugger (Ctrl+F5 in Visual Studio) to see the real memory use; the debugger, Hot Reload and the Live Visual Tree add well over 100 MB on their own
- Images are decoded at a reduced size and only the 16 most recently used are kept in memory
- The runtime is configured for low memory (workstation non-concurrent GC with conserve-memory enabled), and memory is trimmed when the window is minimized

## Features
- Folder sidebar: add, rename, recolor (click the colored dot), delete; add and delete notes
- Sidebar collapses into a slim rail (Ctrl+B) with folder shortcuts; the dark mode button stays at the bottom in both states
- Light / dark mode (remembered between launches)
- Block editor with markdown-style input: headings, bullets, checklists, foldable branches
- Fenced code blocks with a language label
- Tables (editable grid with add / remove row and column)
- Inline formatting rendered when a line is not focused: links, **bold**, *italic*, ~~strikethrough~~, `code`, bare URLs
- Images: choose a file, it is converted to WebP and stored in `%AppData%\PastelNotes\images`. Removing the image from every note deletes the file
- Markdown mode (edit raw text) and export to .md (referenced images are copied to an `images` folder next to the export)
- Indent guides (tree lines) appear when you indent a line with Tab
- Smooth animations: folder and branch fold / unfold, sidebar collapse, color popup, button hover, dialogs
- Autosave to `%AppData%\PastelNotes\notes.json`

## Editor shortcuts
| Action | How |
|---|---|
| New line | Enter |
| Heading / subheading | type `# ` / `## ` at the start of a line |
| Bullet / checklist / branch | type `- ` / `[] ` / `> ` at the start of a line |
| Code block | type ` ``` ` at the start of a line (Tab inserts 4 spaces, Ctrl+Enter exits) |
| Insert link | Ctrl+K or the Link toolbar icon |
| Change level (with children) | Tab / Shift+Tab |
| Move line (with children) | Alt+Up / Alt+Down |
| Check item / fold branch | Ctrl+Enter |
| Remove block formatting | Backspace at the start of a line |
| Delete a table | trash icon under the table (asks for confirmation) |
| Remove an image | Backspace in an empty caption, or the X button on hover |
| Select all text, then all blocks | Ctrl+A (press twice); or drag the mouse across blocks |
| Copy / cut / delete selected blocks | Ctrl+C / Ctrl+X / Delete |
| Undo / redo | Ctrl+Z / Ctrl+Y (or Ctrl+Shift+Z) |
| Paste multi-line markdown | Ctrl+V (becomes blocks) |
| Show / hide sidebar | Ctrl+B |

## Storage format (markdown)
    # Heading            ## Subheading
    - bullet             - [ ] todo       - [x] done
    > open branch        >+ collapsed branch
    ```lang              ![caption](images/<id>.webp)
    code                 | A | B |
    ```                  | --- | --- |
Two spaces of indentation equal one level.
