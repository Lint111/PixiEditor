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
        var node = target.NodeGraph.Nodes.First(x => x.Id == _nodeId);
        var property = node.GetInputProperty(_propertyName);

        int inputsHash = CalculateInputsHash(node);
        int outputsHash = CalculateOutputsHash(node);

        previousValue = GetValue(property);
        string errors = string.Empty;
        if (!property.Validator.Validate(_value, out errors))
        {
            if (string.IsNullOrEmpty(errors))
            {
                _value = property.Validator.GetClosestValidValue(_value);
                if (_value == previousValue)
                {
                    ignoreInUndo = true;
                }
            }

            _value = SetValue(property, _value);
            ignoreInUndo = false;
        }
        else
        {
            _value = SetValue(property, _value);
            ignoreInUndo = false;
        }

        List<IChangeInfo> changes = new();
        changes.Add(new PropertyValueUpdated_ChangeInfo(_nodeId, _propertyName, _value) { Errors = errors });

        int newInputsHash = CalculateInputsHash(node);
        int newOutputsHash = CalculateOutputsHash(node);

        if (inputsHash != newInputsHash)
        {
            changes.Add(NodeInputsChanged_ChangeInfo.FromNode(node));
        }

        if (outputsHash != newOutputsHash)
        {
            changes.Add(NodeOutputsChanged_ChangeInfo.FromNode(node));
        }

        return changes;
    }

    public override OneOf<None, IChangeInfo, List<IChangeInfo>> Revert(Document target)
    {
        var node = target.NodeGraph.Nodes.First(x => x.Id == _nodeId);
        var property = node.GetInputProperty(_propertyName);

        int inputsHash = CalculateInputsHash(node);
        int outputsHash = CalculateOutputsHash(node);

        SetValue(property, previousValue);

        List<IChangeInfo> changes = new();

        changes.Add(new PropertyValueUpdated_ChangeInfo(_nodeId, _propertyName, previousValue));

        int newInputsHash = CalculateInputsHash(node);
        int newOutputsHash = CalculateOutputsHash(node);

        if (inputsHash != newInputsHash)
        {
            changes.Add(NodeInputsChanged_ChangeInfo.FromNode(node));
        }

        if (outputsHash != newOutputsHash)
        {
            changes.Add(NodeOutputsChanged_ChangeInfo.FromNode(node));
        }

        return changes;
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

    private static int CalculateInputsHash(Node node)
    {
        HashCode hash = new();
        foreach (var input in node.InputProperties)
        {
            hash.Add(input.InternalPropertyName);
            hash.Add(input.ValueType);
        }

        return hash.ToHashCode();
    }

    private static int CalculateOutputsHash(Node node)
    {
        HashCode hash = new();
        foreach (var output in node.OutputProperties)
        {
            hash.Add(output.InternalPropertyName);
            hash.Add(output.ValueType);
        }

        return hash.ToHashCode();
    }
}
