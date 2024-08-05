﻿using ChunkyImageLib;
using ChunkyImageLib.DataHolders;
using PixiEditor.DrawingApi.Core;
using PixiEditor.DrawingApi.Core.Surfaces.PaintImpl;
using PixiEditor.DrawingApi.Skia;
using PixiEditor.Numerics;
using PixiEditor.Parser.Skia;

namespace PixiEditor.Models.Serialization.Factories;

public class ChunkyImageSerializationFactory : SerializationFactory<byte[], ChunkyImage>
{
    private static SurfaceSerializationFactory surfaceFactory = new();

    public override byte[] Serialize(ChunkyImage original)
    {
        var encoder = Config.Encoder;
        surfaceFactory.Config = Config;

        Texture surface = new Texture(original.LatestSize);
        original.DrawMostUpToDateRegionOn(
            new RectI(0, 0, original.LatestSize.X,
                original.LatestSize.Y), ChunkResolution.Full, surface.Surface, new VecI(0, 0), new Paint());

        return surfaceFactory.Serialize(surface);
    }

    public override bool TryDeserialize(object serialized, out ChunkyImage original)
    {
        if (serialized is byte[] imgBytes)
        {
            surfaceFactory.Config = Config;
            if (!surfaceFactory.TryDeserialize(imgBytes, out Texture surface))
            {
                original = null;
                return false;
            }

            original = new ChunkyImage(surface.Size);
            original.EnqueueDrawImage(VecI.Zero, surface);
            original.CommitChanges();
            return true;
        }

        original = null;
        return false;
    }

    public override string DeserializationId { get; } = "PixiEditor.ChunkyImage";
}
