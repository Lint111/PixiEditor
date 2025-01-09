﻿using PixiEditor.ChangeableDocument.Changeables.Graph;
using PixiEditor.ChangeableDocument.Changeables.Graph.Nodes;
using PixiEditor.ChangeableDocument.ChangeInfos.NodeGraph;
using PixiEditor.ChangeableDocument.ChangeInfos.Structure;
using PixiEditor.ChangeableDocument.Changes.NodeGraph;

namespace PixiEditor.ChangeableDocument.Changes.Structure;

internal class DuplicateFolder_Change : Change
{
    private readonly Guid folderGuid;
    private Guid duplicateGuid;
    private Guid[] contentGuids;
    private Guid[] contentDuplicateGuids;

    private ConnectionsData? connectionsData;
    private Dictionary<Guid, ConnectionsData> contentConnectionsData = new();

    [GenerateMakeChangeAction]
    public DuplicateFolder_Change(Guid folderGuid, Guid newGuid)
    {
        this.folderGuid = folderGuid;
        duplicateGuid = newGuid;
    }

    public override bool InitializeAndValidate(Document target)
    {
        if (!target.TryFindMember<FolderNode>(folderGuid, out FolderNode? folder))
            return false;

        connectionsData = NodeOperations.CreateConnectionsData(folder);

        List<Guid> contentGuidList = new();

        folder.Content.Connection?.Node.TraverseBackwards(x =>
        {
            contentGuidList.Add(x.Id);
            contentConnectionsData[x.Id] = NodeOperations.CreateConnectionsData(x);
            return true;
        });

        return true;
    }

    public override OneOf<None, IChangeInfo, List<IChangeInfo>> Apply(Document target, bool firstApply,
        out bool ignoreInUndo)
    {
        (FolderNode existingLayer, Node parent) = ((FolderNode, Node))target.FindChildAndParentOrThrow(folderGuid);

        FolderNode clone = (FolderNode)existingLayer.Clone();
        clone.Id = duplicateGuid;

        InputProperty<Painter?> targetInput = parent.InputProperties.FirstOrDefault(x =>
            x.ValueType == typeof(Painter) &&
            x.Connection is { Node: StructureNode }) as InputProperty<Painter?>;

        List<IChangeInfo> operations = new();

        target.NodeGraph.AddNode(clone);
        
        operations.Add(CreateNode_ChangeInfo.CreateFromNode(clone));
        operations.AddRange(NodeOperations.AppendMember(targetInput, clone.Output, clone.Background, clone.Id));

        DuplicateContent(target, clone, existingLayer, operations);
        
        ignoreInUndo = false;

        return operations;
    }

    public override OneOf<None, IChangeInfo, List<IChangeInfo>> Revert(Document target)
    {
        var (member, parent) = target.FindChildAndParentOrThrow(duplicateGuid);

        target.NodeGraph.RemoveNode(member);
        member.Dispose();

        List<IChangeInfo> changes = new();

        changes.AddRange(NodeOperations.DetachStructureNode(member));
        changes.Add(new DeleteStructureMember_ChangeInfo(member.Id));

        if (contentDuplicateGuids is not null)
        {
            foreach (Guid contentGuid in contentDuplicateGuids)
            {
                Node contentNode = target.FindNodeOrThrow<Node>(contentGuid);
                changes.AddRange(NodeOperations.DetachNode(target.NodeGraph, contentNode));
                changes.Add(new DeleteNode_ChangeInfo(contentNode.Id));
                
                target.NodeGraph.RemoveNode(contentNode);
                contentNode.Dispose();
            }
        }

        if (connectionsData is not null)
        {
            Node originalNode = target.FindNodeOrThrow<Node>(folderGuid);
            changes.AddRange(
                NodeOperations.ConnectStructureNodeProperties(connectionsData, originalNode, target.NodeGraph));
        }

        return changes;
    }

    private void DuplicateContent(Document target, FolderNode clone, FolderNode existingLayer,
        List<IChangeInfo> operations)
    {
        Dictionary<Guid, Guid> nodeMap = new Dictionary<Guid, Guid>();

        nodeMap[existingLayer.Id] = clone.Id;
        List<Guid> contentGuidList = new();

        existingLayer.Content.Connection?.Node.TraverseBackwards(x =>
        {
            if (x is not Node targetNode)
                return false;

            Node? node = targetNode.Clone();
            nodeMap[x.Id] = node.Id;
            contentGuidList.Add(node.Id);

            target.NodeGraph.AddNode(node);

            operations.Add(CreateNode_ChangeInfo.CreateFromNode(node));
            return true;
        });

        foreach (var data in contentConnectionsData)
        {
            var updatedData = data.Value.WithUpdatedIds(nodeMap);
            Guid targetNodeId = nodeMap[data.Key];
            operations.AddRange(NodeOperations.ConnectStructureNodeProperties(updatedData,
                target.FindNodeOrThrow<Node>(targetNodeId), target.NodeGraph));
        }
        
        contentDuplicateGuids = contentGuidList.ToArray();
    }
}
