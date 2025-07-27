﻿using PixiEditor.ChangeableDocument.Changeables.Graph.Nodes;
using PixiEditor.ChangeableDocument.Changeables.Interfaces;

namespace PixiEditor.ChangeableDocument.Changeables.Animations;

internal class AnimationData : IReadOnlyAnimationData
{
    public int FrameRate { get; set; } = 24;
    public int OnionFrames { get; set; } = 1;
    public int DefaultEndFrame { get; set; } = 24;
    public IReadOnlyList<IReadOnlyKeyFrame> KeyFrames => keyFrames;
    public double OnionOpacity { get; set; } = 50;

    private List<KeyFrame> keyFrames = new List<KeyFrame>();
    private readonly Document document;

    public AnimationData(Document document)
    {
        this.document = document;
    }

    public void AddKeyFrame(KeyFrame keyFrame)
    {
        Guid id = keyFrame.NodeId;
        if (TryFindKeyFrameCallback(id, out GroupKeyFrame group))
        {
            group.Children.Add(keyFrame);
        }
        else if (keyFrame is GroupKeyFrame groupKeyFrame)
        {
            keyFrames.Add(groupKeyFrame);
            foreach (var child in groupKeyFrame.Children)
            {
                SubscribeToKeyFrameEvents(child);
            }
        }
        else
        {
            var node = document.FindNodeOrThrow<Node>(id);
            GroupKeyFrame createdGroup = new GroupKeyFrame(node.Id, keyFrame.StartFrame, document);
            createdGroup.Children.Add(keyFrame);
            keyFrames.Add(createdGroup);
        }

        SubscribeToKeyFrameEvents(keyFrame);
    }

    private void SubscribeToKeyFrameEvents(KeyFrame keyFrame)
    {
        Node node = document.FindNodeOrThrow<Node>(keyFrame.NodeId);

        keyFrame.KeyFrameVisibilityChanged += node.SetKeyFrameVisibility;
        keyFrame.KeyFrameLengthChanged += node.SetKeyFrameLength;
    }
    
    private void UnsubscribeFromKeyFrameEvents(KeyFrame keyFrame)
    {
        keyFrame.ClearEvents();
    }

    public void RemoveKeyFrame(Guid createdKeyFrameId)
    {
        TryFindKeyFrameCallback<KeyFrame>(createdKeyFrameId, out _, (frame, parent) =>
        {
            if (frame is GroupKeyFrame group)
            {
                keyFrames.Remove(group);
                foreach (var child in group.Children)
                {
                    RemoveKeyFrame(child.Id);
                }
            }

            if (document.TryFindNode<Node>(frame.NodeId, out Node? node))
            {
                node.RemoveKeyFrame(frame.Id);
            }

            parent?.Children.Remove(frame);
            
            if (parent?.Children.Count == 0)
            {
                keyFrames.Remove(parent);
            }
            
            UnsubscribeFromKeyFrameEvents(frame);
        });
    }

    public bool TryFindKeyFrame<T>(Guid id, out T keyFrame) where T : IReadOnlyKeyFrame
    {
        return TryFindKeyFrameCallback(id, out keyFrame, null);
    }

    private bool TryFindKeyFrameCallback<T>(Guid id, out T? foundKeyFrame,
        Action<KeyFrame, GroupKeyFrame?> onFound = null) where T : IReadOnlyKeyFrame
    {
        return TryFindKeyFrame(keyFrames, null, id, out foundKeyFrame, onFound);
    }

    private bool TryFindKeyFrame<T>(List<KeyFrame> root, GroupKeyFrame parent, Guid id, out T? result,
        Action<KeyFrame, GroupKeyFrame?> onFound) where T : IReadOnlyKeyFrame
    {
        for (var i = 0; i < root.Count; i++)
        {
            var frame = root[i];
            if (frame is T targetFrame && targetFrame.Id.Equals(id))
            {
                result = targetFrame;
                onFound?.Invoke(frame, parent);
                return true;
            }

            if (frame is GroupKeyFrame { Children.Count: > 0 } group)
            {
                bool found = TryFindKeyFrame(group.Children, group, id, out result, onFound);
                if (found)
                {
                    return true;
                }
            }
        }

        result = default;
        return false;
    }
}
