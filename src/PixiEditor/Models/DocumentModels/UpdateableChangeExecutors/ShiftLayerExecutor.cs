﻿using System.Collections.Generic;
using System.Linq;
using PixiEditor.ChangeableDocument.Actions.Generated;
using Drawie.Backend.Core.Numerics;
using PixiEditor.Models.Handlers;
using PixiEditor.Models.Handlers.Tools;
using PixiEditor.Models.Tools;
using Drawie.Numerics;

namespace PixiEditor.Models.DocumentModels.UpdateableChangeExecutors;
#nullable enable
internal class ShiftLayerExecutor : UpdateableChangeExecutor
{
    private List<Guid> _affectedMemberGuids = new List<Guid>();
    private VecI startPos;
    private IMoveToolHandler? tool;

    public override ExecutorStartMode StartMode => ExecutorStartMode.OnMouseLeftButtonDown;

    public override ExecutionState Start()
    {
        IStructureMemberHandler? member = document!.SelectedStructureMember;

        tool = GetHandler<IMoveToolHandler>();
        if (tool is null)
            return ExecutionState.Error;


        if (member != null)
            _affectedMemberGuids.Add(member.Id);
        _affectedMemberGuids.AddRange(document!.SoftSelectedStructureMembers.Select(x => x.Id));

        RemoveDrawOnMaskLayers(_affectedMemberGuids);

        startPos = controller!.LastPixelPosition;

        ShiftLayer_Action action = new(_affectedMemberGuids, VecI.Zero, tool.KeepOriginalImage,
            document!.AnimationHandler.ActiveFrameBindable);
        internals!.ActionAccumulator.AddActions(action);

        return ExecutionState.Success;
    }

    private void RemoveDrawOnMaskLayers(List<Guid> affectedMemberGuids)
    {
        for (var i = 0; i < affectedMemberGuids.Count; i++)
        {
            var guid = affectedMemberGuids[i];
            if (document!.StructureHelper.FindOrThrow(guid) is ILayerHandler { ShouldDrawOnMask: true })
            {
                _affectedMemberGuids.Remove(guid);
                i--;
            }
        }
    }

    public override void OnPixelPositionChange(VecI pos)
    {
        ShiftLayer_Action action = new(_affectedMemberGuids, pos - startPos, tool!.KeepOriginalImage,
            document!.AnimationHandler.ActiveFrameBindable);
        internals!.ActionAccumulator.AddActions(action);
    }

    public override void OnLeftMouseButtonUp(VecD argsPositionOnCanvas)
    {
        internals!.ActionAccumulator.AddFinishedActions(new EndShiftLayer_Action());
        onEnded?.Invoke(this);
    }

    public override void ForceStop()
    {
        internals!.ActionAccumulator.AddFinishedActions(new EndShiftLayer_Action());
    }
}
