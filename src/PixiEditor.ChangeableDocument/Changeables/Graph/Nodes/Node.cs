﻿using System.Diagnostics;
using PixiEditor.ChangeableDocument.Changeables.Animations;
using PixiEditor.ChangeableDocument.Changeables.Graph.Context;
using PixiEditor.ChangeableDocument.Changeables.Graph.Interfaces;
using PixiEditor.ChangeableDocument.Rendering;
using PixiEditor.Numerics;

namespace PixiEditor.ChangeableDocument.Changeables.Graph.Nodes;

[DebuggerDisplay("Type = {GetType().Name}")]
public abstract class Node : IReadOnlyNode, IDisposable
{
    private List<InputProperty> inputs = new();
    private List<OutputProperty> outputs = new();

    private List<IReadOnlyNode> _connectedNodes = new();

    public Guid Id { get; internal set; } = Guid.NewGuid();

    public IReadOnlyCollection<InputProperty> InputProperties => inputs;
    public IReadOnlyCollection<OutputProperty> OutputProperties => outputs;
    public IReadOnlyCollection<IReadOnlyNode> ConnectedOutputNodes => _connectedNodes;
    public ChunkyImage? CachedResult { get; private set; }

    public virtual string InternalName { get; }

    protected virtual bool AffectedByAnimation { get; }

    protected virtual bool AffectedByChunkResolution { get; }

    protected virtual bool AffectedByChunkToUpdate { get; }

    protected Node()
    {
        InternalName = $"PixiEditor.{GetType().Name}";
    }

    IReadOnlyCollection<IInputProperty> IReadOnlyNode.InputProperties => inputs;
    IReadOnlyCollection<IOutputProperty> IReadOnlyNode.OutputProperties => outputs;
    public VecD Position { get; set; }

    private KeyFrameTime _lastFrameTime = new KeyFrameTime(-1);
    private ChunkResolution? _lastResolution;
    private VecI? _lastChunkPos;
    private ChunkyImage _cache;
    private Chunk? lastRenderedChunk;

    public Chunk? Execute(RenderingContext context)
    {
        var result = ExecuteInternal(context);
        
        // copy the result to avoid leaking the internal chunks, that are mostly caches used by nodes.
        Chunk returnCopy = Chunk.Create(context.ChunkResolution);
        returnCopy.Surface.DrawingSurface.Canvas.DrawSurface(result.Surface.DrawingSurface, 0, 0);
        
        return returnCopy;
    }

    internal Chunk ExecuteInternal(RenderingContext context)
    {
        if (CachedResult == null)
        {
            CachedResult = new ChunkyImage(context.DocumentSize);
        }

        if (!CacheChanged(context))
        {
            return lastRenderedChunk;
        }

        lastRenderedChunk = OnExecute(context);

        CachedResult.SetCommitedChunk(lastRenderedChunk, context.ChunkToUpdate, context.ChunkResolution);

        UpdateCache(context);
        return lastRenderedChunk;
    }

    protected abstract Chunk? OnExecute(RenderingContext context);
    public abstract bool Validate();

    protected virtual bool CacheChanged(RenderingContext context)
    {
        return (!context.FrameTime.Equals(_lastFrameTime) && AffectedByAnimation)
               || (context.ChunkResolution != _lastResolution /*&& AffectedByChunkResolution*/)
               || (context.ChunkToUpdate != _lastChunkPos /*&& AffectedByChunkToUpdate*/)
               || inputs.Any(x => x.CacheChanged);
    }

    protected virtual void UpdateCache(RenderingContext context)
    {
        foreach (var input in inputs)
        {
            input.UpdateCache();
        }

        _lastFrameTime = context.FrameTime;
        _lastResolution = context.ChunkResolution;
        _lastChunkPos = context.ChunkToUpdate;
    }

    public void RemoveKeyFrame(Guid keyFrameGuid)
    {
        // TODO: Implement
    }

    public void SetKeyFrameLength(Guid keyFrameGuid, int startFrame, int duration)
    {
        // TODO: Implement
    }

    public void AddFrame<T>(Guid keyFrameGuid, int startFrame, int duration, T value)
    {
        // TODO: Implement
    }

    public void TraverseBackwards(Func<IReadOnlyNode, bool> action)
    {
        var visited = new HashSet<IReadOnlyNode>();
        var queueNodes = new Queue<IReadOnlyNode>();
        queueNodes.Enqueue(this);

        while (queueNodes.Count > 0)
        {
            var node = queueNodes.Dequeue();

            if (!visited.Add(node))
            {
                continue;
            }

            if (!action(node))
            {
                return;
            }

            foreach (var inputProperty in node.InputProperties)
            {
                if (inputProperty.Connection != null)
                {
                    queueNodes.Enqueue(inputProperty.Node);
                }
            }
        }
    }

    public void TraverseForwards(Func<IReadOnlyNode, bool> action)
    {
        var visited = new HashSet<IReadOnlyNode>();
        var queueNodes = new Queue<IReadOnlyNode>();
        queueNodes.Enqueue(this);

        while (queueNodes.Count > 0)
        {
            var node = queueNodes.Dequeue();

            if (!visited.Add(node))
            {
                continue;
            }

            if (!action(node))
            {
                return;
            }

            foreach (var outputProperty in node.OutputProperties)
            {
                foreach (var outputNode in ConnectedOutputNodes)
                {
                    queueNodes.Enqueue(outputNode);
                }
            }
        }
    }

    protected FieldInputProperty<T> CreateFieldInput<T>(string propName, string displayName, T defaultValue)
    {
        var property = new FieldInputProperty<T>(this, propName, displayName, defaultValue);
        if (InputProperties.Any(x => x.InternalPropertyName == propName))
        {
            throw new InvalidOperationException($"Input with name {propName} already exists.");
        }

        inputs.Add(property);
        return property;
    }

    protected InputProperty<T> CreateInput<T>(string propName, string displayName, T defaultValue)
    {
        var property = new InputProperty<T>(this, propName, displayName, defaultValue);
        if (InputProperties.Any(x => x.InternalPropertyName == propName))
        {
            throw new InvalidOperationException($"Input with name {propName} already exists.");
        }

        inputs.Add(property);
        return property;
    }

    protected FieldOutputProperty<T> CreateFieldOutput<T>(string propName, string displayName,
        Func<FieldContext, T> defaultFunc)
    {
        var property = new FieldOutputProperty<T>(this, propName, displayName, defaultFunc);
        outputs.Add(property);
        property.Connected += (input, _) => _connectedNodes.Add(input.Node);
        property.Disconnected += (input, _) => _connectedNodes.Remove(input.Node);
        return property;
    }

    protected OutputProperty<T> CreateOutput<T>(string propName, string displayName, T defaultValue)
    {
        var property = new OutputProperty<T>(this, propName, displayName, defaultValue);
        outputs.Add(property);
        property.Connected += (input, _) => _connectedNodes.Add(input.Node);
        property.Disconnected += (input, _) => _connectedNodes.Remove(input.Node);
        return property;
    }

    public virtual void Dispose()
    {
        foreach (var input in inputs)
        {
            if (input is { Connection: null, Value: IDisposable disposable })
            {
                disposable.Dispose();
            }
        }

        foreach (var output in outputs)
        {
            if (output.Value is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        CachedResult?.Dispose();
    }

    public abstract Node CreateCopy();

    public Node Clone()
    {
        var clone = CreateCopy();
        clone.Id = Guid.NewGuid();
        clone.inputs = new List<InputProperty>();
        clone.outputs = new List<OutputProperty>();
        clone._connectedNodes = new List<IReadOnlyNode>();
        foreach (var input in inputs)
        {
            var newInput = input.Clone(clone);
            clone.inputs.Add(newInput);
        }

        foreach (var output in outputs)
        {
            var newOutput = output.Clone(clone);
            clone.outputs.Add(newOutput);
        }

        return clone;
    }

    public InputProperty? GetInputProperty(string inputProperty)
    {
        return inputs.FirstOrDefault(x => x.InternalPropertyName == inputProperty);
    }

    public OutputProperty? GetOutputProperty(string outputProperty)
    {
        return outputs.FirstOrDefault(x => x.InternalPropertyName == outputProperty);
    }
}
