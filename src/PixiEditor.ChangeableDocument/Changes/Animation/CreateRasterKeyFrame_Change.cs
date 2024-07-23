﻿using PixiEditor.ChangeableDocument.Changeables.Animations;
using PixiEditor.ChangeableDocument.Changeables.Graph.Nodes;
using PixiEditor.ChangeableDocument.ChangeInfos.Animation;

namespace PixiEditor.ChangeableDocument.Changes.Animation;

internal class CreateRasterKeyFrame_Change : Change
{
    private readonly Guid _targetLayerGuid;
    private readonly int _frame;
    private readonly Guid? cloneFrom;
    private int? cloneFromFrame;
    private ImageLayerNode? _layer;
    private Guid createdKeyFrameId;

    [GenerateMakeChangeAction]
    public CreateRasterKeyFrame_Change(Guid targetLayerGuid, Guid newKeyFrameGuid, int frame,
        int cloneFromFrame = -1,
        Guid cloneFromExisting = default)
    {
        _targetLayerGuid = targetLayerGuid;
        _frame = frame;
        cloneFrom = cloneFromExisting != default ? cloneFromExisting : null;
        createdKeyFrameId = newKeyFrameGuid;
        this.cloneFromFrame = cloneFromFrame < 0 ? null : cloneFromFrame;
    }

    public override bool InitializeAndValidate(Document target)
    {
        return target.TryFindMember(_targetLayerGuid, out _layer);
    }

    public override OneOf<None, IChangeInfo, List<IChangeInfo>> Apply(Document target, bool firstApply,
        out bool ignoreInUndo)
    {
        var cloneFromImage = cloneFrom.HasValue
            ? target.FindMemberOrThrow<ImageLayerNode>(cloneFrom.Value).GetLayerImageAtFrame(cloneFromFrame ?? 0)
            : null;
        
        ImageLayerNode targetNode = target.FindMemberOrThrow<ImageLayerNode>(_targetLayerGuid);
        
        ChunkyImage img = cloneFromImage?.CloneFromCommitted() ?? new ChunkyImage(target.Size);
        
        var keyFrame =
            new RasterKeyFrame(createdKeyFrameId, targetNode, _frame, target, img);
        
        targetNode.AddFrame(createdKeyFrameId, new ImageFrame(createdKeyFrameId, _frame, 1, img));
        target.AnimationData.AddKeyFrame(keyFrame);
        ignoreInUndo = false;
        return new CreateRasterKeyFrame_ChangeInfo(_targetLayerGuid, _frame, createdKeyFrameId, cloneFrom.HasValue);
    }

    public override OneOf<None, IChangeInfo, List<IChangeInfo>> Revert(Document target)
    {
        target.AnimationData.RemoveKeyFrame(createdKeyFrameId);
        return new DeleteKeyFrame_ChangeInfo(createdKeyFrameId);
    }
}
