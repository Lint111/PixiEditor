﻿using PixiEditor.ChangeableDocument.Changeables.Graph;
using PixiEditor.ChangeableDocument.Changeables.Graph.Nodes;
using PixiEditor.ChangeableDocument.ChangeInfos.NodeGraph;

namespace PixiEditor.ChangeableDocument.Changes.NodeGraph;

internal class UpdatePropertyValue_Change : Change
{
    private readonly Guid _nodeId;
    private readonly string _propertyName;
    private object? _value;
    private object? previousValue;

    [GenerateMakeChangeAction]
    public UpdatePropertyValue_Change(Guid nodeId, string property, object? value)
    {
        _nodeId = nodeId;
        _propertyName = property;
        _value = value;
    }

    public override bool InitializeAndValidate(Document target)
    {
        if (target.TryFindNode<Node>(_nodeId, out var node))
        {
            return node.HasInputProperty(_propertyName);
        }

        return false;
    }

    public override OneOf<None, IChangeInfo, List<IChangeInfo>> Apply(Document target, bool firstApply,
        out bool ignoreInUndo)
    {
        var node = target.RenderNodeGraph.Nodes.First(x => x.Id == _nodeId);
        var property = node.GetInputProperty(_propertyName);

        previousValue = GetValue(property);
        if (!property.Validator.Validate(_value))
        {
            _value = property.Validator.GetClosestValidValue(_value);
            if (_value == previousValue)
            {
                ignoreInUndo = true;
            }
            
            ignoreInUndo = false;
        }
        else
        {
            _value = SetValue(property, _value);
            ignoreInUndo = false;
        }

        return new PropertyValueUpdated_ChangeInfo(_nodeId, _propertyName, _value);
    }

    public override OneOf<None, IChangeInfo, List<IChangeInfo>> Revert(Document target)
    {
        var node = target.RenderNodeGraph.Nodes.First(x => x.Id == _nodeId);
        var property = node.GetInputProperty(_propertyName);
        SetValue(property, previousValue);

        return new PropertyValueUpdated_ChangeInfo(_nodeId, _propertyName, previousValue);
    }

    private static object SetValue(InputProperty property, object? value)
    {
        if (property is IFuncInputProperty fieldInput)
        {
            fieldInput.SetFuncConstantValue(value);
        }
        else
        {
            if (value is int && property.ValueType.IsEnum)
            {
                value = Enum.ToObject(property.ValueType, value);
            }

            property.NonOverridenValue = value;
        }

        return value;
    }


    private static object? GetValue(InputProperty property)
    {
        if (property is IFuncInputProperty fieldInput)
        {
            return fieldInput.GetFuncConstantValue();
        }

        return property.NonOverridenValue;
    }
}
