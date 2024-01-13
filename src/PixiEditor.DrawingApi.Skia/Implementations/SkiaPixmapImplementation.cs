﻿using System;
using PixiEditor.DrawingApi.Core.Bridge.NativeObjectsImpl;
using PixiEditor.DrawingApi.Core.Surface;
using PixiEditor.DrawingApi.Core.Surface.ImageData;
using SkiaSharp;

namespace PixiEditor.DrawingApi.Skia.Implementations
{
    public class SkiaPixmapImplementation : SkObjectImplementation<SKPixmap>, IPixmapImplementation
    {
        private readonly SkiaColorSpaceImplementation colorSpaceImplementation;

        public SkiaPixmapImplementation(SkiaColorSpaceImplementation colorSpaceImplementation)
        {
            this.colorSpaceImplementation = colorSpaceImplementation;
        }

        public void Dispose(IntPtr objectPointer)
        {
            ManagedInstances[objectPointer].Dispose();
            ManagedInstances.TryRemove(objectPointer, out _);
        }

        public IntPtr GetPixels(IntPtr objectPointer)
        {
            return ManagedInstances[objectPointer].GetPixels();
        }

        public Span<T> GetPixelSpan<T>(Pixmap pixmap)
            where T : unmanaged
        {
            return ManagedInstances[pixmap.ObjectPointer].GetPixelSpan<T>();
        }

        public IntPtr Construct(IntPtr dataPtr, ImageInfo imgInfo)
        {
            SKPixmap pixmap = new SKPixmap(imgInfo.ToSkImageInfo(), dataPtr);
            ManagedInstances[pixmap.Handle] = pixmap;
            return pixmap.Handle;
        }

        public int GetWidth(Pixmap pixmap)
        {
            return ManagedInstances[pixmap.ObjectPointer].Width;
        }

        public int GetHeight(Pixmap pixmap)
        {
            return ManagedInstances[pixmap.ObjectPointer].Height;
        }

        public int GetBytesSize(Pixmap pixmap)
        {
            return ManagedInstances[pixmap.ObjectPointer].BytesSize;
        }

        public Pixmap CreateFrom(SKPixmap pixmap)
        {
            ManagedInstances[pixmap.Handle] = pixmap;
            return Pixmap.InternalCreateFromExistingPointer(pixmap.Handle);
        }
    }
}
