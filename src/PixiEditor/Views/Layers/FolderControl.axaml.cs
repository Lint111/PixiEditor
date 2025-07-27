﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using PixiEditor.Models.Controllers.InputDevice;
using PixiEditor.Models.Layers;
using PixiEditor.ViewModels.Document;
using PixiEditor.ViewModels.Document.Nodes;

namespace PixiEditor.Views.Layers;
#nullable enable
internal partial class FolderControl : UserControl
{
    public static readonly StyledProperty<FolderNodeViewModel> FolderProperty =
        AvaloniaProperty.Register<FolderControl, FolderNodeViewModel>(nameof(Folder));

    public FolderNodeViewModel Folder
    {
        get => GetValue(FolderProperty);
        set => SetValue(FolderProperty, value);
    }

    public static readonly StyledProperty<LayersManager> ManagerProperty =
        AvaloniaProperty.Register<FolderControl, LayersManager>(nameof(Manager));

    public LayersManager Manager
    {
        get { return GetValue(ManagerProperty); }
        set { SetValue(ManagerProperty, value); }
    }

    private readonly IBrush? highlightColor;


    private MouseUpdateController? mouseUpdateController;

    public FolderControl()
    {
        InitializeComponent();
        if (App.Current.TryGetResource("SoftSelectedLayerBrush", App.Current.ActualThemeVariant, out var value))
        {
            highlightColor = value as IBrush;
        }

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        AddHandler(DragDrop.DragEnterEvent, FolderControl_DragEnter);
        AddHandler(DragDrop.DragLeaveEvent, FolderControl_DragLeave);

        TopDropGrid.AddHandler(DragDrop.DragEnterEvent, Grid_DragEnter);
        TopDropGrid.AddHandler(DragDrop.DragLeaveEvent, Grid_DragLeave);
        TopDropGrid.AddHandler(DragDrop.DropEvent, Grid_Drop_Top);

        BottomDropGrid.AddHandler(DragDrop.DragEnterEvent, Grid_DragEnter);
        BottomDropGrid.AddHandler(DragDrop.DragLeaveEvent, Grid_DragLeave);
        BottomDropGrid.AddHandler(DragDrop.DropEvent, Grid_Drop_Bottom);

        middleDropGrid.AddHandler(DragDrop.DragEnterEvent, Grid_CenterEnter);
        middleDropGrid.AddHandler(DragDrop.DragLeaveEvent, Grid_CenterLeave);
        middleDropGrid.AddHandler(DragDrop.DropEvent, Grid_Drop_Center);

        BackgroundGrid.AddHandler(DragDrop.DropEvent, BackgroundGrid_Drop);
        centerGrid.AddHandler(DragDrop.DropEvent, BackgroundGrid_Drop);

        DisableDropPanels();
    }

    private void DisableDropPanels()
    {
        TopDropGrid.IsVisible = false;
        middleDropGrid.IsVisible = false;
        BottomDropGrid.IsVisible = false;
    }


    private void BackgroundGrid_Drop(object sender, DragEventArgs e)
    {
        Guid[]? droppedGuids = LayerControl.ExtractMemberGuids(e.Data);
        if (droppedGuids != null && droppedGuids.Contains(Folder.Id))
        {
            e.Handled = true;
        }
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        mouseUpdateController?.Dispose();
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        mouseUpdateController = new MouseUpdateController(this, Manager.FolderControl_MouseMove);
    }

    private void Grid_DragEnter(object sender, DragEventArgs e)
    {
        Grid item = (Grid)sender;
        item.Background = highlightColor;
    }

    private void Grid_CenterEnter(object sender, DragEventArgs e)
    {
        centerGrid.Background = highlightColor;
    }

    private void Grid_DragLeave(object sender, DragEventArgs e)
    {
        Grid grid = (Grid)sender;
        LayerControl.RemoveDragEffect(grid);
    }

    private void Grid_CenterLeave(object sender, DragEventArgs e)
    {
        LayerControl.RemoveDragEffect(centerGrid);
    }

    private bool HandleDrop(IDataObject dataObj, StructureMemberPlacement placement)
    {
        DisableDropPanels();
        Guid[]? droppedGuids = LayerControl.ExtractMemberGuids(dataObj);
        if (droppedGuids is null)
            return false;

        var document = Folder.Document;
        if (placement is StructureMemberPlacement.Below or StructureMemberPlacement.BelowOutsideFolder
            or StructureMemberPlacement.Inside)
        {
            droppedGuids = droppedGuids.Reverse().ToArray();
        }

        using var block = document.Operations.StartChangeBlock();
        Guid lastMovedMember = Folder.Id;

        foreach (Guid memberGuid in droppedGuids)
        {
            document.Operations.MoveStructureMember(memberGuid, lastMovedMember,
                placement);
            lastMovedMember = memberGuid;
            if (placement == StructureMemberPlacement.Inside)
            {
                placement = StructureMemberPlacement.Below;
            }

            block.ExecuteQueuedActions();
        }

        return true;
    }

    private void Grid_Drop_Top(object sender, DragEventArgs e)
    {
        LayerControl.RemoveDragEffect((Grid)sender);
        e.Handled = HandleDrop(e.Data, StructureMemberPlacement.Above);
    }

    private void Grid_Drop_Center(object sender, DragEventArgs e)
    {
        LayerControl.RemoveDragEffect(centerGrid);
        e.Handled = HandleDrop(e.Data, StructureMemberPlacement.Inside);
    }

    private void Grid_Drop_Bottom(object sender, DragEventArgs e)
    {
        LayerControl.RemoveDragEffect((Grid)sender);
        e.Handled = HandleDrop(e.Data, StructureMemberPlacement.Below);
    }

    private void FolderControl_DragEnter(object sender, DragEventArgs e)
    {
        TopDropGrid.IsVisible = true;
        middleDropGrid.IsVisible = true;
        BottomDropGrid.IsVisible = true;
    }

    private void FolderControl_DragLeave(object sender, DragEventArgs e)
    {
        TopDropGrid.IsVisible = false;
        middleDropGrid.IsVisible = false;
        BottomDropGrid.IsVisible = false;
    }

    private void RenameMenuItem_Click(object sender, RoutedEventArgs e)
    {
        editableTextBlock.EnableEditing();
    }
}
