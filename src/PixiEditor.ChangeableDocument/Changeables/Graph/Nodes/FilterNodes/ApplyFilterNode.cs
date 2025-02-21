﻿using PixiEditor.ChangeableDocument.Changeables.Graph.Interfaces;
using PixiEditor.ChangeableDocument.Helpers;
using PixiEditor.ChangeableDocument.Rendering;
using Drawie.Backend.Core;
using Drawie.Backend.Core.Surfaces;
using Drawie.Backend.Core.Surfaces.PaintImpl;
using Drawie.Numerics;

namespace PixiEditor.ChangeableDocument.Changeables.Graph.Nodes.FilterNodes;

[NodeInfo("ApplyFilter")]
public class ApplyFilterNode : RenderNode, IRenderInput
{
    private Paint _paint = new();
    public InputProperty<Filter?> Filter { get; }

    public RenderInputProperty Background { get; }

    public ApplyFilterNode()
    {
        Background = CreateRenderInput("Input", "IMAGE");
        Filter = CreateInput<Filter>("Filter", "FILTER", null);
        Output.FirstInChain = null;
    }

    protected override void OnPaint(RenderContext context, DrawingSurface surface)
    {
        if (Background.Value == null || Filter.Value == null || _paint == null)
            return;

        _paint.SetFilters(Filter.Value);
        int layer = surface.Canvas.SaveLayer(_paint);
        Background.Value.Paint(context, surface);

        surface.Canvas.RestoreToCount(layer);
    }

    public override RectD? GetPreviewBounds(int frame, string elementToRenderName = "")
    {
        return PreviewUtils.FindPreviewBounds(Background.Connection, frame, elementToRenderName);
    }

    public override bool RenderPreview(DrawingSurface renderOn, RenderContext context,
        string elementToRenderName)
    {
        if (Background.Value == null)
            return false;

        int layer = renderOn.Canvas.SaveLayer(_paint);
        Background.Value.Paint(context, renderOn);
        renderOn.Canvas.RestoreToCount(layer);

        return true;
    }

    public override Node CreateCopy() => new ApplyFilterNode();
}
