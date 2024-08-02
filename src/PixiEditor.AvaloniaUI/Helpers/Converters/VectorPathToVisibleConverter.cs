﻿using System.Globalization;
using Avalonia.Data.Converters;
using PixiEditor.DrawingApi.Core.Surfaces.Surface.Vector;

namespace PixiEditor.AvaloniaUI.Helpers.Converters;

internal class VectorPathToVisibleConverter : SingleInstanceConverter<VectorPathToVisibleConverter>
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is VectorPath path)
        {
            return !path.IsEmpty;
        }

        return false;
    }
}
