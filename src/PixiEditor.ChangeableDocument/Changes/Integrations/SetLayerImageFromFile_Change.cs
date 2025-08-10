using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PixiEditor.ChangeableDocument.Changeables.Graph.Nodes;

namespace PixiEditor.ChangeableDocument.Changes.Integrations;
internal class SetLayerImageFromFile_Change : Change
{
    private readonly Guid layerNodeId;
    private readonly string filePath;
    private readonly bool ignore;

    [GenerateMakeChangeAction]
    public SetLayerImageFromFile_Change(Guid layerNodeId, string filePath, bool ignore = true)
    {
        this.layerNodeId = layerNodeId;
        this.filePath = filePath;
        this.ignore = ignore;
    }

    public override bool InitializeAndValidate(Document doc)
    {
        return File.Exists(filePath) && doc.TryFindNode<LayerNode>(layerNodeId, out _);
    }

    public override OneOf<None, IChangeInfo, List<IChangeInfo>> Apply(Document doc, bool firstApply,out bool ignoreInUndo)
    {
        ignoreInUndo = ignore;
        
        if(!doc.TryFindNode<LayerNode>(layerNodeId,out var layer)) return new None();

        using var decoded = 
    }

    public override OneOf<None, IChangeInfo, List<IChangeInfo>> Revert(Document target)=> new None();
}
