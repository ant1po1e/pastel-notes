using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using PastelNotes.Services;
using PastelNotes.ViewModels;

namespace PastelNotes.Views;

public partial class NoteEditorView : UserControl
{
    private readonly DialogService _dialogs = new();
    private readonly DispatcherTimer _dragTimer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private int _dragAnchor = -1;
    private bool _selecting;

    public NoteEditorView()
    {
        InitializeComponent();

        _dragTimer.Tick += (_, _) => UpdateDragSelection(true);

        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is NoteEditorViewModel oldEditor)
            {
                oldEditor.HistoryRestored -= OnHistoryRestored;
                oldEditor.FocusRequested -= OnFocusRequested;
            }
            if (e.NewValue is NoteEditorViewModel newEditor)
            {
                newEditor.HistoryRestored += OnHistoryRestored;
                newEditor.FocusRequested += OnFocusRequested;
            }
        };
    }

    private void OnFocusRequested(BlockViewModel block) => FocusBlock(block, 0);

    private void OnHistoryRestored(int index)
    {
        if (Editor is { } ed && index >= 0 && index < ed.Blocks.Count)
            FocusBlock(ed.Blocks[index]);
    }

    private NoteEditorViewModel? Editor => DataContext as NoteEditorViewModel;

    private void FocusBlock(BlockViewModel block, int caret = -1, int attempt = 0)
    {
        var priority = attempt == 0 ? DispatcherPriority.Loaded : DispatcherPriority.Background;
        Dispatcher.BeginInvoke(priority, new Action(() =>
        {
            BlocksList.UpdateLayout();

            TextBox? tb = null;
            if (BlocksList.ItemContainerGenerator.ContainerFromItem(block) is DependencyObject container)
            {
                var visible = FindAll<TextBox>(container).Where(t => t.IsVisible).ToList();
                tb = visible.FirstOrDefault(t => t.Name == "Input") ?? visible.FirstOrDefault();
            }

            if (tb == null)
            {
                if (attempt < 8) FocusBlock(block, caret, attempt + 1);
                return;
            }

            tb.Focus();
            tb.CaretIndex = caret < 0 ? tb.Text.Length : Math.Min(caret, tb.Text.Length);
            tb.BringIntoView();
        }));
    }

    public void FocusFirstBlock()
    {
        if (Editor is { } ed && ed.Blocks.Count > 0)
            FocusBlock(ed.Blocks[0], 0);
    }

    private static IEnumerable<T> FindAll<T>(DependencyObject parent) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var nested in FindAll<T>(child)) yield return nested;
        }
    }

    private static BlockViewModel? BlockOf(object sender)
        => (sender as FrameworkElement)?.DataContext as BlockViewModel;

    private static void InsertLink(TextBox tb)
    {
        int start = tb.SelectionStart;
        string label = tb.SelectionLength > 0 ? tb.SelectedText : "link text";
        string snippet = $"[{label}](https://)";
        tb.SelectedText = snippet;
        tb.Select(start + snippet.Length - 1, 0);
    }

    private void Block_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (Editor is { } ed && BlockOf(sender) is { } b)
            ed.CurrentBlock = b;
    }

    private void Block_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Editor is not { } ed || sender is not TextBox tb || tb.DataContext is not BlockViewModel b) return;

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
        bool isCode = b.Type == BlockType.CodeBlock;
        bool isImage = b.Type == BlockType.Image;

        if (ed.HasBlockSelection && HandleSelectionKey(ed, key, ctrl))
        {
            e.Handled = true;
            return;
        }

        switch (key)
        {
            case Key.A when ctrl:
                if (tb.Text.Length == 0 || tb.SelectionLength == tb.Text.Length)
                {
                    ed.SelectAll();
                    e.Handled = true;
                }
                break;

            case Key.Z when ctrl:
                if (shift) ed.Redo(); else ed.Undo();
                e.Handled = true;
                break;

            case Key.Y when ctrl:
                ed.Redo();
                e.Handled = true;
                break;

            case Key.Enter:
                if (isCode && !ctrl) return;
                if (isCode) FocusBlock(ed.InsertTextAfter(b), 0);
                else if (ctrl) ed.Toggle(b);
                else if (isImage) FocusBlock(ed.InsertTextAfter(b), 0);
                else HandleEnter(tb, b, ed);
                e.Handled = true;
                break;

            case Key.K when ctrl:
                InsertLink(tb);
                e.Handled = true;
                break;

            case Key.Tab:
                if (isCode)
                {
                    if (!shift)
                    {
                        int position = tb.SelectionStart;
                        tb.SelectedText = "    ";
                        tb.CaretIndex = position + 4;
                    }
                }
                else if (shift) ed.Outdent(b);
                else ed.Indent(b);
                e.Handled = true;
                break;

            case Key.Back:
                if (tb.CaretIndex == 0 && tb.SelectionLength == 0)
                {
                    if (isImage)
                    {
                        if (b.Text.Length == 0)
                        {
                            var previous = ed.PreviousVisible(b);
                            ed.Remove(b);
                            if (previous != null) FocusBlock(previous);
                            e.Handled = true;
                        }
                    }
                    else if (isCode)
                    {
                        if (b.Text.Length == 0)
                        {
                            b.Type = BlockType.Text;
                            FocusBlock(b, 0);
                            e.Handled = true;
                        }
                    }
                    else if (b.Type != BlockType.Text)
                    {
                        b.Type = BlockType.Text;
                        e.Handled = true;
                    }
                    else if (b.Level > 0)
                    {
                        ed.Outdent(b);
                        e.Handled = true;
                    }
                    else
                    {
                        var prev = ed.MergeIntoPrevious(b, out int caret);
                        if (prev != null)
                        {
                            FocusBlock(prev, caret);
                            e.Handled = true;
                        }
                    }
                }
                break;

            case Key.Up when alt:
                ed.MoveUp(b);
                FocusBlock(b, tb.CaretIndex);
                e.Handled = true;
                break;

            case Key.Down when alt:
                ed.MoveDown(b);
                FocusBlock(b, tb.CaretIndex);
                e.Handled = true;
                break;

            case Key.Up when !ctrl && !shift:
                if (tb.GetLineIndexFromCharacterIndex(tb.CaretIndex) <= 0 && ed.PreviousVisible(b) is { } up)
                {
                    FocusBlock(up, tb.CaretIndex);
                    e.Handled = true;
                }
                break;

            case Key.Down when !ctrl && !shift:
                int lastLine = tb.LineCount - 1;
                if (lastLine < 0 || tb.GetLineIndexFromCharacterIndex(tb.CaretIndex) >= lastLine)
                {
                    if (ed.NextVisible(b) is { } down)
                    {
                        FocusBlock(down, tb.CaretIndex);
                        e.Handled = true;
                    }
                }
                break;
        }
    }

    private bool HandleSelectionKey(NoteEditorViewModel ed, Key key, bool ctrl)
    {
        switch (key)
        {
            case Key.LeftCtrl:
            case Key.RightCtrl:
            case Key.LeftShift:
            case Key.RightShift:
            case Key.LeftAlt:
            case Key.RightAlt:
            case Key.System:
                return false;

            case Key.C when ctrl:
                CopyToClipboard(ed.SerializeSelection());
                return true;

            case Key.X when ctrl:
                CopyToClipboard(ed.SerializeSelection());
                RemoveSelection(ed);
                return true;

            case Key.Delete:
            case Key.Back:
                RemoveSelection(ed);
                return true;

            case Key.A when ctrl:
                ed.SelectAll();
                return true;

            case Key.Escape:
                ed.ClearSelection();
                return true;

            case Key.Z when ctrl:
            case Key.Y when ctrl:
                return false;

            default:
                ed.ClearSelection();
                return false;
        }
    }

    private void RemoveSelection(NoteEditorViewModel ed)
    {
        var focus = ed.RemoveSelected();
        if (focus != null) FocusBlock(focus);
    }

    private static void CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch
        {
        }
    }

    private static DependencyObject? ParentOf(DependencyObject element)
        => element is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(element)
            : LogicalTreeHelper.GetParent(element);

    private int AnchorIndexFor(DependencyObject? source)
    {
        if (Editor is not { } ed) return -1;

        while (source != null)
        {
            if (source is FrameworkElement { Name: "Input" or "Display", DataContext: BlockViewModel block })
                return ed.Blocks.IndexOf(block);
            source = ParentOf(source);
        }
        return -1;
    }

    private int BlockIndexAt(double y)
    {
        if (Editor is not { } ed) return -1;

        int lastVisible = -1;
        for (int i = 0; i < ed.Blocks.Count; i++)
        {
            if (!ed.Blocks[i].IsVisible) continue;
            if (BlocksList.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement container || container.ActualHeight <= 0) continue;

            lastVisible = i;
            double top = container.TranslatePoint(new Point(0, 0), BlocksList).Y;
            if (y < top + container.ActualHeight) return i;
        }
        return lastVisible;
    }

    private void UpdateDragSelection(bool fromTimer)
    {
        if (Editor is not { } ed || _dragAnchor < 0) return;

        if (Mouse.LeftButton != MouseButtonState.Pressed)
        {
            EndDragSelection();
            return;
        }

        if (fromTimer && _selecting)
        {
            double y = Mouse.GetPosition(BlocksScroll).Y;
            double edge = 28;
            if (y < edge)
                BlocksScroll.ScrollToVerticalOffset(BlocksScroll.VerticalOffset - Math.Min(40, 6 + (edge - y) / 2));
            else if (y > BlocksScroll.ViewportHeight - edge)
                BlocksScroll.ScrollToVerticalOffset(BlocksScroll.VerticalOffset + Math.Min(40, 6 + (y - (BlocksScroll.ViewportHeight - edge)) / 2));
        }

        int current = BlockIndexAt(Mouse.GetPosition(BlocksList).Y);
        if (current < 0) return;

        if (!_selecting)
        {
            if (current == _dragAnchor) return;

            _selecting = true;
            if (Keyboard.FocusedElement is TextBox focused) focused.Select(focused.CaretIndex, 0);
            Mouse.Capture(BlocksScroll, CaptureMode.SubTree);
            _dragTimer.Start();
        }

        ed.SelectRange(_dragAnchor, current);
    }

    private void EndDragSelection()
    {
        _dragTimer.Stop();
        if (_selecting) Mouse.Capture(null);
        _selecting = false;
        _dragAnchor = -1;
    }

    private void Blocks_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Editor is not { } ed) return;

        ed.ClearSelection();
        _selecting = false;
        _dragAnchor = AnchorIndexFor(e.OriginalSource as DependencyObject);
    }

    private void Blocks_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragAnchor >= 0) UpdateDragSelection(false);
    }

    private void Blocks_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragAnchor >= 0 || _selecting) EndDragSelection();
    }

    private void HandleEnter(TextBox tb, BlockViewModel b, NoteEditorViewModel ed)
    {
        if (string.IsNullOrEmpty(b.Text))
        {
            if (b.Type != BlockType.Text) { b.Type = BlockType.Text; return; }
            if (b.Level > 0) { ed.Outdent(b); return; }
        }

        var next = ed.Split(b, tb.CaretIndex);
        FocusBlock(next, 0);
    }

    private void Block_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        BlocksScroll.ScrollToVerticalOffset(BlocksScroll.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private void Block_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (Editor is not { } ed || sender is not TextBox tb || tb.DataContext is not BlockViewModel b) return;
        if (b.Type == BlockType.CodeBlock) return;
        if (!e.DataObject.GetDataPresent(DataFormats.UnicodeText)) return;

        var text = (e.DataObject.GetData(DataFormats.UnicodeText) as string ?? "")
            .Replace("\r\n", "\n").TrimEnd('\n');
        if (!text.Contains('\n')) return;

        e.CancelCommand();
        var last = ed.PasteMarkdown(b, text);
        if (last != null) FocusBlock(last);
    }

    private void Display_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBlock display) return;

        DependencyObject? element = e.OriginalSource as DependencyObject;
        while (element != null && element != display)
        {
            if (element is Hyperlink) return;
            element = LogicalTreeHelper.GetParent(element);
        }

        if (display.Parent is Grid host && host.Children.OfType<TextBox>().FirstOrDefault() is { } input)
        {
            input.Focus();
            input.CaretIndex = input.Text.Length;
            e.Handled = true;
        }
    }

    private void Cell_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (Editor is { } ed && (sender as FrameworkElement)?.DataContext is CellViewModel cell)
            ed.CurrentBlock = cell.Owner;
    }

    private void Cell_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;

        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        if (e.Key == Key.Enter)
        {
            tb.MoveFocus(new TraversalRequest(FocusNavigationDirection.Down));
            e.Handled = true;
        }
        else if (e.Key == Key.Z && ctrl && Editor is { } undoEditor)
        {
            if (shift) undoEditor.Redo(); else undoEditor.Undo();
            e.Handled = true;
        }
        else if (e.Key == Key.Y && ctrl && Editor is { } redoEditor)
        {
            redoEditor.Redo();
            e.Handled = true;
        }
        else if (e.Key == Key.K && ctrl)
        {
            InsertLink(tb);
            e.Handled = true;
        }
    }

    private void TableAddRow_Click(object sender, RoutedEventArgs e) => BlockOf(sender)?.Table?.AddRow();
    private void TableRemoveRow_Click(object sender, RoutedEventArgs e) => BlockOf(sender)?.Table?.RemoveRow();
    private void TableAddColumn_Click(object sender, RoutedEventArgs e) => BlockOf(sender)?.Table?.AddColumn();
    private void TableRemoveColumn_Click(object sender, RoutedEventArgs e) => BlockOf(sender)?.Table?.RemoveColumn();

    private void TableDelete_Click(object sender, RoutedEventArgs e)
    {
        if (Editor is not { } ed || BlockOf(sender) is not { } block) return;
        if (_dialogs.Confirm("Delete table", "Delete this table and all of its content?"))
            ed.Remove(block);
    }

    private void ImageRemove_Click(object sender, RoutedEventArgs e)
    {
        if (Editor is { } ed && BlockOf(sender) is { } b)
            ed.Remove(b);
    }

    private BlockViewModel? TargetBlock()
    {
        if (Editor is not { } ed) return null;
        var target = ed.CurrentBlock;
        if (target == null || !ed.Blocks.Contains(target)) target = ed.Blocks.LastOrDefault();
        return target;
    }

    private void TypeButton_Click(object sender, RoutedEventArgs e)
    {
        if (Editor is not { } ed || TargetBlock() is not { } target) return;

        if (sender is FrameworkElement { Tag: string tag } && Enum.TryParse<BlockType>(tag, out var type))
        {
            ed.SetType(target, type);
            FocusBlock(target);
        }
    }

    private void TableButton_Click(object sender, RoutedEventArgs e)
    {
        if (Editor is not { } ed) return;
        FocusBlock(ed.InsertTable(TargetBlock()), 0);
    }

    private async void ImageButton_Click(object sender, RoutedEventArgs e)
    {
        if (Editor is not { } ed) return;

        var dialog = new OpenFileDialog
        {
            Title = "Choose an image",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp;*.ico|All files|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        var target = TargetBlock();
        try
        {
            string file = await Task.Run(() => ImageService.Import(dialog.FileName));
            FocusBlock(ed.InsertImage(target, file));
        }
        catch (Exception ex)
        {
            _dialogs.Alert("Could not add image", ex.Message);
        }
    }

    private void LinkButton_Click(object sender, RoutedEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox tb)
            InsertLink(tb);
    }

    private void CollapseAll_Click(object sender, RoutedEventArgs e) => Editor?.SetAllBranches(false);
    private void ExpandAll_Click(object sender, RoutedEventArgs e) => Editor?.SetAllBranches(true);

    private void EndArea_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Editor is not { } ed) return;
        FocusBlock(ed.AppendBlock());
        e.Handled = true;
    }
}
