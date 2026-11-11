using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using PastelNotes.Views;

namespace PastelNotes.Services;

public class DialogService
{
    private static Window? Owner => Application.Current?.MainWindow;

    public bool Confirm(string title, string message, string confirmText = "Delete", string cancelText = "Cancel", bool danger = true)
    {
        var dialog = new MessageDialog(title, message, confirmText, cancelText, danger ? "\uE74D" : "\uE897", danger);
        if (Owner is { IsLoaded: true } owner) dialog.Owner = owner;
        return dialog.ShowDialog() == true;
    }

    public void Alert(string title, string message, string okText = "OK")
    {
        var dialog = new MessageDialog(title, message, okText, null, "\uE783", false);
        if (Owner is { IsLoaded: true } owner) dialog.Owner = owner;
        dialog.ShowDialog();
    }
}
