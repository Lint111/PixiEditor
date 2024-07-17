﻿using PixiEditor.ChangeableDocument.Changeables.Animations;
using PixiEditor.ChangeableDocument.Changeables.Graph.Interfaces;
using PixiEditor.ChangeableDocument.Rendering;
using PixiEditor.DrawingApi.Core.Surface.ImageData;

namespace PixiEditor.ChangeableDocument.Changeables.Graph.Nodes;

public class OutputNode : Node, IBackgroundInput
{
    public InputProperty<Chunk?> Input { get; } 
    public OutputNode()
    {
        Input = CreateInput<Chunk>("Background", "INPUT", null);
    }
    
    public override bool Validate()
    {
        return Input.Connection != null;
    }

    public override Node CreateCopy()
    {
        return new OutputNode();
    }

    protected override Chunk? OnExecute(RenderingContext context)
    {
        return Input.Value;
    }

    InputProperty<Chunk?> IBackgroundInput.Background => Input;
}
