﻿using PixiEditor.ChangeableDocument.Changeables.Animations;
using PixiEditor.ChangeableDocument.Changeables.Graph.Interfaces;
using PixiEditor.ChangeableDocument.Changeables.Interfaces;
using PixiEditor.DrawingApi.Core.ColorsImpl;
using PixiEditor.DrawingApi.Core.Surfaces.PaintImpl;
using PixiEditor.Numerics;
using BlendMode = PixiEditor.ChangeableDocument.Enums.BlendMode;
using DrawingApiBlendMode = PixiEditor.DrawingApi.Core.Surfaces.BlendMode;

namespace PixiEditor.ChangeableDocument.Rendering;

public class RenderingContext : IDisposable
{
    public Paint BlendModePaint = new() { BlendMode = DrawingApiBlendMode.SrcOver };
    public Paint BlendModeOpacityPaint = new() { BlendMode = DrawingApiBlendMode.SrcOver };
    public Paint ReplacingPaintWithOpacity = new() { BlendMode = DrawingApiBlendMode.Src };

    public KeyFrameTime FrameTime { get; }
    public VecI ChunkToUpdate { get; }
    public ChunkResolution ChunkResolution { get; }
    public VecI DocumentSize { get; set; }

    public bool IsDisposed { get; private set; }
    
    public RenderingContext(KeyFrameTime frameTime, VecI chunkToUpdate, ChunkResolution chunkResolution, VecI docSize)
    {
        FrameTime = frameTime;
        ChunkToUpdate = chunkToUpdate;
        ChunkResolution = chunkResolution;
        DocumentSize = docSize;
    }

    public static DrawingApiBlendMode GetDrawingBlendMode(BlendMode blendMode)
    {
        return blendMode switch
        {
            BlendMode.Normal => DrawingApiBlendMode.SrcOver,
            BlendMode.Darken => DrawingApiBlendMode.Darken,
            BlendMode.Multiply => DrawingApiBlendMode.Multiply,
            BlendMode.ColorBurn => DrawingApiBlendMode.ColorBurn,
            BlendMode.Lighten => DrawingApiBlendMode.Lighten,
            BlendMode.Screen => DrawingApiBlendMode.Screen,
            BlendMode.ColorDodge => DrawingApiBlendMode.ColorDodge,
            BlendMode.LinearDodge => DrawingApiBlendMode.Plus,
            BlendMode.Overlay => DrawingApiBlendMode.Overlay,
            BlendMode.SoftLight => DrawingApiBlendMode.SoftLight,
            BlendMode.HardLight => DrawingApiBlendMode.HardLight,
            BlendMode.Difference => DrawingApiBlendMode.Difference,
            BlendMode.Exclusion => DrawingApiBlendMode.Exclusion,
            BlendMode.Hue => DrawingApiBlendMode.Hue,
            BlendMode.Saturation => DrawingApiBlendMode.Saturation,
            BlendMode.Luminosity => DrawingApiBlendMode.Luminosity,
            BlendMode.Color => DrawingApiBlendMode.Color,
            _ => DrawingApiBlendMode.SrcOver,
        };
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }
        
        IsDisposed = true;
        BlendModePaint.Dispose();
        BlendModeOpacityPaint.Dispose();
        ReplacingPaintWithOpacity.Dispose();
    }
}
