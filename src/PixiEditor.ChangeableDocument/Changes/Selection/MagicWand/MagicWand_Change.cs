﻿using PixiEditor.ChangeableDocument.Changes.Drawing;
using PixiEditor.ChangeableDocument.Enums;
using PixiEditor.DrawingApi.Core.Numerics;
using PixiEditor.DrawingApi.Core.Surfaces.Surface.Vector;
using PixiEditor.Numerics;

namespace PixiEditor.ChangeableDocument.Changes.Selection.MagicWand;

internal class MagicWand_Change : Change
{
    private VectorPath? originalPath;
    private VectorPath path = new() { FillType = PathFillType.EvenOdd };
    private VecI point;
    private readonly List<Guid> memberGuids;
    private readonly SelectionMode mode;
    private int frame;

    [GenerateMakeChangeAction]
    public MagicWand_Change(List<Guid> memberGuids, VecI point, SelectionMode mode, int frame)
    {
        path.MoveTo(point);
        this.mode = mode;
        this.memberGuids = memberGuids;
        this.point = point;
        this.frame = frame;
    }

    public override bool InitializeAndValidate(Document target)
    {
        originalPath = new VectorPath(target.Selection.SelectionPath);
        return true;
    }

    public override OneOf<None, IChangeInfo, List<IChangeInfo>> Apply(Document target, bool firstApply, out bool ignoreInUndo)
    {
        HashSet<Guid> membersToReference = new();

        target.ForEveryReadonlyMember(member =>
        {
            if (memberGuids.Contains(member.Id))
                membersToReference.Add(member.Id);
        });

        path = MagicWandHelper.DoMagicWandFloodFill(point, membersToReference, target, frame);

        ignoreInUndo = false;
        return CommonApply(target);
    }

    public override OneOf<None, IChangeInfo, List<IChangeInfo>> Revert(Document target)
    {
        (var toDispose, target.Selection.SelectionPath) = (target.Selection.SelectionPath, new VectorPath(originalPath!));
        toDispose.Dispose();
        return new Selection_ChangeInfo(new VectorPath(target.Selection.SelectionPath));
    }

    private Selection_ChangeInfo CommonApply(Document target)
    {
        var toDispose = target.Selection.SelectionPath;
        if (mode == SelectionMode.New)
        {
            var copy = new VectorPath(path);
            copy.Close();
            target.Selection.SelectionPath = copy;
        }
        else
        {
            target.Selection.SelectionPath = originalPath!.Op(path, mode.ToVectorPathOp());
        }
        toDispose.Dispose();

        return new Selection_ChangeInfo(new VectorPath(target.Selection.SelectionPath));
    }

    public override void Dispose()
    {
        path.Dispose();
        originalPath?.Dispose();
    }
}
