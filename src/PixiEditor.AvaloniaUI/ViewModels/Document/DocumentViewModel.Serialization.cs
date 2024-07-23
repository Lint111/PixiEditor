﻿using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using ChunkyImageLib;
using ChunkyImageLib.DataHolders;
using PixiEditor.AvaloniaUI.Helpers;
using PixiEditor.AvaloniaUI.Models.Handlers;
using PixiEditor.AvaloniaUI.Models.IO.FileEncoders;
using PixiEditor.ChangeableDocument.Changeables.Graph.Interfaces;
using PixiEditor.ChangeableDocument.Changeables.Interfaces;
using PixiEditor.DrawingApi.Core.Bridge;
using PixiEditor.DrawingApi.Core.Numerics;
using PixiEditor.DrawingApi.Core.Surface;
using PixiEditor.DrawingApi.Core.Surface.ImageData;
using PixiEditor.Extensions.CommonApi.Palettes;
using PixiEditor.Numerics;
using PixiEditor.Parser;
using PixiEditor.Parser.Collections;
using BlendMode = PixiEditor.Parser.BlendMode;
using IKeyFrameChildrenContainer = PixiEditor.ChangeableDocument.Changeables.Interfaces.IKeyFrameChildrenContainer;
using PixiDocument = PixiEditor.Parser.Document;

namespace PixiEditor.AvaloniaUI.ViewModels.Document;

internal partial class DocumentViewModel
{
    public PixiDocument ToSerializable()
    {
        var root = new Folder();
        
        var doc = Internals.Tracker.Document;

        //AddMembers(doc.StructureRoot.Children, doc, root);

        var document = new PixiDocument
        {
            Width = Width, Height = Height,
            Swatches = ToCollection(Swatches), Palette = ToCollection(Palette),
            RootFolder = root, PreviewImage = (TryRenderWholeImage(0).Value as Surface)?.DrawingSurface.Snapshot().Encode().AsSpan().ToArray(),
            ReferenceLayer = GetReferenceLayer(doc),
            AnimationData = ToAnimationData(doc.AnimationData)
        };

        return document;
    }

    private static ReferenceLayer? GetReferenceLayer(IReadOnlyDocument document)
    {
        if (document.ReferenceLayer == null)
        {
            return null;
        }

        var layer = document.ReferenceLayer!;

        var surface = new Surface(new VecI(layer.ImageSize.X, layer.ImageSize.Y));
        
        surface.DrawBytes(surface.Size, layer.ImageBgra8888Bytes.ToArray(), ColorType.Bgra8888, AlphaType.Premul);

        var encoder = new UniversalFileEncoder(EncodedImageFormat.Png);

        using var stream = new MemoryStream();
        
        encoder.Save(stream, surface);

        stream.Position = 0;

        return new ReferenceLayer
        {
            Enabled = layer.IsVisible,
            Width = (float)layer.Shape.RectSize.X,
            Height = (float)layer.Shape.RectSize.Y,
            OffsetX = (float)layer.Shape.TopLeft.X,
            OffsetY = (float)layer.Shape.TopLeft.Y,
            Corners = new Corners
            {
                TopLeft = layer.Shape.TopLeft.ToVector2(), 
                TopRight = layer.Shape.TopRight.ToVector2(), 
                BottomLeft = layer.Shape.BottomLeft.ToVector2(), 
                BottomRight = layer.Shape.BottomRight.ToVector2()
            },
            Opacity = 1,
            ImageBytes = stream.ToArray()
        };
    }

    private static void AddMembers(IEnumerable<IReadOnlyStructureNode> members, IReadOnlyDocument document, Folder parent)
    {
        foreach (var member in members)
        {
            if (member is IReadOnlyFolderNode readOnlyFolder)
            {
                var folder = ToSerializable(readOnlyFolder);

                //AddMembers(readOnlyFolder.Children, document, folder);

                parent.Children.Add(folder);
            }
            else if (member is IReadOnlyLayerNode readOnlyLayer)
            {
                parent.Children.Add(ToSerializable(readOnlyLayer, document));
            }
        }
    }
    
    private static Folder ToSerializable(IReadOnlyFolderNode folder)
    {
        return new Folder
        {
            Name = folder.MemberName,
            BlendMode = (BlendMode)(int)folder.BlendMode.Value,
            Enabled = folder.IsVisible.Value,
            Opacity = folder.Opacity.Value,
            ClipToMemberBelow = folder.ClipToPreviousMember.Value,
            Mask = GetMask(folder.Mask.Value, folder.MaskIsVisible.Value)
        };
    }
    
    private static ImageLayer ToSerializable(IReadOnlyLayerNode layer, IReadOnlyDocument document)
    {
        var result = document.GetLayerRasterizedImage(layer.Id, 0);

        var tightBounds = document.GetChunkAlignedLayerBounds(layer.Id, 0);
        using var data = result?.Encode();
        byte[] bytes = data?.AsSpan().ToArray();
        var serializable = new ImageLayer
        {
            Width = result?.Width ?? 0, Height = result?.Height ?? 0, OffsetX = tightBounds?.X ?? 0, OffsetY = tightBounds?.Y ?? 0,
            Enabled = layer.IsVisible.Value, BlendMode = (BlendMode)(int)layer.BlendMode.Value, ImageBytes = bytes,
            ClipToMemberBelow = layer.ClipToPreviousMember.Value, Name = layer.MemberName,
            Guid = layer.Id,
            LockAlpha = layer is ITransparencyLockable { LockTransparency: true },
            Opacity = layer.Opacity.Value, Mask = GetMask(layer.Mask.Value, layer.MaskIsVisible.Value)
        };

        return serializable;
    }

    private static Mask GetMask(IReadOnlyChunkyImage mask, bool maskVisible)
    {
        if (mask == null) 
            return null;
        
        var maskBound = mask.FindChunkAlignedMostUpToDateBounds();

        if (maskBound == null)
        {
            return new Mask();
        }
        
        var surface = DrawingBackendApi.Current.SurfaceImplementation.Create(new ImageInfo(
            maskBound.Value.Width,
            maskBound.Value.Height));
                
        mask.DrawMostUpToDateRegionOn(new RectI(0, 0, maskBound.Value.Width, maskBound.Value.Height), ChunkResolution.Full, surface, new VecI(0, 0));

        return new Mask
        {
            Width = maskBound.Value.Width, Height = maskBound.Value.Height,
            OffsetX = maskBound.Value.X, OffsetY = maskBound.Value.Y,
            Enabled = maskVisible, ImageBytes = surface.Snapshot().Encode().AsSpan().ToArray()
        };
    }

    private ColorCollection ToCollection(IList<PaletteColor> collection) =>
        new(collection.Select(x => Color.FromArgb(255, x.R, x.G, x.B)));

    private AnimationData ToAnimationData(IReadOnlyAnimationData animationData)
    {
        var animData = new AnimationData();
        animData.KeyFrameGroups = new List<KeyFrameGroup>();
        BuildKeyFrames(animationData.KeyFrames, animData);
        
        return animData;
    }

    private static void BuildKeyFrames(IReadOnlyList<IReadOnlyKeyFrame> root, AnimationData animationData)
    {
        foreach (var keyFrame in root)
        {
            if(keyFrame is IKeyFrameChildrenContainer container)
            {
                KeyFrameGroup group = new();
                group.LayerGuid = keyFrame.LayerGuid;
                group.Enabled = keyFrame.IsVisible;
                
                foreach (var child in container.Children)
                {
                    if (child is IKeyFrameChildrenContainer groupKeyFrame)
                    {
                        BuildKeyFrames(groupKeyFrame.Children, null);
                    }
                    else if (child is IReadOnlyRasterKeyFrame rasterKeyFrame)
                    {
                        BuildRasterKeyFrame(rasterKeyFrame, group);
                    }
                }
                
                animationData?.KeyFrameGroups.Add(group);
            }
        }
    }

    private static void BuildRasterKeyFrame(IReadOnlyRasterKeyFrame rasterKeyFrame, KeyFrameGroup group)
    {
        var bounds = rasterKeyFrame.Image.FindChunkAlignedMostUpToDateBounds();

        DrawingSurface surface = null;
                        
        if (bounds != null)
        {
            surface = DrawingBackendApi.Current.SurfaceImplementation.Create(
                new ImageInfo(bounds.Value.Width, bounds.Value.Height));

            rasterKeyFrame.Image.DrawMostUpToDateRegionOn(
                new RectI(0, 0, bounds.Value.Width, bounds.Value.Height), ChunkResolution.Full, surface,
                new VecI(0, 0));
        }

        group.Children.Add(new RasterKeyFrame()
        {
            LayerGuid = rasterKeyFrame.LayerGuid,
            StartFrame = rasterKeyFrame.StartFrame,
            Duration = rasterKeyFrame.Duration,
            ImageBytes = surface?.Snapshot().Encode().AsSpan().ToArray(),
        });
    }
}
