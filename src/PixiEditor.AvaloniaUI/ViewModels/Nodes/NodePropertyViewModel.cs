﻿using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using PixiEditor.AvaloniaUI.Models.DocumentModels;
using PixiEditor.AvaloniaUI.Models.Handlers;
using PixiEditor.AvaloniaUI.ViewModels.Nodes.Properties;

namespace PixiEditor.AvaloniaUI.ViewModels.Nodes;

internal abstract class NodePropertyViewModel : ViewModelBase, INodePropertyHandler
{
    private string propertyName;
    private string displayName;
    private object? _value;
    private INodeHandler node;
    private bool isInput;
    private bool isFunc;
    private IBrush socketBrush;
    
    private ObservableCollection<INodePropertyHandler> connectedInputs = new();
    private INodePropertyHandler? connectedOutput;

    public string DisplayName
    {
        get => displayName;
        set => SetProperty(ref displayName, value);
    }
    
    public object? Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
            {
                ViewModelMain.Current.NodeGraphManager.UpdatePropertyValue((node, PropertyName, value));
            }
        }
    }
    
    public bool IsInput
    {
        get => isInput;
        set
        {
            if (SetProperty(ref isInput, value))
            {
                OnPropertyChanged(nameof(ShowInputField));
            }
        }
    }

    public bool IsFunc
    {
        get => isFunc;
        set => SetProperty(ref isFunc, value);
    }

    public INodePropertyHandler? ConnectedOutput
    {
        get => connectedOutput;
        set
        {
            if (SetProperty(ref connectedOutput, value))
            {
                OnPropertyChanged(nameof(ShowInputField));
            }
        }
    }

    public bool ShowInputField
    {
        get => IsInput && ConnectedOutput == null;
    }

    public ObservableCollection<INodePropertyHandler> ConnectedInputs
    {
        get => connectedInputs;
        set => SetProperty(ref connectedInputs, value);
    }

    public INodeHandler Node
    {
        get => node;
        set => SetProperty(ref node, value);
    }

    public string PropertyName
    {
        get => propertyName;
        set => SetProperty(ref propertyName, value);
    }
    
    public IBrush SocketBrush
    {
        get => socketBrush;
        set => SetProperty(ref socketBrush, value);
    }
    
    public Type PropertyType { get; }

    public NodePropertyViewModel(INodeHandler node, Type propertyType)
    {
        Node = node;
        PropertyType = propertyType;
        var targetType = propertyType;

        if (propertyType.IsAssignableTo(typeof(Delegate)))
        {
            targetType = propertyType.GetMethod("Invoke").ReturnType;
        }

        if (Application.Current.Styles.TryGetResource($"{targetType.Name}SocketBrush", App.Current.ActualThemeVariant, out object brush))
        {
            if (brush is IBrush brushValue)
            {
                SocketBrush = brushValue;
            }
        }
        
        if(SocketBrush == null)
        {
            if(Application.Current.Styles.TryGetResource($"DefaultSocketBrush", App.Current.ActualThemeVariant, out object defaultBrush))
            {
                if (defaultBrush is IBrush defaultBrushValue)
                {
                    SocketBrush = defaultBrushValue;
                }
            }
        }
    }

    public static NodePropertyViewModel? CreateFromType(Type type, INodeHandler node)
    {
        Type propertyType = type;
        
        if (type.IsAssignableTo(typeof(Delegate)))
        {
            propertyType = type.GetMethod("Invoke").ReturnType;
        }
        
        string name = $"{propertyType.Name}PropertyViewModel";
        
        Type viewModelType = Type.GetType($"PixiEditor.AvaloniaUI.ViewModels.Nodes.Properties.{name}");
        if (viewModelType == null)
        {
            if (propertyType.IsEnum)
            {
                return new GenericEnumPropertyViewModel(node, type, propertyType);
            }
            
            return new GenericPropertyViewModel(node, type);
        }
        
        return (NodePropertyViewModel)Activator.CreateInstance(viewModelType, node, type);
    }

    public void InternalSetValue(object? value) => SetProperty(ref _value, value, nameof(Value));
}

internal abstract class NodePropertyViewModel<T> : NodePropertyViewModel
{
    public new T Value
    {
        get
        {
            if (base.Value == null)
                return default;

            return (T)base.Value;
        }
        set => base.Value = value;
    }
    
    public NodePropertyViewModel(NodeViewModel node, Type valueType) : base(node, valueType)
    {
    }
}
