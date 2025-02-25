﻿using PixiEditor.ChangeableDocument.Rendering;
using Drawie.Backend.Core;
using Drawie.Backend.Core.Bridge;
using Drawie.Backend.Core.ColorsImpl;
using Drawie.Backend.Core.Surfaces;
using Drawie.Backend.Core.Surfaces.ImageData;
using Drawie.Numerics;
using PixiEditor.ChangeableDocument.Changeables.Graph.Interfaces;

namespace PixiEditor.ChangeableDocument.Changeables.Graph.Nodes;

[NodeInfo("CreateImage")]
public class CreateImageNode : Node, IPreviewRenderable
{
    public OutputProperty<Texture> Output { get; }

    public InputProperty<VecI> Size { get; }

    public InputProperty<Color> Fill { get; }

    public RenderInputProperty Content { get; }

    public InputProperty<VecD> ContentOffset { get; }

    public RenderOutputProperty RenderOutput { get; }

    private TextureCache textureCache = new();

    public CreateImageNode()
    {
        Output = CreateOutput<Texture>(nameof(Output), "IMAGE", null);
        Size = CreateInput(nameof(Size), "SIZE", new VecI(32, 32)).WithRules(v => v.Min(VecI.One));
        Fill = CreateInput(nameof(Fill), "FILL", Colors.Transparent);
        Content = CreateRenderInput(nameof(Content), "CONTENT");
        ContentOffset = CreateInput(nameof(ContentOffset), "CONTENT_OFFSET", VecD.Zero);
        RenderOutput = CreateRenderOutput("RenderOutput", "RENDER_OUTPUT", () => new Painter(OnPaint));
    }

    protected override void OnExecute(RenderContext context)
    {
        if (Size.Value.X <= 0 || Size.Value.Y <= 0)
        {
            return;
        }

        var surface = Render(context);

        Output.Value = surface;

        RenderOutput.ChainToPainterValue();
    }

    private Texture Render(RenderContext context)
    {
        var surface = textureCache.RequestTexture(0, Size.Value, context.ProcessingColorSpace, false);

        surface.DrawingSurface.Canvas.Clear(Fill.Value);

        int saved = surface.DrawingSurface.Canvas.Save();

        RenderContext ctx = new RenderContext(surface.DrawingSurface, context.FrameTime, context.ChunkResolution,
            context.DocumentSize, context.ProcessingColorSpace);

        surface.DrawingSurface.Canvas.Translate((float)-ContentOffset.Value.X, (float)-ContentOffset.Value.Y);

        Content.Value?.Paint(ctx, surface.DrawingSurface);

        surface.DrawingSurface.Canvas.RestoreToCount(saved);
        return surface;
    }

    private void OnPaint(RenderContext context, DrawingSurface surface)
    {
        if(Output.Value == null || Output.Value.IsDisposed) return;
        
        surface.Canvas.DrawSurface(Output.Value.DrawingSurface, 0, 0);
    }

    public override Node CreateCopy() => new CreateImageNode();

    public override void Dispose()
    {
        base.Dispose();
        textureCache.Dispose();
    }

    public RectD? GetPreviewBounds(int frame, string elementToRenderName = "")
    {
        if (Size.Value.X <= 0 || Size.Value.Y <= 0)
        {
            return null;
        }

        return new RectD(0, 0, Size.Value.X, Size.Value.Y);
    }

    public bool RenderPreview(DrawingSurface renderOn, RenderContext context, string elementToRenderName)
    {
        if (Size.Value.X <= 0 || Size.Value.Y <= 0)
        {
            return false;
        }

        if (Output.Value == null)
        {
            return false;
        }

        var surface = Render(context);
        
        if (surface == null || surface.IsDisposed)
        {
            return false;
        }
        
        renderOn.Canvas.DrawSurface(surface.DrawingSurface, 0, 0);
        
        return true;
    }
}
