using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PastelNotes.ViewModels;

namespace PastelNotes;

public partial class MainWindow : Window
{
    private const int RailWidth = 64;
    private const int DwmUseImmersiveDarkMode = 20;

    public static readonly DependencyProperty SidebarWidthProperty = DependencyProperty.Register(
        nameof(SidebarWidth), typeof(double), typeof(MainWindow),
        new PropertyMetadata(310.0, (d, e) => ((MainWindow)d).SidebarColumn.Width = new GridLength((double)e.NewValue)));

    private readonly MainViewModel _vm = new();
    private GridLength _sidebarWidth = new(310);

    public double SidebarWidth
    {
        get => (double)GetValue(SidebarWidthProperty);
        set => SetValue(SidebarWidthProperty, value);
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsSidebarOpen)) ApplySidebar(true);
            if (e.PropertyName == nameof(MainViewModel.IsDarkMode)) ApplyTitleBar();
        };
        ApplySidebar(false);

        SourceInitialized += (_, _) => ApplyTitleBar();

        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized) TrimMemory();
        };

        _vm.NoteCreated += () => Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            TitleBox.Focus();
            TitleBox.SelectAll();
        }));
    }

    [DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr minimum, IntPtr maximum);

    private static void TrimMemory()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, new IntPtr(-1), new IntPtr(-1));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private void ApplyTitleBar()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        int value = _vm.IsDarkMode ? 1 : 0;
        DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref value, sizeof(int));
    }

    private void ApplySidebar(bool animate)
    {
        bool open = _vm.IsSidebarOpen;
        double current = SidebarColumn.ActualWidth > 0
            ? SidebarColumn.ActualWidth
            : (open ? _sidebarWidth.Value : RailWidth);

        if (!open && current > RailWidth + 20)
            _sidebarWidth = new GridLength(current);

        double target = open ? _sidebarWidth.Value : RailWidth;
        Splitter.Visibility = open ? Visibility.Visible : Visibility.Collapsed;

        BeginAnimation(SidebarWidthProperty, null);
        SidebarColumn.MinWidth = 0;
        SidebarColumn.MaxWidth = 480;

        if (!animate)
        {
            FinishSidebar(open, target);
            return;
        }

        SidebarWidth = current;
        var animation = new DoubleAnimation(current, target, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        animation.Completed += (_, _) =>
        {
            SidebarWidth = target;
            BeginAnimation(SidebarWidthProperty, null);
            FinishSidebar(open, target);
        };
        BeginAnimation(SidebarWidthProperty, animation);
    }

    private void FinishSidebar(bool open, double target)
    {
        SidebarWidth = target;
        SidebarColumn.Width = new GridLength(target);
        SidebarColumn.MaxWidth = open ? 480 : RailWidth;
        SidebarColumn.MinWidth = open ? 230 : RailWidth;
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

    private void Palette_Opened(object? sender, EventArgs e)
    {
        if (sender is not Popup { Child: Border card }) return;

        var scale = new ScaleTransform(0.8, 0.8);
        card.RenderTransformOrigin = new Point(0.15, 0);
        card.RenderTransform = scale;

        var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };
        var duration = TimeSpan.FromMilliseconds(180);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.8, 1, duration) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.8, 1, duration) { EasingFunction = ease });
        card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
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
