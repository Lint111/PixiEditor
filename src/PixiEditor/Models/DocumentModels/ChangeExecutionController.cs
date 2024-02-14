﻿using System.Windows.Input;
using ChunkyImageLib.DataHolders;
using PixiEditor.ChangeableDocument.Enums;
using PixiEditor.DrawingApi.Core.Numerics;
using PixiEditor.Models.DocumentModels.UpdateableChangeExecutors;
using PixiEditor.Models.Enums;
using PixiEditor.ViewModels.SubViewModels.Document;
using PixiEditor.Views.UserControls.SymmetryOverlay;

namespace PixiEditor.Models.DocumentModels;
#nullable enable
internal class ChangeExecutionController
{
    public MouseButtonState LeftMouseState { get; private set; }
    public ShapeCorners LastTransformState { get; private set; }
    public VecI LastPixelPosition => lastPixelPos;
    public VecD LastPrecisePosition => lastPrecisePos;
    public bool IsChangeActive => currentSession is not null;
    public event EventHandler? UpdateableChangeEnded;

    private readonly DocumentViewModel document;
    private readonly DocumentInternalParts internals;

    private VecI lastPixelPos;
    private VecD lastPrecisePos;

    private UpdateableChangeExecutor? currentSession = null;
    
    private UpdateableChangeExecutor? _queuedExecutor = null;

    public ChangeExecutionController(DocumentViewModel document, DocumentInternalParts internals)
    {
        this.document = document;
        this.internals = internals;
    }

    public ExecutorType GetCurrentExecutorType()
    {
        if (currentSession is null)
            return ExecutorType.None;
        return currentSession.Type;
    }

    public bool TryStartExecutor<T>(bool force = false)
        where T : UpdateableChangeExecutor, new()
    {
        if (CanStartExecutor(force))
            return false;
        if (force)
            currentSession?.ForceStop();
        
        T executor = new T();
        return TryStartExecutorInternal(executor);
    }

    public bool TryStartExecutor(UpdateableChangeExecutor brandNewExecutor, bool force = false)
    {
        if (CanStartExecutor(force))
            return false;
        if (force)
            currentSession?.ForceStop();
        
        return TryStartExecutorInternal(brandNewExecutor);
    }

    private bool CanStartExecutor(bool force)
    {
        return (currentSession is not null || _queuedExecutor is not null) && !force;
    }

    private bool TryStartExecutorInternal(UpdateableChangeExecutor executor)
    {
        executor.Initialize(document, internals, this, EndExecutor);

        if (executor.StartMode == ExecutorStartMode.OnMouseLeftButtonDown)
        {
            _queuedExecutor = executor;
            return true;
        }

        return StartExecutor(executor);
    }
    
    private bool StartExecutor(UpdateableChangeExecutor brandNewExecutor)
    {
        if (brandNewExecutor.Start() == ExecutionState.Success)
        {
            currentSession = brandNewExecutor;
            return true;
        }

        return false;
    }

    private void EndExecutor(UpdateableChangeExecutor executor)
    {
        if (executor != currentSession)
            throw new InvalidOperationException();
        currentSession = null;
        _queuedExecutor = null;
        UpdateableChangeEnded?.Invoke(this, EventArgs.Empty);
    }

    public bool TryStopActiveExecutor()
    {
        if (currentSession is null)
            return false;
        currentSession.ForceStop();
        currentSession = null;
        UpdateableChangeEnded?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void MidChangeUndoInlet() => currentSession?.OnMidChangeUndo();
    public void MidChangeRedoInlet() => currentSession?.OnMidChangeRedo();

    public void ConvertedKeyDownInlet(Key key)
    {
        currentSession?.OnConvertedKeyDown(key);
    }

    public void ConvertedKeyUpInlet(Key key)
    {
        currentSession?.OnConvertedKeyUp(key);
    }

    public void MouseMoveInlet(VecD newCanvasPos)
    {
        //update internal state
        VecI newPixelPos = (VecI)newCanvasPos.Floor();
        bool pixelPosChanged = false;
        if (lastPixelPos != newPixelPos)
        {
            lastPixelPos = newPixelPos;
            pixelPosChanged = true;
        }
        lastPrecisePos = newCanvasPos;

        //call session events
        if (currentSession is not null)
        {
            if (pixelPosChanged)
                currentSession.OnPixelPositionChange(newPixelPos);
            currentSession.OnPrecisePositionChange(newCanvasPos);
        }
    }

    public void OpacitySliderDragStartedInlet() => currentSession?.OnOpacitySliderDragStarted();
    public void OpacitySliderDraggedInlet(float newValue)
    {
        currentSession?.OnOpacitySliderDragged(newValue);
    }
    public void OpacitySliderDragEndedInlet() => currentSession?.OnOpacitySliderDragEnded();

    public void SymmetryDragStartedInlet(SymmetryAxisDirection dir) => currentSession?.OnSymmetryDragStarted(dir);
    public void SymmetryDraggedInlet(SymmetryAxisDragInfo info)
    {
        currentSession?.OnSymmetryDragged(info);
    }

    public void SymmetryDragEndedInlet(SymmetryAxisDirection dir) => currentSession?.OnSymmetryDragEnded(dir);

    public void LeftMouseButtonDownInlet(VecD canvasPos)
    {
        //update internal state
        LeftMouseState = MouseButtonState.Pressed;

        if (_queuedExecutor != null && currentSession == null)
        {
            StartExecutor(_queuedExecutor);
        }
        
        //call session event
        currentSession?.OnLeftMouseButtonDown(canvasPos);
    }

    public void LeftMouseButtonUpInlet()
    {
        //update internal state
        LeftMouseState = MouseButtonState.Released;

        //call session events
        currentSession?.OnLeftMouseButtonUp();
    }

    public void TransformMovedInlet(ShapeCorners corners)
    {
        LastTransformState = corners;
        currentSession?.OnTransformMoved(corners);
    }

    public void TransformAppliedInlet() => currentSession?.OnTransformApplied();

    public void LineOverlayMovedInlet(VecD start, VecD end)
    {
        currentSession?.OnLineOverlayMoved(start, end);
    }

    public void SelectedObjectNudgedInlet(VecI distance)
    {
        currentSession?.OnSelectedObjectNudged(distance);
    }
}
