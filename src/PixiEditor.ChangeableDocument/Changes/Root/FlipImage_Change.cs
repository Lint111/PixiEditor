﻿using ChunkyImageLib.Operations;
using PixiEditor.ChangeableDocument.Changeables.Graph.Nodes;
using PixiEditor.ChangeableDocument.ChangeInfos.Root;
using PixiEditor.ChangeableDocument.Enums;
using PixiEditor.DrawingApi.Core;
using PixiEditor.DrawingApi.Core.Numerics;
using PixiEditor.DrawingApi.Core.Surfaces;
using PixiEditor.DrawingApi.Core.Surfaces.Surface;
using PixiEditor.DrawingApi.Core.Surfaces.Surface.PaintImpl;
using PixiEditor.Numerics;
using BlendMode = PixiEditor.ChangeableDocument.Enums.BlendMode;

namespace PixiEditor.ChangeableDocument.Changes.Root;

internal sealed class FlipImage_Change : Change
{
    private readonly FlipType flipType;
    private List<Guid> membersToFlip;
    private int frame;

    [GenerateMakeChangeAction]
    public FlipImage_Change(FlipType flipType, int frame, List<Guid>? membersToFlip = null)
    {
        this.flipType = flipType;
        membersToFlip ??= new List<Guid>();
        this.frame = frame;
        this.membersToFlip = membersToFlip;
    }
    
    public override bool InitializeAndValidate(Document target)
    {
        if (membersToFlip.Count > 0)
        {
            membersToFlip = target.ExtractLayers(membersToFlip);
            
            foreach (var layer in membersToFlip)
            {
                if (!target.HasMember(layer)) return false;
            }  
        }
        
        return true;
    }

    public override OneOf<None, IChangeInfo, List<IChangeInfo>> Apply(Document target, bool firstApply, out bool ignoreInUndo)
    {
        var changes = Flip(target);
        
        ignoreInUndo = false;
        return changes;
    }

    private void FlipImage(ChunkyImage img)
    {
        using Paint paint = new()
        {
            BlendMode = Surface.BlendMode.Src
        };

        RectI bounds = new RectI(VecI.Zero, img.LatestSize);
        if (membersToFlip.Count > 0)
        {
            var preciseBounds = img.FindTightCommittedBounds();
            if (preciseBounds.HasValue)
            {
                bounds = preciseBounds.Value;
            }
        }

        using Surface originalSurface = new(img.LatestSize);
        img.DrawMostUpToDateRegionOn(
            new RectI(VecI.Zero, img.LatestSize), 
            ChunkResolution.Full,
            originalSurface.DrawingSurface,
            VecI.Zero);

        using Surface flipped = new Surface(img.LatestSize);

        bool flipX = flipType == FlipType.Horizontal;
        bool flipY = flipType == FlipType.Vertical;
        
        flipped.DrawingSurface.Canvas.Save();
        flipped.DrawingSurface.Canvas.Scale(
            flipX ? -1 : 1, 
            flipY ? -1 : 1, 
            flipX ? bounds.X + (bounds.Width / 2f) : 0,
            flipY ? bounds.Y + (bounds.Height / 2f) : 0f);
        flipped.DrawingSurface.Canvas.DrawSurface(originalSurface.DrawingSurface, 0, 0, paint);
        flipped.DrawingSurface.Canvas.Restore();
        
        img.EnqueueClear();
        img.EnqueueDrawImage(VecI.Zero, flipped);
    }

    public override OneOf<None, IChangeInfo, List<IChangeInfo>> Revert(Document target)
    {
        return Flip(target);
    }

    private OneOf<None, IChangeInfo, List<IChangeInfo>> Flip(Document target)
    {
        List<IChangeInfo> changes = new List<IChangeInfo>();

        target.ForEveryMember(member =>
        {
            if (membersToFlip.Count == 0 || membersToFlip.Contains(member.Id))
            {
                if (member is ImageLayerNode layer)
                {
                    var image = layer.GetLayerImageAtFrame(frame);
                    FlipImage(image);
                    changes.Add(
                        new LayerImageArea_ChangeInfo(member.Id, image.FindAffectedArea()));
                    image.CommitChanges();
                }
                // TODO: Add support for non-raster layers

                if (member.Mask.Value is not null)
                {
                    FlipImage(member.Mask.Value);
                    changes.Add(
                        new MaskArea_ChangeInfo(member.Id, member.Mask.Value.FindAffectedArea()));
                    member.Mask.Value.CommitChanges();
                }
            }
        });

        return changes;
    }
}
