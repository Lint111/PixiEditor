﻿using ChunkyImageLib.DataHolders;
using Drawie.Backend.Core;
using Drawie.Backend.Core.Numerics;
using PixiEditor.ChangeableDocument.Changeables.Interfaces;
using PixiEditor.ChangeableDocument.Rendering;
using Drawie.Backend.Core.Surfaces;
using Drawie.Backend.Core.Surfaces.ImageData;
using Drawie.Numerics;
using PixiEditor.ChangeableDocument.Changeables.Animations;
using PixiEditor.ChangeableDocument.Changeables.Graph;
using PixiEditor.ChangeableDocument.Changeables.Graph.Interfaces;
using PixiEditor.ChangeableDocument.Changeables.Graph.Nodes;
using PixiEditor.Models.Handlers;

namespace PixiEditor.Models.Rendering;

internal class SceneRenderer
{
    public const double ZoomDiffToRerender = 20;
    public IReadOnlyDocument Document { get; }
    public IDocument DocumentViewModel { get; }
    public bool HighResRendering { get; set; } = true;

    private Dictionary<string, Texture> cachedTextures = new();
    private bool lastHighResRendering = true;
    private int lastGraphCacheHash = -1;
    private KeyFrameTime lastFrameTime;
    private Dictionary<Guid, bool> lastFramesVisibility = new();

    public SceneRenderer(IReadOnlyDocument trackerDocument, IDocument documentViewModel)
    {
        Document = trackerDocument;
        DocumentViewModel = documentViewModel;
    }

    public void RenderScene(DrawingSurface target, ChunkResolution resolution, string? targetOutput = null)
    {
        if (Document.Renderer.IsBusy || DocumentViewModel.Busy) return;
        RenderOnionSkin(target, resolution, targetOutput);

        string adjustedTargetOutput = targetOutput ?? "";

        IReadOnlyNodeGraph finalGraph = SolveFinalNodeGraph(targetOutput);
        bool shouldRerender = ShouldRerender(target, resolution, adjustedTargetOutput, finalGraph);
        if (shouldRerender)
        {
            if (cachedTextures.ContainsKey(adjustedTargetOutput))
            {
                cachedTextures[adjustedTargetOutput]?.Dispose();
            }

            var rendered = RenderGraph(target, resolution, targetOutput, finalGraph);
            cachedTextures[adjustedTargetOutput] = rendered;
        }
        else
        {
            var cachedTexture = cachedTextures[adjustedTargetOutput];
            Matrix3X3 matrixDiff = SolveMatrixDiff(target, cachedTexture);
            int saved = target.Canvas.Save();
            target.Canvas.SetMatrix(matrixDiff);
            target.Canvas.DrawSurface(cachedTexture.DrawingSurface, 0, 0);
            target.Canvas.RestoreToCount(saved);
        }
    }

    private Texture RenderGraph(DrawingSurface target, ChunkResolution resolution, string? targetOutput, IReadOnlyNodeGraph finalGraph)
    {
        DrawingSurface renderTarget = target;
        Texture? renderTexture = null;
        bool restoreCanvas = false;

        if (RenderInDocumentSize())
        {
            renderTexture = Texture.ForProcessing(Document.Size, Document.ProcessingColorSpace);
            renderTarget = renderTexture.DrawingSurface;
        }
        else
        {
            renderTexture = Texture.ForProcessing(renderTarget.DeviceClipBounds.Size, Document.ProcessingColorSpace);
            renderTarget = renderTexture.DrawingSurface;

            target.Canvas.Save();
            renderTarget.Canvas.Save();

            renderTarget.Canvas.SetMatrix(target.Canvas.TotalMatrix);
            target.Canvas.SetMatrix(Matrix3X3.Identity);
            restoreCanvas = true;
        }

        RenderContext context = new(renderTarget, DocumentViewModel.AnimationHandler.ActiveFrameTime,
            resolution, Document.Size, Document.ProcessingColorSpace);
        context.TargetOutput = targetOutput;
        finalGraph.Execute(context);

        if (renderTexture != null)
        {
            target.Canvas.DrawSurface(renderTexture.DrawingSurface, 0, 0);

            if (restoreCanvas)
            {
                target.Canvas.Restore();
            }
        }

        return renderTexture;
    }

    private bool RenderInDocumentSize()
    {
        return !HighResRendering || !HighDpiRenderNodePresent(Document.NodeGraph);
    }

    private bool ShouldRerender(DrawingSurface target, ChunkResolution resolution, string? targetOutput,
        IReadOnlyNodeGraph finalGraph)
    {
        if (!cachedTextures.TryGetValue(targetOutput ?? "", out var cachedTexture) || cachedTexture == null ||
            cachedTexture.IsDisposed)
        {
            return true;
        }

        if (lastHighResRendering != HighResRendering)
        {
            lastHighResRendering = HighResRendering;
            return true;
        }

        bool renderInDocumentSize = RenderInDocumentSize();
        VecI compareSize = renderInDocumentSize ? Document.Size : target.DeviceClipBounds.Size;

        if (cachedTexture.DrawingSurface.DeviceClipBounds.Size != compareSize)
        {
            return true;
        }

        if(lastFrameTime.Frame != DocumentViewModel.AnimationHandler.ActiveFrameTime.Frame)
        {
            lastFrameTime = DocumentViewModel.AnimationHandler.ActiveFrameTime;
            return true;
        }

        foreach (var frame in DocumentViewModel.AnimationHandler.KeyFrames)
        {
            if (lastFramesVisibility.TryGetValue(frame.Id, out var lastVisibility))
            {
                if (frame.IsVisible != lastVisibility)
                {
                    lastFramesVisibility[frame.Id] = frame.IsVisible;
                    return true;
                }
            }
            else
            {
                lastFramesVisibility[frame.Id] = frame.IsVisible;
                return true;
            }
        }

        if (!renderInDocumentSize)
        {
            double lengthDiff = target.LocalClipBounds.Size.Length - cachedTexture.DrawingSurface.LocalClipBounds.Size.Length;
            if (lengthDiff > 0 || target.LocalClipBounds.Pos != cachedTexture.DrawingSurface.LocalClipBounds.Pos || lengthDiff < -ZoomDiffToRerender)
            {
                return true;
            }
        }

        int currentGraphCacheHash = finalGraph.GetCacheHash();
        if (lastGraphCacheHash != currentGraphCacheHash)
        {
            lastGraphCacheHash = currentGraphCacheHash;
            return true;
        }

        return false;
    }

    private Matrix3X3 SolveMatrixDiff(DrawingSurface target, Texture cachedTexture)
    {
        Matrix3X3 old = cachedTexture.DrawingSurface.Canvas.TotalMatrix;
        Matrix3X3 current = target.Canvas.TotalMatrix;

        Matrix3X3 solveMatrixDiff = current.Concat(old.Invert());
        return solveMatrixDiff;
    }

    private IReadOnlyNodeGraph SolveFinalNodeGraph(string? targetOutput)
    {
        if (targetOutput == null)
        {
            return Document.NodeGraph;
        }

        CustomOutputNode[] outputNodes = Document.NodeGraph.AllNodes.OfType<CustomOutputNode>().ToArray();

        foreach (CustomOutputNode outputNode in outputNodes)
        {
            if (outputNode.OutputName.Value == targetOutput)
            {
                return GraphFromOutputNode(outputNode);
            }
        }

        return Document.NodeGraph;
    }

    private IReadOnlyNodeGraph GraphFromOutputNode(CustomOutputNode outputNode)
    {
        NodeGraph graph = new();
        outputNode.TraverseBackwards(n =>
        {
            if (n is Node node)
            {
                graph.AddNode(node);
            }

            return true;
        });

        graph.CustomOutputNode = outputNode;
        return graph;
    }

    private bool HighDpiRenderNodePresent(IReadOnlyNodeGraph documentNodeGraph)
    {
        bool highDpiRenderNodePresent = false;
        documentNodeGraph.TryTraverse(n =>
        {
            if (n is IHighDpiRenderNode { AllowHighDpiRendering: true })
            {
                highDpiRenderNodePresent = true;
            }
        });

        return highDpiRenderNodePresent;
    }

    private void RenderOnionSkin(DrawingSurface target, ChunkResolution resolution, string? targetOutput)
    {
        var animationData = Document.AnimationData;
        if (!DocumentViewModel.AnimationHandler.OnionSkinningEnabledBindable)
        {
            return;
        }

        double onionOpacity = animationData.OnionOpacity / 100.0;
        double alphaFalloffMultiplier = 1.0 / animationData.OnionFrames;

        var finalGraph = SolveFinalNodeGraph(targetOutput);

        // Render previous frames'
        for (int i = 1; i <= animationData.OnionFrames; i++)
        {
            int frame = DocumentViewModel.AnimationHandler.ActiveFrameTime.Frame - i;
            if (frame < DocumentViewModel.AnimationHandler.FirstFrame)
            {
                break;
            }

            double finalOpacity = onionOpacity * alphaFalloffMultiplier * (animationData.OnionFrames - i + 1);

            RenderContext onionContext = new(target, frame, resolution, Document.Size, Document.ProcessingColorSpace,
                finalOpacity);
            onionContext.TargetOutput = targetOutput;
            finalGraph.Execute(onionContext);
        }

        // Render next frames
        for (int i = 1; i <= animationData.OnionFrames; i++)
        {
            int frame = DocumentViewModel.AnimationHandler.ActiveFrameTime.Frame + i;
            if (frame >= DocumentViewModel.AnimationHandler.LastFrame)
            {
                break;
            }

            double finalOpacity = onionOpacity * alphaFalloffMultiplier * (animationData.OnionFrames - i + 1);
            RenderContext onionContext = new(target, frame, resolution, Document.Size, Document.ProcessingColorSpace,
                finalOpacity);
            onionContext.TargetOutput = targetOutput;
            finalGraph.Execute(onionContext);
        }
    }
}
