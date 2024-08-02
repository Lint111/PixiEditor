﻿using PixiEditor.DrawingApi.Core.Surfaces.Surface.Vector;

namespace PixiEditor.ChangeableDocument.Changeables.Interfaces;

public interface IReadOnlySelection
{
    /// <summary>
    /// The path of the selection
    /// </summary>
    public VectorPath SelectionPath { get; }
}
