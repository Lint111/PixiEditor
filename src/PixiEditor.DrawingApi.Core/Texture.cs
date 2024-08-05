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
    public DrawingSurface Surface { get; }

    public event SurfaceChangedEventHandler? Changed;

    public bool IsDisposed { get; private set; }

    private bool pixmapUpToDate;
    private Pixmap pixmap;

    private Paint nearestNeighborReplacingPaint =
        new() { BlendMode = BlendMode.Src, FilterQuality = FilterQuality.None };

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

    public Texture(Texture createFrom)
    {
        Size = createFrom.Size;

        Surface =
            DrawingSurface.Create(
                new ImageInfo(Size.X, Size.Y, ColorType.RgbaF16, AlphaType.Premul, ColorSpace.CreateSrgb())
                {
                    GpuBacked = true
                });

        Surface.Canvas.DrawSurface(createFrom.Surface, 0, 0);
    }

    private void SurfaceOnChanged(RectD? changedRect)
    {
        pixmapUpToDate = false;
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

    public Color GetSRGBPixel(VecI vecI)
    {
        if (vecI.X < 0 || vecI.X >= Size.X || vecI.Y < 0 || vecI.Y >= Size.Y)
            return Color.Empty;

        if (!pixmapUpToDate)
        {
            pixmapUpToDate = true;
            pixmap = Surface.PeekPixels();
        }

        return pixmap.GetPixelColor(vecI);
    }

    public void Dispose()
    {
        if (IsDisposed)
            return;

        IsDisposed = true;
        Surface.Changed -= SurfaceOnChanged;
        Surface.Dispose();
    }

    public Pixmap? PeekReadOnlyPixels()
    {
        if (pixmapUpToDate)
        {
            return pixmap;
        }

        pixmap = Surface.PeekPixels();
        pixmapUpToDate = true;

        return pixmap;
    }

    public void CopyTo(Texture destination)
    {
        destination.Surface.Canvas.DrawSurface(Surface, 0, 0);
    }

    public unsafe bool IsFullyTransparent()
    {
        ulong* ptr = (ulong*)PeekReadOnlyPixels().GetPixels();
        for (int i = 0; i < Size.X * Size.Y; i++)
        {
            // ptr[i] actually contains 4 16-bit floats. We only care about the first one which is alpha.
            // An empty pixel can have alpha of 0 or -0 (not sure if -0 actually ever comes up). 0 in hex is 0x0, -0 in hex is 0x8000
            if ((ptr[i] & 0x1111_0000_0000_0000) != 0 && (ptr[i] & 0x1111_0000_0000_0000) != 0x8000_0000_0000_0000)
                return false;
        }

        return true;
    }

    public void DrawBytes(VecI surfaceSize, byte[] pixels, ColorType color, AlphaType alphaType)
    {
        if (surfaceSize != Size)
            throw new ArgumentException("Surface size must match the size of the byte array");

        using Image image = Image.FromPixels(new ImageInfo(Size.X, Size.Y, color, alphaType, ColorSpace.CreateSrgb()),
            pixels);
        Surface.Canvas.DrawImage(image, 0, 0);
    }

    public Texture ResizeNearestNeighbor(VecI newSize)
    {
        using Image image = Surface.Snapshot();
        Texture newSurface = new(newSize);
        newSurface.Surface.Canvas.DrawImage(image, new RectD(0, 0, newSize.X, newSize.Y),
            nearestNeighborReplacingPaint);
        return newSurface;
    }
}
