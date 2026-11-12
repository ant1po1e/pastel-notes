using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using PastelNotes.Services;
using PastelNotes.ViewModels;

namespace PastelNotes.Converters;

public class RenderedModeConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        bool focused = values.Length > 0 && values[0] is true;
        string text = values.Length > 1 ? values[1] as string ?? "" : "";
        bool textual = values.Length > 2 && values[2] is BlockType type &&
            (type == BlockType.Text || type == BlockType.Heading1 || type == BlockType.Heading2 ||
             type == BlockType.Bullet || type == BlockType.Checklist || type == BlockType.Branch);

        bool rendered = !focused && textual && InlineMarkdown.HasMarkup(text);

        if (parameter as string == "opacity") return rendered ? 0.0 : 1.0;
        return rendered ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
