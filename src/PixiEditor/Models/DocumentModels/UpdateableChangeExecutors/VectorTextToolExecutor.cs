﻿using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Drawie.Backend.Core.Numerics;
using Drawie.Backend.Core.Text;
using Drawie.Backend.Core.Vector;
using Drawie.Numerics;
using PixiEditor.ChangeableDocument.Actions.Generated;
using PixiEditor.ChangeableDocument.Changeables.Graph.Interfaces;
using PixiEditor.ChangeableDocument.Changeables.Graph.Nodes.Shapes.Data;
using PixiEditor.Helpers.Extensions;
using PixiEditor.Models.Controllers.InputDevice;
using PixiEditor.Models.DocumentModels.UpdateableChangeExecutors.Features;
using PixiEditor.Models.Handlers;
using PixiEditor.Models.Handlers.Toolbars;
using PixiEditor.Models.Handlers.Tools;
using PixiEditor.Models.Tools;
using PixiEditor.ViewModels;
using Color = Drawie.Backend.Core.ColorsImpl.Color;

namespace PixiEditor.Models.DocumentModels.UpdateableChangeExecutors;

internal class VectorTextToolExecutor : UpdateableChangeExecutor, ITextOverlayEvents, IQuickToolSwitchable
{
    private ITextToolHandler textHandler;
    private ITextToolbar toolbar;
    private IStructureMemberHandler selectedMember;

    private string lastText = "";
    private VecD position;
    private Matrix3X3 lastMatrix = Matrix3X3.Identity;
    private Font? cachedFont;
    private bool isListeningForValidLayer;
    private VectorPath? onPath;
    
    private List<Font> fontsToDispose = new();

    public override bool BlocksOtherActions => false;

    public override ExecutorType Type => ExecutorType.ToolLinked;

    public override ExecutionState Start()
    {
        textHandler = GetHandler<ITextToolHandler>();
        if (textHandler == null)
        {
            return ExecutionState.Error;
        }

        toolbar = textHandler.Toolbar as ITextToolbar;
        if (toolbar == null)
        {
            return ExecutionState.Error;
        }

        selectedMember = document.SelectedStructureMember;

        if (selectedMember is not IVectorLayerHandler layerHandler)
        {
            isListeningForValidLayer = true;
            return ExecutionState.Success;
        }

        isListeningForValidLayer = false;
        var shape = layerHandler.GetShapeData(document.AnimationHandler.ActiveFrameBindable);
        if (shape is TextVectorData textData)
        {
            document.TextOverlayHandler.Show(textData.Text, textData.Position, textData.Font,
                textData.TransformationMatrix, textData.Spacing);

            toolbar.Fill = textData.Fill;
            toolbar.FillBrush = textData.FillPaintable.ToBrush();
            toolbar.StrokeBrush = textData.Stroke.ToBrush();
            toolbar.ToolSize = textData.StrokeWidth;
            toolbar.FontFamily = textData.Font.Family;
            toolbar.FontSize = textData.Font.Size;
            toolbar.Spacing = textData.Spacing ?? textData.Font.Size;
            toolbar.Bold = textData.Font.Bold;
            toolbar.Italic = textData.Font.Italic;

            onPath = textData.Path;
            lastText = textData.Text;
            position = textData.Position;
            lastMatrix = textData.TransformationMatrix;
        }
        else if (shape is null)
        {
            document.TextOverlayHandler.Show("", controller.LastPrecisePosition, toolbar.ConstructFont(),
                Matrix3X3.Identity, toolbar.Spacing);
            lastText = "";
            position = controller.LastPrecisePosition;
            // TODO: Implement proper putting on path editing
            /*if (controller.LeftMousePressed)
            {
                TryPutOnPath(controller.LastPrecisePosition);
            }*/
        }
        else
        {
            return ExecutionState.Error;
        }

        return ExecutionState.Success;
    }

    public override void OnLeftMouseButtonDown(MouseOnCanvasEventArgs args)
    {
        var topMostWithinClick = QueryLayers<IVectorLayerHandler>(args.PositionOnCanvas);

        var firstLayer = topMostWithinClick.FirstOrDefault();
        args.Handled = firstLayer != null;
        if (firstLayer is not IVectorLayerHandler layerHandler)
        {
            if (document.TextOverlayHandler.IsActive)
            {
                args.Handled = true;
                document.TextOverlayHandler.Hide();
            }

            return;
        }

        document.Operations.SetSelectedMember(layerHandler.Id);
        document.Operations.InvokeCustomAction(
            () =>
            {
                document.TextOverlayHandler.SetCursorPosition(args.PositionOnCanvas);
            }, false);
    }

    public void OnQuickToolSwitch()
    {
        document.TextOverlayHandler.SetCursorPosition(internals.ChangeController.LastPrecisePosition);
    }

    public override void ForceStop()
    {
        internals.ActionAccumulator.AddFinishedActions(new EndSetShapeGeometry_Action());
        document.TextOverlayHandler.Hide();
        
        foreach (var font in fontsToDispose)
        {
            if (font != null && !font.IsDisposed)
            {
                font.Dispose();
            }
        }
        
        fontsToDispose.Clear();
    }

    public void OnTextChanged(string text)
    {
        var constructedText = ConstructTextData(text);
        internals.ActionAccumulator.AddFinishedActions(
            new SetShapeGeometry_Action(selectedMember.Id, constructedText),
            new EndSetShapeGeometry_Action(),
            new SetLowDpiRendering_Action(selectedMember.Id, toolbar.ForceLowDpiRendering));
        lastText = text;
        document.TextOverlayHandler.Font = constructedText.Font;
    }

    public override void OnSettingsChanged(string name, object value)
    {
        if (isListeningForValidLayer)
        {
            return;
        }

        if (name == nameof(ITextToolbar.FontFamily))
        {
            fontsToDispose.Add(cachedFont);
            cachedFont = toolbar.ConstructFont();
            document.TextOverlayHandler.Font = cachedFont;
        }
        else
        {
            if (cachedFont == null)
            {
                cachedFont = toolbar.ConstructFont();
            }

            document.TextOverlayHandler.Font.Size = toolbar.FontSize;
            cachedFont.Size = toolbar.FontSize;
            cachedFont.Bold = toolbar.Bold;
            cachedFont.Italic = toolbar.Italic;
        }

        var constructedText = ConstructTextData(lastText);
        internals.ActionAccumulator.AddActions(
            new SetShapeGeometry_Action(selectedMember.Id, constructedText),
            new SetLowDpiRendering_Action(selectedMember.Id, toolbar.ForceLowDpiRendering));

        document.TextOverlayHandler.Font = constructedText.Font;
        document.TextOverlayHandler.Spacing = toolbar.Spacing;
    }

    public override void OnColorChanged(Color color, bool primary)
    {
        if (!primary || !toolbar.SyncWithPrimaryColor)
        {
            return;
        }

        toolbar.StrokeBrush = new SolidColorBrush(color.ToColor());
        toolbar.FillBrush = new SolidColorBrush(color.ToColor());
    }

    private void TryPutOnPath(VecD pos)
    {
        var topMostWithinClick = QueryLayers<IVectorLayerHandler>(pos);
        var firstValidLayer = topMostWithinClick.FirstOrDefault(x =>
            x.GetShapeData(document.AnimationHandler.ActiveFrameTime) is not null and not TextVectorData);

        if (firstValidLayer is null)
        {
            return;
        }

        var shape = firstValidLayer.GetShapeData(document.AnimationHandler.ActiveFrameTime);

        ShapeVectorData newShape = (ShapeVectorData)(shape as ShapeVectorData).Clone();

        newShape.Fill = false;
        newShape.StrokeWidth = 0;

        onPath = newShape.ToPath();

        var constructedText = ConstructTextData(lastText);
        internals.ActionAccumulator.AddFinishedActions(
            new SetShapeGeometry_Action(selectedMember.Id, constructedText),
            new EndSetShapeGeometry_Action(),
            new SetLowDpiRendering_Action(selectedMember.Id, toolbar.ForceLowDpiRendering),
            new SetShapeGeometry_Action(firstValidLayer.Id, newShape),
            new EndSetShapeGeometry_Action());
    }

    private TextVectorData ConstructTextData(string text)
    {
        if (cachedFont == null || cachedFont.Family.Name != toolbar.FontFamily.Name)
        {
            Font toDispose = cachedFont;
            Dispatcher.UIThread.Post(() =>
            {
                toDispose?.Dispose();
            });
            cachedFont = toolbar.ConstructFont();
        }
        else
        {
            cachedFont.Size = toolbar.FontSize;
        }

        return new TextVectorData()
        {
            Text = text,
            Position = position,
            Fill = toolbar.Fill,
            FillPaintable = toolbar.FillBrush.ToPaintable(),
            StrokeWidth = (float)toolbar.ToolSize,
            Stroke = toolbar.StrokeBrush.ToPaintable(),
            TransformationMatrix = lastMatrix,
            Font = cachedFont,
            Spacing = toolbar.Spacing,
            AntiAlias = toolbar.AntiAliasing,
            Path = onPath,
            // TODO: MaxWidth = toolbar.MaxWidth
        };
    }

    bool IExecutorFeature.IsFeatureEnabled(IExecutorFeature feature)
    {
        return feature is ITextOverlayEvents || feature is IQuickToolSwitchable;
    }
}
