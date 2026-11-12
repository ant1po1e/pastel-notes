using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace PastelNotes.Services;

public static class ThemeService
{
    public static bool IsDark { get; private set; }

    public static event Action? Changed;

    public static void Apply(bool dark)
    {
        IsDark = dark;

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var replacement = new ResourceDictionary
        {
            Source = new Uri(dark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml", UriKind.Relative)
        };

        int index = -1;
        for (int i = 0; i < dictionaries.Count; i++)
        {
            var source = dictionaries[i].Source?.OriginalString;
            if (source != null && source.EndsWith("Theme.xaml", StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        if (index >= 0) dictionaries[index] = replacement;
        else dictionaries.Insert(0, replacement);

        Changed?.Invoke();
    }
}
