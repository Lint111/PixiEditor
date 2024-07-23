﻿using System.Reflection;
using PixiEditor.ChangeableDocument.Changeables.Graph.Context;
using PixiEditor.ChangeableDocument.Changeables.Graph.Interfaces;
using PixiEditor.ChangeableDocument.Changeables.Graph.Nodes;
using PixiEditor.ChangeableDocument.Changes.NodeGraph;

namespace PixiEditor.ChangeableDocument.Changeables.Graph;

public class InputProperty : IInputProperty
{
    private object _internalValue;
    private int _lastExecuteHash = -1;
    public string InternalPropertyName { get; }
    public string DisplayName { get; }

    public object? Value
    {
        get
        {
            if (Connection == null)
            {
                return _internalValue;
            }

            var connectionValue = Connection.Value;
            
            if (!ValueType.IsAssignableTo(typeof(Delegate)) && connectionValue is Delegate connectionField)
            {
                return connectionField.DynamicInvoke(FuncContext.NoContext);
            }

            if (ValueType.IsAssignableTo(typeof(Delegate)) && connectionValue is not Delegate)
            {
                return FuncFactory(connectionValue);
            }

            return connectionValue;
        }
    }
    
    public object NonOverridenValue
    {
        get => _internalValue;
        set
        {
            _internalValue = value;
        }
    }

    protected virtual object FuncFactory(object toReturn)
    {
        Func<FuncContext, object> func = _ => toReturn;
        return func;
    }

    public Node Node { get; }
    public Type ValueType { get; } 
    internal bool CacheChanged
    {
        get
        {
            if (Value is ICacheable cacheable)
            {
                return cacheable.GetCacheHash() != _lastExecuteHash;
            }

            if(Value is null)
            {
                return _lastExecuteHash != 0;
            }
            
            if(Value.GetType().IsValueType || Value.GetType() == typeof(string))
            {
                return Value.GetHashCode() != _lastExecuteHash;
            }

            return true;
        }
    }

    internal void UpdateCache()
    {
        if (Value is null)
        {
            _lastExecuteHash = 0;
        }
        else if (Value is ICacheable cacheable)
        {
            _lastExecuteHash = cacheable.GetCacheHash();
        }
        else
        {
            _lastExecuteHash = Value.GetHashCode();
        }
    }
    
    IReadOnlyNode INodeProperty.Node => Node;
    
    public IOutputProperty? Connection { get; set; }
    
    internal InputProperty(Node node, string internalName, string displayName, object defaultValue, Type valueType)
    {
        InternalPropertyName = internalName;
        DisplayName = displayName;
        _internalValue = defaultValue;
        Node = node;
        ValueType = valueType;
    }

    public InputProperty Clone(Node forNode)
    {
        if(NonOverridenValue is ICloneable cloneable)
            return new InputProperty(forNode, InternalPropertyName, DisplayName, cloneable.Clone(), ValueType);

        if (NonOverridenValue is Enum enumVal)
        {
            return new InputProperty(forNode, InternalPropertyName, DisplayName, enumVal, ValueType);
        }

        if (NonOverridenValue is null)
        {
            object? nullValue = null;
            if (ValueType.IsValueType)
            {
                nullValue = Activator.CreateInstance(ValueType);
            }
            
            return new InputProperty(forNode, InternalPropertyName, DisplayName, nullValue, ValueType);
        }
        
        if(!NonOverridenValue.GetType().IsValueType && NonOverridenValue.GetType() != typeof(string))
            throw new InvalidOperationException("Value is not cloneable and not a primitive type");
        
        return new InputProperty(forNode, InternalPropertyName, DisplayName, NonOverridenValue, ValueType);
    }
}


public class InputProperty<T> : InputProperty, IInputProperty<T>
{
    public new T Value
    {
        get
        {
            object value = base.Value;
            if (value is null) return default(T);
            
            if(value is T tValue)
                return tValue;

            return ConversionTable.TryConvert(value, typeof(T), out object result) ? (T)result : default;
        }
    }

    public T NonOverridenValue
    {
        get => (T)(base.NonOverridenValue ?? default(T));
        set => base.NonOverridenValue = value;
    }
    
    internal InputProperty(Node node, string internalName, string displayName, T defaultValue) : base(node, internalName, displayName, defaultValue, typeof(T))
    {
    }
}
