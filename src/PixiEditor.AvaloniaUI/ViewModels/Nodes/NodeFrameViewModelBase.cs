﻿using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PixiEditor.AvaloniaUI.Models.Handlers;
using PixiEditor.Numerics;

namespace PixiEditor.AvaloniaUI.ViewModels.Nodes;

public abstract class NodeFrameViewModelBase : ObservableObject
{
    private Guid id;
    private VecD topLeft;
    private VecD bottomRight;
    private VecD size;
    
    public ObservableCollection<INodeHandler> Nodes { get; }

    public string InternalName { get; init; }
    
    public Guid Id
    {
        get => id;
        set => SetProperty(ref id, value);
    }
    
    public VecD TopLeft
    {
        get => topLeft;
        set => SetProperty(ref topLeft, value);
    }

    public VecD BottomRight
    {
        get => bottomRight;
        set => SetProperty(ref bottomRight, value);
    }

    public VecD Size
    {
        get => size;
        set => SetProperty(ref size, value);
    }

    public NodeFrameViewModelBase(Guid id, IEnumerable<INodeHandler> nodes)
    {
        Id = id;
        Nodes = new ObservableCollection<INodeHandler>(nodes);

        Nodes.CollectionChanged += OnCollectionChanged;
        AddHandlers(Nodes);
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var action = e.Action;
        if (action != NotifyCollectionChangedAction.Add && action != NotifyCollectionChangedAction.Remove && action != NotifyCollectionChangedAction.Replace && action != NotifyCollectionChangedAction.Reset)
        {
            return;
        }
        
        AddHandlers((IEnumerable<NodeViewModel>)e.NewItems);
        RemoveHandlers((IEnumerable<NodeViewModel>)e.OldItems);
    }

    private void AddHandlers(IEnumerable<INodeHandler> nodes)
    {
        foreach (var node in nodes)
        {
            node.PropertyChanged += NodePropertyChanged;
        }
    }

    private void RemoveHandlers(IEnumerable<INodeHandler> nodes)
    {
        foreach (var node in nodes)
        {
            node.PropertyChanged -= NodePropertyChanged;
        }
    }

    private void NodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(INodeHandler.PositionBindable))
        {
            return;
        }
        
        CalculateBounds();
    }

    protected abstract void CalculateBounds();
}
