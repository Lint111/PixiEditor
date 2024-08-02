﻿using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Svg.Skia;
using PixiEditor.AvaloniaUI.Helpers.Extensions;
using PixiEditor.DrawingApi.Core.Surfaces;
using Bitmap = Avalonia.Media.Imaging.Bitmap;

namespace PixiEditor.AvaloniaUI.Helpers.Converters;

internal class ImagePathToBitmapConverter : SingleInstanceConverter<ImagePathToBitmapConverter>
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path)
            return AvaloniaProperty.UnsetValue;

        try
        {
            return LoadBitmapFromRelativePath(path);
        }
        catch (FileNotFoundException)
        {
            return AvaloniaProperty.UnsetValue;
        }
    }

    public static Bitmap LoadBitmapFromRelativePath(string path)
    {
        Uri uri = new($"avares://{Assembly.GetExecutingAssembly().FullName}{path}");
        if (!AssetLoader.Exists(uri))
            throw new FileNotFoundException($"Could not find asset with path {path}");

        return new Bitmap(AssetLoader.Open(uri));
    }

    public static Surface.Bitmap LoadDrawingApiBitmapFromRelativePath(string path)
    {
        Uri uri = new($"avares://{Assembly.GetExecutingAssembly().FullName}{path}");
        if (!AssetLoader.Exists(uri))
            throw new FileNotFoundException($"Could not find asset with path {path}");

        return BitmapExtensions.FromStream(AssetLoader.Open(uri));
    }

    public static Bitmap? TryLoadBitmapFromRelativePath(string path)
    {
        Uri uri = new($"avares://{Assembly.GetExecutingAssembly().FullName}{path}");
        return !AssetLoader.Exists(uri) ? null : new Bitmap(AssetLoader.Open(uri));
    }
}
