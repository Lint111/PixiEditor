﻿using System.Collections.Generic;
using PixiEditor.Models.Handlers;

namespace PixiEditor.Models.DocumentModels.Public;
#nullable enable
internal class DocumentStructureModule
{
    private readonly IDocument doc;

    public DocumentStructureModule(IDocument owner)
    {
        this.doc = owner;
    }

    public IStructureMemberHandler FindOrThrow(Guid guid) => Find(guid) ??
                                                             throw new ArgumentException(
                                                                 "Could not find member with guid " + guid.ToString());

    public IStructureMemberHandler? Find(Guid guid)
    {
        return FindNode<IStructureMemberHandler>(guid);
    }

    public T? FindNode<T>(Guid guid) where T : class, INodeHandler
    {
        return doc.NodeGraphHandler.AllNodes.FirstOrDefault(x => x.Id == guid && x is T) as T;
    }

    public bool TryFindNode<T>(Guid guid, out T found) where T : class, INodeHandler
    {
        found = FindNode<T>(guid);
        return found != null;
    }

    public Guid FindClosestMember(IReadOnlyList<Guid> guids)
    {
        IStructureMemberHandler? firstNode = FindNode<IStructureMemberHandler>(guids[0]);
        if (firstNode is null)
            return Guid.Empty;

        INodeHandler? parent = null;

        firstNode.TraverseForwards(traversedNode =>
        {
            if (!guids.Contains(traversedNode.Id) && traversedNode is IStructureMemberHandler)
            {
                parent = traversedNode;
                return false;
            }

            return true;
        });

        if (parent is null)
        {
            var lastNode = FindNode<IStructureMemberHandler>(guids[^1]);
            if (lastNode is null)
                return Guid.Empty;

            lastNode.TraverseBackwards(traversedNode =>
            {
                if (!guids.Contains(traversedNode.Id) && traversedNode is IStructureMemberHandler)
                {
                    parent = traversedNode;
                    return false;
                }

                return true;
            });
        }

        if (parent is null)
            return Guid.Empty;

        return parent.Id;
    }

    public INodeHandler? FindFirstWhere(Predicate<INodeHandler> predicate)
    {
        return FindFirstWhere(predicate, doc.NodeGraphHandler);
    }

    private INodeHandler? FindFirstWhere(
        Predicate<INodeHandler> predicate,
        INodeGraphHandler graphVM)
    {
        INodeHandler? result = null;
        graphVM.TryTraverse(node =>
        {
            if (predicate(node))
            {
                result = node;
                return false;
            }

            return true;
        });

        return result;
    }

    public (IStructureMemberHandler?, INodeHandler?) FindChildAndParent(Guid childGuid)
    {
        List<IStructureMemberHandler>? path = FindPath(childGuid);
        return path.Count switch
        {
            0 => (null, null),
            1 => (path[0], null),
            >= 2 => (path[0], path[1]),
            _ => (null, null),
        };
    }

    public (IStructureMemberHandler, IFolderHandler) FindChildAndParentOrThrow(Guid childGuid)
    {
        List<IStructureMemberHandler>? path = FindPath(childGuid);
        if (path.Count < 2)
            throw new ArgumentException("Couldn't find child and parent");
        return (path[0], (IFolderHandler)path[1]);
    }

    public List<IStructureMemberHandler> FindPath(Guid guid)
    {
        List<INodeHandler>? list = new List<INodeHandler>();
        var targetNode = FindNode<INodeHandler>(guid);
        if (targetNode == null) return [];
        FillPath(targetNode, list);
        return list.Cast<IStructureMemberHandler>().ToList();
    }

    /// <summary>
    ///     Returns all layers in the document.
    /// </summary>
    /// <returns>List of ILayerHandlers. Empty if no layers found.</returns>
    public List<ILayerHandler> GetAllLayers(bool includeFoldersWithMask = false)
    {
        List<ILayerHandler> layers = new List<ILayerHandler>();

        doc.NodeGraphHandler.TryTraverse(node =>
        {
            if (node is ILayerHandler layer)
                layers.Add(layer);
            return true;
        });

        return layers;
    }

    public List<IStructureMemberHandler> GetAllMembers()
    {
        List<IStructureMemberHandler> members = new List<IStructureMemberHandler>();

        doc.NodeGraphHandler.TryTraverse(node =>
        {
            if (node is IStructureMemberHandler member)
                members.Add(member);
            return true;
        });

        return members;
    }

    private void FillPath(INodeHandler node, List<INodeHandler> toFill)
    {
        node.TraverseForwards(newNode =>
        {
            if (newNode is IStructureMemberHandler strNode)
            {
                toFill.Add(strNode);
            }

            return true;
        });
    }

    public INodeHandler? GetFirstForwardNode(INodeHandler startNode)
    {
        INodeHandler? result = null;
        startNode.TraverseForwards(node =>
        {
            if (node == startNode)
                return true;

            result = node;
            return false;
        });

        return result;
    }

    public IStructureMemberHandler? GetAboveMember(Guid memberId, bool includeFolders)
    {
        INodeHandler member = FindNode<INodeHandler>(memberId);
        if (member == null)
            return null;

        IStructureMemberHandler? result = null;
        member.TraverseForwards(node =>
        {
            if (node != member && node is IStructureMemberHandler structureMemberNode)
            {
                if (node is IFolderHandler && !includeFolders)
                    return true;

                result = structureMemberNode;
                return false;
            }

            return true;
        });

        return result;
    }

    public IStructureMemberHandler? GetBelowMember(Guid memberId, bool includeFolders)
    {
        INodeHandler member = FindNode<INodeHandler>(memberId);
        if (member == null)
            return null;

        IStructureMemberHandler? result = null;
        member.TraverseBackwards(node =>
        {
            if (node != member && node is IStructureMemberHandler structureMemberNode)
            {
                if (node is IFolderHandler && !includeFolders)
                    return true;

                result = structureMemberNode;
                return false;
            }

            return true;
        });

        return result;
    }
}
