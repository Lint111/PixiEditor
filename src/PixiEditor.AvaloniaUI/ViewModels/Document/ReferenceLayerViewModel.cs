﻿using System.Collections.Immutable;
using System.Linq;
using Avalonia;
using Avalonia.Media.Imaging;
using ChunkyImageLib;
using ChunkyImageLib.DataHolders;
using ChunkyImageLib.Operations;
using CommunityToolkit.Mvvm.ComponentModel;
using PixiEditor.AvaloniaUI.Helpers;
using PixiEditor.AvaloniaUI.Models.DocumentModels;
using PixiEditor.AvaloniaUI.Models.Handlers;
using PixiEditor.AvaloniaUI.ViewModels.Tools.Tools;
using PixiEditor.ChangeableDocument.Actions.Generated;
using PixiEditor.DrawingApi.Core;
using PixiEditor.DrawingApi.Core.Numerics;
using PixiEditor.DrawingApi.Core.Surfaces;
using PixiEditor.DrawingApi.Core.Surfaces.Surface;
using PixiEditor.DrawingApi.Core.Surfaces.Surface.ImageData;
using PixiEditor.Numerics;

namespace PixiEditor.AvaloniaUI.ViewModels.Document;

#nullable enable
internal class ReferenceLayerViewModel : ObservableObject, IReferenceLayerHandler
{
    private readonly DocumentViewModel doc;
    private readonly DocumentInternalParts internals;

    public const double TopMostOpacity = 0.6;
    
    public Surface? ReferenceBitmap { get; private set; }

    private ShapeCorners referenceShape;
    public ShapeCorners ReferenceShapeBindable 
    { 
        get => referenceShape; 
        set
        {
            if (!doc.UpdateableChangeActive)
                internals.ActionAccumulator.AddFinishedActions(new TransformReferenceLayer_Action(value));
        }
    }
    
    public Matrix ReferenceTransformMatrix
    {
        get
        {
            if (ReferenceBitmap is null)
                return Matrix.Identity;

            Matrix3X3 skiaMatrix = OperationHelper.CreateMatrixFromPoints(ReferenceShapeBindable, new VecD(ReferenceBitmap.Size.X, ReferenceBitmap.Size.Y));
            return new Matrix(skiaMatrix.ScaleX, skiaMatrix.SkewY, skiaMatrix.SkewX, skiaMatrix.ScaleY, skiaMatrix.TransX, skiaMatrix.TransY);
        }
    }

    private bool isVisible;
    public bool IsVisibleBindable
    {
        get => isVisible;
        set
        {
            if (!doc.UpdateableChangeActive)
                internals.ActionAccumulator.AddFinishedActions(new ReferenceLayerIsVisible_Action(value));
        }
    }

    private bool isTransforming;
    public bool IsTransforming
    {
        get => isTransforming;
        set
        {
            isTransforming = value;
            OnPropertyChanged(nameof(IsTransforming));
            OnPropertyChanged(nameof(ShowHighest));
        }
    }
    
    private bool isTopMost;
    public bool IsTopMost
    {
        get => isTopMost;
        set
        {
            if (!doc.UpdateableChangeActive)
                internals.ActionAccumulator.AddFinishedActions(new ReferenceLayerTopMost_Action(value));
        }
    }
    
    public bool ShowHighest
    {
        get => (IsTopMost || IsTransforming) && !IsColorPickerSelected();
    }

    public ReferenceLayerViewModel(DocumentViewModel doc, DocumentInternalParts internals)
    {
        this.doc = doc;
        this.internals = internals;
    }

    private bool IsColorPickerSelected()
    {
        var viewModel = ViewModelMain.Current.ToolsSubViewModel;
        
        if (viewModel.ActiveTool is ColorPickerToolViewModel colorPicker)
        {
            return colorPicker.PickFromReferenceLayer && !colorPicker.PickFromCanvas;
        }

        return false;
    }

    #region Internal methods

    public void RaiseShowHighestChanged() => OnPropertyChanged(nameof(ShowHighest));
    
    public void SetReferenceLayer(ImmutableArray<byte> imageBgra8888Bytes, VecI imageSize, ShapeCorners shape)
    {
        ReferenceBitmap = Surface.Load(imageBgra8888Bytes.ToArray(), ColorType.Bgra8888, imageSize); //TODO: Was WriteableBitmapUtility.FromBgra8888Array(imageBgra8888Bytes.ToArray(), imageSize);
        referenceShape = shape;
        isVisible = true;
        isTransforming = false;
        isTopMost = false;
        OnPropertyChanged(nameof(ReferenceBitmap));
        OnPropertyChanged(nameof(ReferenceShapeBindable));
        OnPropertyChanged(nameof(ReferenceTransformMatrix));
        OnPropertyChanged(nameof(IsVisibleBindable));
        OnPropertyChanged(nameof(IsTransforming));
        OnPropertyChanged(nameof(ShowHighest));
    }

    public void DeleteReferenceLayer()
    {
        ReferenceBitmap = null;
        isVisible = false;
        OnPropertyChanged(nameof(ReferenceBitmap));
        OnPropertyChanged(nameof(ReferenceTransformMatrix));
        OnPropertyChanged(nameof(IsVisibleBindable));
    }
    
    public void TransformReferenceLayer(ShapeCorners shape)
    {
        referenceShape = shape;
        OnPropertyChanged(nameof(ReferenceShapeBindable));
        OnPropertyChanged(nameof(ReferenceTransformMatrix));
    }

    public void SetReferenceLayerIsVisible(bool isVisible)
    {
        this.isVisible = isVisible;
        OnPropertyChanged(nameof(IsVisibleBindable));
    }

    public void SetReferenceLayerTopMost(bool isTopMost)
    {
        this.isTopMost = isTopMost;
        OnPropertyChanged(nameof(IsTopMost));
        OnPropertyChanged(nameof(ShowHighest));
    }

    #endregion
}
