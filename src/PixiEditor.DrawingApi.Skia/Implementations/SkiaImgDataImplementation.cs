﻿using System;
using System.Collections.Generic;
using System.IO;
using PixiEditor.DrawingApi.Core.Bridge.NativeObjectsImpl;
using PixiEditor.DrawingApi.Core.Surfaces.ImageData;
using SkiaSharp;

namespace PixiEditor.DrawingApi.Skia.Implementations
{
    public sealed class SkiaImgDataImplementation : SkObjectImplementation<SKData>, IImgDataImplementation
    {
        public void Dispose(IntPtr objectPointer)
        {
            if (ManagedInstances.ContainsKey(objectPointer))
            {
                ManagedInstances[objectPointer].Dispose();
                ManagedInstances.TryRemove(objectPointer, out _);
            }
        }

        public void SaveTo(ImgData imgData, FileStream stream)
        {
            SKData data = ManagedInstances[imgData.ObjectPointer];
            data.SaveTo(stream);
        }

        public Stream AsStream(ImgData imgData)
        {
            SKData data = ManagedInstances[imgData.ObjectPointer];
            return data.AsStream();
        }

        public ReadOnlySpan<byte> AsSpan(ImgData imgData)
        {
            SKData data = ManagedInstances[imgData.ObjectPointer];
            return data.AsSpan();
        }

        public object GetNativeImgData(IntPtr objectPointer)
        {
            return ManagedInstances[objectPointer];
        }
    }
}
