﻿#nullable enable

using PixiEditor.ChangeableDocument.Changeables.Animations;
using PixiEditor.ChangeableDocument.Changeables.Graph.Interfaces;
using PixiEditor.ChangeableDocument.Changeables.Graph.Nodes;
using Drawie.Backend.Core.Surfaces.ImageData;
using PixiEditor.Helpers;
using PixiEditor.Models.DocumentModels;
using PixiEditor.Models.Handlers;
using Drawie.Numerics;

namespace PixiEditor.Models.Rendering;

internal class MemberPreviewUpdater
{
    private readonly IDocument doc;
    private readonly DocumentInternalParts internals;

    private AnimationKeyFramePreviewRenderer AnimationKeyFramePreviewRenderer { get; }

    public MemberPreviewUpdater(IDocument doc, DocumentInternalParts internals)
    {
        this.doc = doc;
        this.internals = internals;
        AnimationKeyFramePreviewRenderer = new AnimationKeyFramePreviewRenderer(internals);
    }

    public void UpdatePreviews(HashSet<Guid> membersToUpdate,
        HashSet<Guid> masksToUpdate, HashSet<Guid> nodesToUpdate, HashSet<Guid> keyFramesToUpdate)
    {
        if (!membersToUpdate.Any() && !masksToUpdate.Any() && !nodesToUpdate.Any() &&
            !keyFramesToUpdate.Any())
            return;

        UpdatePreviewPainters(membersToUpdate, masksToUpdate, nodesToUpdate, keyFramesToUpdate);
    }

    /// <summary>
    /// Re-renders changed chunks using <see cref="mainPreviewAreasAccumulator"/> and <see cref="maskPreviewAreasAccumulator"/> along with the passed lists of bitmaps that need full re-render.
    /// </summary>
    /// <param name="members">Members that should be rendered</param>
    /// <param name="masksToUpdate">Masks that should be rendered</param>
    private void UpdatePreviewPainters(HashSet<Guid> members, HashSet<Guid> masksToUpdate,
        HashSet<Guid> nodesToUpdate, HashSet<Guid> keyFramesToUpdate)
    {
        RenderWholeCanvasPreview();
        RenderLayersPreview(members);
        RenderMaskPreviews(masksToUpdate);

        RenderAnimationPreviews(members, keyFramesToUpdate);

        RenderNodePreviews(nodesToUpdate);
    }

    /// <summary>
    /// Re-renders the preview of the whole canvas which is shown as the tab icon
    /// </summary>
    private void RenderWholeCanvasPreview()
    {
        var previewSize = StructureHelpers.CalculatePreviewSize(internals.Tracker.Document.Size);
        //float scaling = (float)previewSize.X / doc.SizeBindable.X;

        if (doc.PreviewPainter == null)
        {
            doc.PreviewPainter = new PreviewPainter(doc.Renderer, doc.Renderer, doc.AnimationHandler.ActiveFrameTime,
                doc.SizeBindable, internals.Tracker.Document.ProcessingColorSpace);
        }

        doc.PreviewPainter.DocumentSize = doc.SizeBindable;
        doc.PreviewPainter.ProcessingColorSpace = internals.Tracker.Document.ProcessingColorSpace;
        doc.PreviewPainter.FrameTime = doc.AnimationHandler.ActiveFrameTime;
        doc.PreviewPainter.Repaint();
    }

    private void RenderLayersPreview(HashSet<Guid> memberGuids)
    {
        foreach (var node in doc.NodeGraphHandler.AllNodes)
        {
            if (node is IStructureMemberHandler structureMemberHandler)
            {
                if (!memberGuids.Contains(node.Id))
                    continue;

                if (structureMemberHandler.PreviewPainter == null)
                {
                    var member = internals.Tracker.Document.FindMember(node.Id);
                    if (member is not IPreviewRenderable previewRenderable)
                        continue;

                    structureMemberHandler.PreviewPainter =
                        new PreviewPainter(doc.Renderer, previewRenderable,
                            doc.AnimationHandler.ActiveFrameTime, doc.SizeBindable,
                            internals.Tracker.Document.ProcessingColorSpace);
                    structureMemberHandler.PreviewPainter.Repaint();
                }
                else
                {
                    structureMemberHandler.PreviewPainter.FrameTime = doc.AnimationHandler.ActiveFrameTime;
                    structureMemberHandler.PreviewPainter.DocumentSize = doc.SizeBindable;
                    structureMemberHandler.PreviewPainter.ProcessingColorSpace =
                        internals.Tracker.Document.ProcessingColorSpace;

                    structureMemberHandler.PreviewPainter.Repaint();
                }
            }
        }
    }

    private void RenderAnimationPreviews(HashSet<Guid> memberGuids, HashSet<Guid> keyFramesGuids)
    {
        foreach (var keyFrame in doc.AnimationHandler.KeyFrames)
        {
            if (keyFrame is ICelGroupHandler groupHandler)
            {
                foreach (var childFrame in groupHandler.Children)
                {
                    if (!keyFramesGuids.Contains(childFrame.Id))
                    {
                        if (!memberGuids.Contains(childFrame.LayerGuid) || !IsInFrame(childFrame))
                            continue;
                    }

                    RenderFramePreview(childFrame);
                }

                if (!memberGuids.Contains(groupHandler.LayerGuid))
                    continue;

                RenderGroupPreview(groupHandler);
            }
        }
    }

    private bool IsInFrame(ICelHandler cel)
    {
        return cel.StartFrameBindable <= doc.AnimationHandler.ActiveFrameBindable &&
               cel.StartFrameBindable + cel.DurationBindable >= doc.AnimationHandler.ActiveFrameBindable;
    }

    private void RenderFramePreview(ICelHandler cel)
    {
        if (internals.Tracker.Document.AnimationData.TryFindKeyFrame(cel.Id, out KeyFrame _))
        {
            KeyFrameTime frameTime = doc.AnimationHandler.ActiveFrameTime;
            if (cel.PreviewPainter == null)
            {
                cel.PreviewPainter = new PreviewPainter(doc.Renderer, AnimationKeyFramePreviewRenderer, frameTime,
                    doc.SizeBindable,
                    internals.Tracker.Document.ProcessingColorSpace, cel.Id.ToString());
            }
            else
            {
                cel.PreviewPainter.FrameTime = frameTime;
                cel.PreviewPainter.DocumentSize = doc.SizeBindable;
                cel.PreviewPainter.ProcessingColorSpace = internals.Tracker.Document.ProcessingColorSpace;
            }

            cel.PreviewPainter.Repaint();
        }
    }

    private void RenderGroupPreview(ICelGroupHandler groupHandler)
    {
        var group = internals.Tracker.Document.AnimationData.KeyFrames.FirstOrDefault(x => x.Id == groupHandler.Id);
        if (group != null)
        {
            KeyFrameTime frameTime = doc.AnimationHandler.ActiveFrameTime;
            ColorSpace processingColorSpace = internals.Tracker.Document.ProcessingColorSpace;
            VecI documentSize = doc.SizeBindable;

            if (groupHandler.PreviewPainter == null)
            {
                groupHandler.PreviewPainter =
                    new PreviewPainter(doc.Renderer, AnimationKeyFramePreviewRenderer, frameTime, documentSize,
                        processingColorSpace,
                        groupHandler.Id.ToString());
            }
            else
            {
                groupHandler.PreviewPainter.FrameTime = frameTime;
                groupHandler.PreviewPainter.DocumentSize = documentSize;
                groupHandler.PreviewPainter.ProcessingColorSpace = processingColorSpace;
            }

            groupHandler.PreviewPainter.Repaint();
        }
    }

    private void RenderMaskPreviews(HashSet<Guid> members)
    {
        foreach (var node in doc.NodeGraphHandler.AllNodes)
        {
            if (node is IStructureMemberHandler structureMemberHandler)
            {
                if (!members.Contains(node.Id))
                    continue;

                var member = internals.Tracker.Document.FindMember(node.Id);
                if (member is not IPreviewRenderable previewRenderable)
                    continue;

                if (structureMemberHandler.MaskPreviewPainter == null)
                {
                    structureMemberHandler.MaskPreviewPainter = new PreviewPainter(
                        doc.Renderer,
                        previewRenderable,
                        doc.AnimationHandler.ActiveFrameTime,
                        doc.SizeBindable,
                        internals.Tracker.Document.ProcessingColorSpace,
                        nameof(StructureNode.EmbeddedMask));
                }
                else
                {
                    structureMemberHandler.MaskPreviewPainter.FrameTime = doc.AnimationHandler.ActiveFrameTime;
                    structureMemberHandler.MaskPreviewPainter.DocumentSize = doc.SizeBindable;
                    structureMemberHandler.MaskPreviewPainter.ProcessingColorSpace =
                        internals.Tracker.Document.ProcessingColorSpace;
                }

                structureMemberHandler.MaskPreviewPainter.Repaint();
            }
        }
    }

    private void RenderNodePreviews(HashSet<Guid> nodesGuids)
    {
        var outputNode = internals.Tracker.Document.NodeGraph.OutputNode;

        if (outputNode is null)
            return;

        var allNodes =
            internals.Tracker.Document.NodeGraph
                .AllNodes; //internals.Tracker.Document.NodeGraph.CalculateExecutionQueue(outputNode);

        if (nodesGuids.Count == 0)
            return;

        List<Guid> actualRepaintedNodes = new();
        foreach (var guid in nodesGuids)
        {
            QueueRepaintNode(actualRepaintedNodes, guid, allNodes);
        }
    }

    private void QueueRepaintNode(List<Guid> actualRepaintedNodes, Guid guid,
        IReadOnlyCollection<IReadOnlyNode> allNodes)
    {
        if (actualRepaintedNodes.Contains(guid))
            return;

        var nodeVm = doc.StructureHelper.FindNode<INodeHandler>(guid);
        if (nodeVm == null)
        {
            return;
        }

        actualRepaintedNodes.Add(guid);
        IReadOnlyNode node = allNodes.FirstOrDefault(x => x.Id == guid);
        if (node is null)
            return;

        RequestRepaintNode(node, nodeVm);

        nodeVm.TraverseForwards(next =>
        {
            if (next is not INodeHandler nextVm)
                return true;

            var nextNode = allNodes.FirstOrDefault(x => x.Id == next.Id);

            if (nextNode is null || actualRepaintedNodes.Contains(next.Id))
                return true;

            RequestRepaintNode(nextNode, nextVm);
            actualRepaintedNodes.Add(next.Id);
            return true;
        });
    }

    private void RequestRepaintNode(IReadOnlyNode node, INodeHandler nodeVm)
    {
        if (node is IPreviewRenderable renderable)
        {
            if (nodeVm.ResultPainter == null)
            {
                nodeVm.ResultPainter = new PreviewPainter(doc.Renderer, renderable,
                    doc.AnimationHandler.ActiveFrameTime,
                    doc.SizeBindable, internals.Tracker.Document.ProcessingColorSpace);
                nodeVm.ResultPainter.Repaint();
            }
            else
            {
                nodeVm.ResultPainter.FrameTime = doc.AnimationHandler.ActiveFrameTime;
                nodeVm.ResultPainter.DocumentSize = doc.SizeBindable;
                nodeVm.ResultPainter.ProcessingColorSpace = internals.Tracker.Document.ProcessingColorSpace;

                nodeVm.ResultPainter?.Repaint();
            }
        }
    }
}
