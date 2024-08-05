﻿using PixiEditor.ChangeableDocument.Rendering;
using PixiEditor.DrawingApi.Core;
using PixiEditor.DrawingApi.Core.Surfaces;
using PixiEditor.DrawingApi.Core.Surfaces.PaintImpl;
using PixiEditor.Numerics;

namespace PixiEditor.ChangeableDocument.Changeables.Graph.Nodes.CombineSeparate;

[NodeInfo("CombineChannels")]
public class CombineChannelsNode : Node
{
    private readonly Paint _screenPaint = new() { BlendMode = BlendMode.Screen };
    private readonly Paint _clearPaint = new() { BlendMode = BlendMode.DstIn };
    
    private readonly ColorFilter _redFilter = ColorFilter.CreateColorMatrix(ColorMatrix.UseRed + ColorMatrix.OpaqueAlphaOffset);
    private readonly ColorFilter _greenFilter = ColorFilter.CreateColorMatrix(ColorMatrix.UseGreen + ColorMatrix.OpaqueAlphaOffset);
    private readonly ColorFilter _blueFilter = ColorFilter.CreateColorMatrix(ColorMatrix.UseBlue + ColorMatrix.OpaqueAlphaOffset);

    public InputProperty<Texture> Red { get; }
    
    public InputProperty<Texture> Green { get; }
    
    public InputProperty<Texture> Blue { get; }
    
    public InputProperty<Texture> Alpha { get; }

    public OutputProperty<Texture> Image { get; }
    
    // TODO: Either use a shader to combine each, or find a way to automatically "detect" if alpha channel is grayscale or not, oooor find an even better solution
    public InputProperty<bool> Grayscale { get; }

    public CombineChannelsNode()
    {
        Red = CreateInput<Texture>(nameof(Red), "RED", null);
        Green = CreateInput<Texture>(nameof(Green), "GREEN", null);
        Blue = CreateInput<Texture>(nameof(Blue), "BLUE", null);
        Alpha = CreateInput<Texture>(nameof(Alpha), "ALPHA", null);
        
        Image = CreateOutput<Texture>(nameof(Image), "IMAGE", null);
        Grayscale = CreateInput(nameof(Grayscale), "GRAYSCALE", false);
    }

    protected override Texture? OnExecute(RenderingContext context)
    {
        var size = GetSize();

        if (size == VecI.Zero)
            return null;
        
        var workingSurface = new Texture(size);

        if (Red.Value is { } red)
        {
            _screenPaint.ColorFilter = _redFilter;
            workingSurface.Surface.Canvas.DrawSurface(red.Surface, 0, 0, _screenPaint);
        }

        if (Green.Value is { } green)
        {
            _screenPaint.ColorFilter = _greenFilter;
            workingSurface.Surface.Canvas.DrawSurface(green.Surface, 0, 0, _screenPaint);
        }

        if (Blue.Value is { } blue)
        {
            _screenPaint.ColorFilter = _blueFilter;
            workingSurface.Surface.Canvas.DrawSurface(blue.Surface, 0, 0, _screenPaint);
        }

        if (Alpha.Value is { } alpha)
        {
            _clearPaint.ColorFilter = Grayscale.Value ? Filters.AlphaGrayscaleFilter : null;

            workingSurface.Surface.Canvas.DrawSurface(alpha.Surface, 0, 0, _clearPaint);
        }

        Image.Value = workingSurface;

        return workingSurface;
    }

    private VecI GetSize()
    {
        var final = new RectI();

        if (Red.Value is { } red)
        {
            final = final.Union(new RectI(VecI.Zero, red.Size));
        }

        if (Green.Value is { } green)
        {
            final = final.Union(new RectI(VecI.Zero, green.Size));
        }

        if (Blue.Value is { } blue)
        {
            final = final.Union(new RectI(VecI.Zero, blue.Size));
        }

        if (Alpha.Value is { } alpha)
        {
            final = final.Union(new RectI(VecI.Zero, alpha.Size));
        }

        return final.Size;
    }

    public override string DisplayName { get; set; } = "COMBINE_CHANNELS_NODE";

    public override Node CreateCopy() => new CombineChannelsNode();
}
