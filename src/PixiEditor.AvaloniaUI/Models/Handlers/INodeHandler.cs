﻿using System.Collections.ObjectModel;
using System.ComponentModel;
using ChunkyImageLib;
using PixiEditor.AvaloniaUI.Models.Structures;
using PixiEditor.ChangeableDocument.Changeables.Graph.Interfaces;
using PixiEditor.Numerics;

namespace PixiEditor.AvaloniaUI.Models.Handlers;

public interface INodeHandler : INotifyPropertyChanged
{
    public Guid Id { get; }
    public string NodeName { get; set; }
    public string InternalName { get; }
    public ObservableRangeCollection<INodePropertyHandler> Inputs { get; }
    public ObservableRangeCollection<INodePropertyHandler> Outputs { get; }
    public Surface ResultPreview { get; set; }
    public VecD PositionBindable { get; set; }
    public bool IsSelected { get; set; }
    public void TraverseBackwards(Func<INodeHandler, bool> func);
    public void TraverseBackwards(Func<INodeHandler, INodeHandler, bool> func);
    public void TraverseBackwards(Func<INodeHandler, INodeHandler, INodePropertyHandler, bool> func);
    public void TraverseForwards(Func<INodeHandler, bool> func);
    public void TraverseForwards(Func<INodeHandler, INodeHandler, bool> func);
    public void TraverseForwards(Func<INodeHandler, INodeHandler, INodePropertyHandler, bool> func);
}
