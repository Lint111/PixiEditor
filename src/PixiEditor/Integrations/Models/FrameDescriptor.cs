using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PixiEditor.Integrations.Models;

public enum IngestMode { Layer,NodeGraph}

public sealed record FrameDescriptor(
    string ColorPath,
    string? NormalPath,
    int FrameIndex,
    int Width,
    int Height,
    IReadOnlyDictionary<string, string> Metadata = null
    );
