﻿using PixiEditor.ChangeableDocument.Changeables.Graph.Interfaces;
using PixiEditor.ChangeableDocument.Changeables.Interfaces;
using PixiEditor.DrawingApi.Core.Surface.ImageData;
using PixiEditor.Numerics;

namespace PixiEditor.ChangeableDocument.Rendering;

public class DocumentEvaluator
{
    public DocumentEvaluator(IReadOnlyDocument document)
    {
        Document = document;
    }

    private IReadOnlyDocument Document { get; }
    
    public OneOf<Chunk, EmptyChunk> RenderChunk(VecI chunkPos, ChunkResolution resolution, int frame, RectI? globalClippingRect = null)
    {
        using RenderingContext context = new(frame, chunkPos, resolution, Document.Size);
        try
        {
            RectI? transformedClippingRect = TransformClipRect(globalClippingRect, resolution, chunkPos);

            Chunk? chunk = Document.NodeGraph.Execute(context);
            if (chunk is null)
            {
                return new EmptyChunk();
            }
            
            /*Chunk chunk = Chunk.Create(resolution);

            chunk.Surface.DrawingSurface.Canvas.Save();
            chunk.Surface.DrawingSurface.Canvas.Clear();

            if (transformedClippingRect is not null)
            {
                chunk.Surface.DrawingSurface.Canvas.ClipRect((RectD)transformedClippingRect);
            }
            
            VecD pos = chunkPos;
            int x = (int)(pos.X * ChunkyImage.FullChunkSize * resolution.Multiplier());
            int y = (int)(pos.Y * ChunkyImage.FullChunkSize * resolution.Multiplier());
            int width = (int)(ChunkyImage.FullChunkSize * resolution.Multiplier());
            int height = (int)(ChunkyImage.FullChunkSize * resolution.Multiplier());
            
            RectD sourceRect = new(x, y, width, height);

            using var chunkSnapshot = evaluated.DrawingSurface.Snapshot((RectI)sourceRect);
            
            chunk.Surface.DrawingSurface.Canvas.DrawImage(chunkSnapshot, 0, 0, context.ReplacingPaintWithOpacity);

            chunk.Surface.DrawingSurface.Canvas.Restore();*/

            return chunk;
        }
        catch (ObjectDisposedException)
        {
            return new EmptyChunk();
        }
    }

    public OneOf<Chunk, EmptyChunk> RenderChunk(VecI chunkPos, ChunkResolution resolution,
        IReadOnlyNode node, int frame, RectI? globalClippingRect = null)
    {
        using RenderingContext context = new(frame, chunkPos, resolution, Document.Size);
        try
        {
            RectI? transformedClippingRect = TransformClipRect(globalClippingRect, resolution, chunkPos);

            Chunk? chunk = node.Execute(context);
            if (chunk is null)
            {
                return new EmptyChunk();
            }

            /*if (transformedClippingRect is not null)
            {
                chunk.Surface.DrawingSurface.Canvas.ClipRect((RectD)transformedClippingRect);
            }
            
            chunk.Surface.DrawingSurface.Canvas.DrawSurface(evaluated.DrawingSurface, transformedClippingRect.Value.X, transformedClippingRect.Value.Y, context.ReplacingPaintWithOpacity);

            chunk.Surface.DrawingSurface.Canvas.Restore();*/

            return chunk;
        }
        catch (ObjectDisposedException)
        {
            return new EmptyChunk();
        }
    }

    private static RectI? TransformClipRect(RectI? globalClippingRect, ChunkResolution resolution, VecI chunkPos)
    {
        if (globalClippingRect is not RectI rect)
            return null;

        double multiplier = resolution.Multiplier();
        VecI pixelChunkPos = chunkPos * (int)(ChunkyImage.FullChunkSize * multiplier);
        return (RectI?)rect.Scale(multiplier).Translate(-pixelChunkPos).RoundOutwards();
    }
}
