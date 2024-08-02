﻿using ChunkyImageLib.DataHolders;
using PixiEditor.ChangeableDocument.Actions;
using PixiEditor.DrawingApi.Core.ColorsImpl;
using PixiEditor.DrawingApi.Core.Numerics;
using PixiEditor.DrawingApi.Core.Surfaces.Surface;
using PixiEditor.Extensions.CommonApi.Palettes;
using PixiEditor.Models.Enums;
using PixiEditor.Numerics;
using PixiEditor.ViewModels.SubViewModels.Document;
using PixiEditor.ViewModels.SubViewModels.Tools;
using PixiEditor.ViewModels.SubViewModels.Tools.Tools;
using PixiEditor.ViewModels.SubViewModels.Tools.ToolSettings.Toolbars;

namespace PixiEditor.Models.DocumentModels.UpdateableChangeExecutors;
#nullable enable
internal class LineToolExecutor : UpdateableChangeExecutor
{
    public override ExecutorType Type => ExecutorType.ToolLinked;

    private VecI startPos;
    private Color strokeColor;
    private int strokeWidth;
    private Guid memberGuid;
    private bool drawOnMask;

    private VecI curPos;
    private bool started = false;
    private bool transforming = false;
    private LineToolViewModel? toolViewModel;

    public override ExecutionState Start()
    {
        ColorsViewModel? colorsVM = ViewModelMain.Current?.ColorsSubViewModel;
        toolViewModel = ViewModelMain.Current?.ToolsSubViewModel.GetTool<LineToolViewModel>();
        StructureMemberViewModel? member = document?.SelectedStructureMember;
        if (colorsVM is null || toolViewModel is null || member is null)
            return ExecutionState.Error;

        drawOnMask = member is LayerViewModel layer ? layer.ShouldDrawOnMask : true;
        if (drawOnMask && !member.HasMaskBindable)
            return ExecutionState.Error;
        if (!drawOnMask && member is not LayerViewModel)
            return ExecutionState.Error;

        startPos = controller!.LastPixelPosition;
        strokeColor = colorsVM.PrimaryColor;
        strokeWidth = toolViewModel.ToolSize;
        memberGuid = member.GuidValue;

        colorsVM.AddSwatch(new PaletteColor(strokeColor.R, strokeColor.G, strokeColor.B));

        return ExecutionState.Success;
    }

    public override void OnPixelPositionChange(VecI pos)
    {
        if (transforming)
            return;
        started = true;

        if (toolViewModel!.Snap)
            pos = ShapeToolExecutor<ShapeTool>.Get45IncrementedPosition(startPos, pos);
        curPos = pos;
        internals!.ActionAccumulator.AddActions(new DrawLine_Action(memberGuid, startPos, pos, strokeWidth, strokeColor, StrokeCap.Butt, drawOnMask));
    }

    public override void OnLeftMouseButtonUp()
    {
        if (!started)
        {
            onEnded!(this);
            return;
        }

        document!.LineToolOverlayViewModel.Show(startPos + new VecD(0.5), curPos + new VecD(0.5));
        transforming = true;
    }

    public override void OnLineOverlayMoved(VecD start, VecD end)
    {
        if (!transforming)
            return;
        internals!.ActionAccumulator.AddActions(new DrawLine_Action(memberGuid, (VecI)start, (VecI)end, strokeWidth, strokeColor, StrokeCap.Butt, drawOnMask));
    }

    public override void OnSelectedObjectNudged(VecI distance)
    {
        if (!transforming)
            return;
        document!.LineToolOverlayViewModel.Nudge(distance);
    }

    public override void OnMidChangeUndo()
    {
        if (!transforming)
            return;
        document!.LineToolOverlayViewModel.Undo();
    }

    public override void OnMidChangeRedo()
    {
        if (!transforming)
            return;
        document!.LineToolOverlayViewModel.Redo();
    }

    public override void OnTransformApplied()
    {
        if (!transforming)
            return;

        document!.LineToolOverlayViewModel.Hide();
        internals!.ActionAccumulator.AddFinishedActions(new EndDrawLine_Action());
        onEnded!(this);
    }

    public override void ForceStop()
    {
        if (transforming)
            document!.LineToolOverlayViewModel.Hide();

        internals!.ActionAccumulator.AddFinishedActions(new EndDrawLine_Action());
    }
}
