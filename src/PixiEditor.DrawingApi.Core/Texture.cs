﻿using System;
using System.IO;
using PixiEditor.DrawingApi.Core.ColorsImpl;
using PixiEditor.DrawingApi.Core.Surfaces;
using PixiEditor.DrawingApi.Core.Surfaces.ImageData;
using PixiEditor.DrawingApi.Core.Surfaces.PaintImpl;
using PixiEditor.Numerics;

namespace PixiEditor.DrawingApi.Core;

public class Texture : IDisposable
{
    public VecI Size { get; }
    public DrawingSurface Surface { get; private set; }

    public event SurfaceChangedEventHandler? Changed;

    public bool IsDisposed { get; private set; }

    private bool pixmapUpToDate;
    private Pixmap pixmap;

    public Texture(VecI size)
    {
        Size = size;
        Surface =
            DrawingSurface.Create(
                new ImageInfo(Size.X, Size.Y, ColorType.RgbaF16, AlphaType.Premul, ColorSpace.CreateSrgb())
                {
                    GpuBacked = true
                });

        Surface.Changed += SurfaceOnChanged;
    }

    internal Texture(DrawingSurface surface)
    {
        Surface = surface;
        Surface.Changed += SurfaceOnChanged;
    }
    
    ~Texture()
    {
       Surface.Changed -= SurfaceOnChanged;
    }

    private void SurfaceOnChanged(RectD? changedRect)
    {
        Changed?.Invoke(changedRect);
    }


    public static Texture Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(null, path);
        using var image = Image.FromEncodedData(path);
        if (image is null)
            throw new ArgumentException($"The image with path {path} couldn't be loaded");

        Texture texture = new Texture(image.Size);
        texture.Surface.Canvas.DrawImage(image, 0, 0);

        return texture;
    }

    public static Texture Load(byte[] data)
    {
        using Image image = Image.FromEncodedData(data);
        Texture texture = new Texture(image.Size);
        texture.Surface.Canvas.DrawImage(image, 0, 0);

        return texture;
    }

    public static Texture? Load(byte[] encoded, ColorType colorType, VecI imageSize)
    {
        using var image = Image.FromPixels(new ImageInfo(imageSize.X, imageSize.Y, colorType), encoded);
        if (image is null)
            return null;

        var surface = new Texture(new VecI(image.Width, image.Height));
        surface.Surface.Canvas.DrawImage(image, 0, 0);

        return surface;
    }

    public Texture CreateResized(VecI newSize, ResizeMethod method)
    {
        using Image image = Surface.Snapshot();
        Texture newTexture = new(newSize);
        using Paint paint = new();

        FilterQuality filterQuality = method switch
        {
            ResizeMethod.HighQuality => FilterQuality.High,
            ResizeMethod.MediumQuality => FilterQuality.Medium,
            ResizeMethod.LowQuality => FilterQuality.Low,
            _ => FilterQuality.None
        };

        paint.FilterQuality = filterQuality;

        newTexture.Surface.Canvas.DrawImage(image, new RectD(0, 0, newSize.X, newSize.Y), paint);

        return newTexture;
    }

    public Color? GetSRGBPixel(VecI vecI)
    {
        if (vecI.X < 0 || vecI.X >= Size.X || vecI.Y < 0 || vecI.Y >= Size.Y)
            return null;

        if (!pixmapUpToDate)
        {
            pixmapUpToDate = true;
            pixmap = Surface.PeekPixels();
        }

        return pixmap.GetPixelColor(vecI);
    }

    public void AddDirtyRect(RectI dirtyRect)
    {
        Changed?.Invoke(new RectD(dirtyRect.X, dirtyRect.Y, dirtyRect.Width, dirtyRect.Height));
    }

    public void Dispose()
    {
        if (IsDisposed)
            return;

        IsDisposed = true;
        Surface.Changed -= SurfaceOnChanged;
        Surface.Dispose();
    }

    public static Texture FromExisting(DrawingSurface drawingSurface)
    {
        Texture texture = new(drawingSurface);
        return texture;
    }
}
