﻿using Avalonia;
using ChunkyImageLib.DataHolders;
using Drawie.Backend.Core.Numerics;
using Drawie.Backend.Core.Surfaces;
using Drawie.Interop.Avalonia.Core.Controls;
using PixiEditor.Models.Rendering;
using Drawie.Numerics;

namespace PixiEditor.Views.Visuals;

public class PreviewPainterControl : DrawieControl
{
    public static readonly StyledProperty<int> FrameToRenderProperty =
        AvaloniaProperty.Register<PreviewPainterControl, int>("FrameToRender");

    public static readonly StyledProperty<PreviewPainter> PreviewPainterProperty =
        AvaloniaProperty.Register<PreviewPainterControl, PreviewPainter>(
            nameof(PreviewPainter));

    public PreviewPainter PreviewPainter
    {
        get => GetValue(PreviewPainterProperty);
        set => SetValue(PreviewPainterProperty, value);
    }

    public int FrameToRender
    {
        get { return (int)GetValue(FrameToRenderProperty); }
        set { SetValue(FrameToRenderProperty, value); }
    }

    public PreviewPainterControl()
    {
        PreviewPainterProperty.Changed.Subscribe(PainterChanged);
        BoundsProperty.Changed.Subscribe(UpdatePainterBounds);
    }

    public PreviewPainterControl(PreviewPainter previewPainter, int frameToRender)
    {
        PreviewPainter = previewPainter;
        FrameToRender = frameToRender;
        PreviewPainterProperty.Changed.Subscribe(PainterChanged);
    }


    private void PainterChanged(AvaloniaPropertyChangedEventArgs<PreviewPainter> args)
    {
        if (args.OldValue.Value != null)
        {
            args.OldValue.Value.RequestRepaint -= OnPainterRenderRequest;
        }

        if (args.NewValue.Value != null)
        {
            args.NewValue.Value.RequestRepaint += OnPainterRenderRequest;
        }
    }

    private void OnPainterRenderRequest()
    {
        QueueNextFrame();
    }

    public override void Draw(DrawingSurface surface)
    {
        if (PreviewPainter == null)
        {
            return;
        }

        RectD? previewBounds =
            PreviewPainter.PreviewRenderable.GetPreviewBounds(FrameToRender, PreviewPainter.ElementToRenderName);
        
        float x = (float)(previewBounds?.Width ?? 0);
        float y = (float)(previewBounds?.Height ?? 0);

        surface.Canvas.Save();

        Matrix3X3 matrix = Matrix3X3.Identity;
        if (previewBounds != null)
        {
            matrix = UniformScale(x, y, previewBounds.Value);
        }

        PreviewPainter.Paint(surface, new VecI((int)Bounds.Size.Width, (int)Bounds.Size.Height), matrix);

        surface.Canvas.Restore();
    }

    private Matrix3X3 UniformScale(float x, float y,  RectD previewBounds)
    {
        float scaleX = (float)Bounds.Width / x;
        float scaleY = (float)Bounds.Height / y;
        var scale = Math.Min(scaleX, scaleY);
        float dX = (float)Bounds.Width / 2 / scale - x / 2;
        dX -= (float)previewBounds.X;
        float dY = (float)Bounds.Height / 2 / scale - y / 2;
        dY -= (float)previewBounds.Y;
        Matrix3X3 matrix = Matrix3X3.CreateScale(scale, scale);
        return matrix.Concat(Matrix3X3.CreateTranslation(dX, dY));
    }
    
    private void UpdatePainterBounds(AvaloniaPropertyChangedEventArgs<Rect> args)
    {
        if (PreviewPainter == null)
        {
            return;
        }

        PreviewPainter.SizeToRequest = new VecI((int)Bounds.Size.Width, (int)Bounds.Size.Height);
    }
}
