using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PastelNotes.ViewModels;

namespace PastelNotes.Views;

public partial class NoteEditorView : UserControl
{
    public NoteEditorView()
    {
        InitializeComponent();
    }

    private NoteEditorViewModel? Editor => DataContext as NoteEditorViewModel;

    private void FocusBlock(BlockViewModel block, int caret = -1)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            BlocksList.UpdateLayout();
            if (BlocksList.ItemContainerGenerator.ContainerFromItem(block) is not DependencyObject container) return;
            var tb = FindChild<TextBox>(container);
            if (tb == null) return;

            tb.Focus();
            tb.CaretIndex = caret < 0 ? tb.Text.Length : Math.Min(caret, tb.Text.Length);
            tb.BringIntoView();
        }));
    }

    public void FocusFirstBlock()
    {
        if (Editor is { } ed && ed.Blocks.Count > 0)
        {
            var first = ed.Blocks[0];
            FocusBlock(first, 0);
        }
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            var nested = FindChild<T>(child);
            if (nested != null) return nested;
        }
        return null;
    }

    private void Block_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (Editor is { } ed && sender is FrameworkElement { DataContext: BlockViewModel b })
            ed.CurrentBlock = b;
    }

    private void Block_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Editor is not { } ed || sender is not TextBox tb || tb.DataContext is not BlockViewModel b) return;

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;

        switch (key)
        {
            case Key.Enter:
                if (ctrl) ed.Toggle(b);
                else HandleEnter(tb, b, ed);
                e.Handled = true;
                break;

            case Key.Tab:
                if (shift) ed.Outdent(b); else ed.Indent(b);
                e.Handled = true;
                break;

            case Key.Back:
                if (tb.CaretIndex == 0 && tb.SelectionLength == 0)
                {
                    if (b.Type != BlockType.Text)
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
        if (!e.DataObject.GetDataPresent(DataFormats.UnicodeText)) return;

        var text = (e.DataObject.GetData(DataFormats.UnicodeText) as string ?? "")
            .Replace("\r\n", "\n").TrimEnd('\n');
        if (!text.Contains('\n')) return;

        e.CancelCommand();
        var last = ed.PasteMarkdown(b, text);
        if (last != null) FocusBlock(last);
    }

    private void TypeButton_Click(object sender, RoutedEventArgs e)
    {
        if (Editor is not { } ed) return;

        var target = ed.CurrentBlock;
        if (target == null || !ed.Blocks.Contains(target)) target = ed.Blocks.LastOrDefault();
        if (target == null) return;

        if (sender is FrameworkElement { Tag: string tag } && Enum.TryParse<BlockType>(tag, out var type))
        {
            ed.SetType(target, type);
            FocusBlock(target);
        }
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
