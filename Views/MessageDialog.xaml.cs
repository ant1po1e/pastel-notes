using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PastelNotes.Views;

public partial class MessageDialog : Window
{
    public MessageDialog(string title, string message, string confirmText, string? cancelText, string glyph, bool danger)
    {
        InitializeComponent();

        TitleText.Text = title;
        MessageText.Text = message;
        IconText.Text = glyph;
        ConfirmButton.Content = confirmText;

        if (cancelText == null)
        {
            CancelButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            CancelButton.Content = cancelText;
        }

        if (danger)
        {
            ConfirmButton.Background = Brush("#FFB5A7");
            IconBadge.Background = Brush("#FFE3DC");
        }
        else
        {
            ConfirmButton.Background = Brush("#FFC8DD");
        }
    }

    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            DialogResult = true;
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            DialogResult = CancelButton.Visibility == Visibility.Visible ? false : true;
            e.Handled = true;
        }
    }

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
