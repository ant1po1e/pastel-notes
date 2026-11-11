using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using PastelNotes.ViewModels;

namespace PastelNotes;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private GridLength _sidebarWidth = new(310);

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsSidebarOpen)) ApplySidebar();
        };
        ApplySidebar();

        _vm.NoteCreated += () => Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            TitleBox.Focus();
            TitleBox.SelectAll();
        }));
    }

    private void ApplySidebar()
    {
        if (_vm.IsSidebarOpen)
        {
            SidebarColumn.MinWidth = 230;
            SidebarColumn.Width = _sidebarWidth;
            Splitter.Visibility = Visibility.Visible;
        }
        else
        {
            if (SidebarColumn.ActualWidth > 0) _sidebarWidth = new GridLength(SidebarColumn.ActualWidth);
            SidebarColumn.MinWidth = 0;
            SidebarColumn.Width = new GridLength(0);
            Splitter.Visibility = Visibility.Collapsed;
        }
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e) => _vm.SaveNow();

    private void TitleBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            EditorHost.FocusFirstBlock();
            e.Handled = true;
        }
    }

    private void FolderName_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FolderViewModel f })
            f.IsExpanded = !f.IsExpanded;
    }

    private void NameBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.IsVisible)
        {
            tb.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                tb.Focus();
                tb.SelectAll();
            }));
        }
    }

    private void NameBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: FolderViewModel f })
        {
            if (string.IsNullOrWhiteSpace(f.Name)) f.Name = "Folder";
            f.IsEditing = false;
        }
    }

    private void NameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Escape)
        {
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }
}
