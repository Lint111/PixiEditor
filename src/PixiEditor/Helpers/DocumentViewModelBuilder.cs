﻿using System.Collections;
using System.Reflection;
using ChunkyImageLib;
using ChunkyImageLib.DataHolders;
using PixiEditor.Helpers.Extensions;
using PixiEditor.ViewModels.Document;
using PixiEditor.ChangeableDocument.Changeables.Graph;
using PixiEditor.ChangeableDocument.Changeables.Graph.Nodes;
using Drawie.Backend.Core;
using Drawie.Backend.Core.Surfaces.ImageData;
using PixiEditor.Extensions.CommonApi.Palettes;
using Drawie.Numerics;
using PixiEditor.ChangeableDocument.Changeables.Graph.Interfaces;
using PixiEditor.Parser;
using PixiEditor.Parser.Graph;
using PixiEditor.Parser.Skia;
using NodeGraph = PixiEditor.Parser.Graph.NodeGraph;

namespace PixiEditor.Helpers;

internal class DocumentViewModelBuilder
{
    public string SerializerName { get; set; }
    public string SerializerVersion { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public List<PaletteColor> Swatches { get; set; } = new List<PaletteColor>();
    public List<PaletteColor> Palette { get; set; } = new List<PaletteColor>();

    public ReferenceLayerBuilder ReferenceLayer { get; set; }
    public AnimationDataBuilder AnimationData { get; set; }

    public NodeGraphBuilder Graph { get; set; }
    public string ImageEncoderUsed { get; set; } = "QOI";
    public bool UsesLegacyColorBlending { get; set; } = false;
    public Version? PixiParserVersionUsed { get; set; }

    public DocumentViewModelBuilder WithSize(int width, int height)
    {
        Width = width;
        Height = height;

        return this;
    }

    public DocumentViewModelBuilder WithSize(VecI size) => WithSize(size.X, size.Y);

    public DocumentViewModelBuilder WithSwatches(IEnumerable<PaletteColor> swatches)
    {
        Swatches = new(swatches);
        return this;
    }

    public DocumentViewModelBuilder WithSwatches<T>(IEnumerable<T> swatches, Func<T, PaletteColor> toColor) =>
        WithSwatches(swatches.Select(toColor));

    public DocumentViewModelBuilder WithPalette(IEnumerable<PaletteColor> palette)
    {
        Palette = new(palette);
        return this;
    }

    public DocumentViewModelBuilder WithPalette<T>(IEnumerable<T> pallet, Func<T, PaletteColor> toColor) =>
        WithPalette(pallet.Select(toColor));

    public DocumentViewModelBuilder WithReferenceLayer<T>(T reference,
        Action<T, ReferenceLayerBuilder, ImageEncoder?> builder,
        ImageEncoder? encoder)
    {
        if (reference != null)
        {
            WithReferenceLayer(x => builder(reference, x, encoder));
        }

        return this;
    }

    public DocumentViewModelBuilder WithReferenceLayer(Action<ReferenceLayerBuilder> builder)
    {
        var reference = new ReferenceLayerBuilder();

        builder(reference);

        ReferenceLayer = reference;

        return this;
    }

    public DocumentViewModelBuilder WithAnimationData(AnimationData? animationData, NodeGraph documentGraph)
    {
        AnimationData = new AnimationDataBuilder();

        if (animationData != null && animationData.KeyFrameGroups.Count > 0)
        {
            AnimationData.WithFrameRate(animationData.FrameRate);
            AnimationData.WithOnionFrames(animationData.OnionFrames);
            AnimationData.WithOnionOpacity(animationData.OnionOpacity);
            BuildKeyFrames(animationData.KeyFrameGroups.ToList(), AnimationData.KeyFrameGroups, documentGraph);
        }

        return this;
    }

    public DocumentViewModelBuilder WithGraph(NodeGraph graph, Action<NodeGraph, NodeGraphBuilder> builder)
    {
        if (graph != null)
        {
            WithGraph(x => builder(graph, x));
        }

        return this;
    }

    public DocumentViewModelBuilder WithGraph(Action<NodeGraphBuilder> builder)
    {
        var graph = new NodeGraphBuilder();
        builder(graph);
        Graph = graph;
        return this;
    }

    public DocumentViewModelBuilder WithImageEncoder(string encoder)
    {
        ImageEncoderUsed = encoder;
        return this;
    }

    public DocumentViewModelBuilder WithLegacyColorBlending(bool usesLegacyColorBlending)
    {
        UsesLegacyColorBlending = usesLegacyColorBlending;
        return this;
    }

    private static void BuildKeyFrames(List<KeyFrameGroup> root, List<KeyFrameBuilder> data, NodeGraph documentGraph)
    {
        foreach (KeyFrameGroup group in root)
        {
            GroupKeyFrameBuilder builder = new GroupKeyFrameBuilder()
                .WithNodeId(group.NodeId);

            foreach (var child in group.Children)
            {
                builder.WithChild<KeyFrameBuilder>(x => x
                    .WithKeyFrameId(child.KeyFrameId)
                    .WithNodeId(child.NodeId));
            }

            data?.Add(builder);
        }
        
        TryAddMissingKeyFrames(root, data, documentGraph);
    }

    private static void TryAddMissingKeyFrames(List<KeyFrameGroup> groups, List<KeyFrameBuilder>? data, NodeGraph documentGraph)
    {
        if (data == null)
        {
            return;
        }

        foreach (var node in documentGraph.AllNodes)
        {
            if (node.KeyFrames.Length > 1 && data.All(x => x.NodeId != node.Id))
            {
                GroupKeyFrameBuilder builder = new GroupKeyFrameBuilder()
                .WithNodeId(node.Id);
                
                foreach (var keyFrame in node.KeyFrames)
                {
                    builder.WithChild<KeyFrameBuilder>(x => x
                        .WithKeyFrameId(keyFrame.Id)
                        .WithNodeId(node.Id));
                }   
                
                data.Add(builder);
            }
        }
    }

    public class ReferenceLayerBuilder
    {
        public bool IsVisible { get; set; }

        public bool IsTopmost { get; set; }

        public VecI ImageSize { get; set; }

        public ShapeCorners Shape { get; set; }

        public byte[] ImageBgra8888Bytes { get; set; }

        public ReferenceLayerBuilder WithIsVisible(bool isVisible)
        {
            IsVisible = isVisible;
            return this;
        }

        public ReferenceLayerBuilder WithIsTopmost(bool isTopmost)
        {
            IsTopmost = isTopmost;
            return this;
        }

        public ReferenceLayerBuilder WithSurface(Surface surface)
        {
            byte[] bytes = surface.ToByteArray();
            WithImage(surface.Size, bytes);

            return this;
        }

        public ReferenceLayerBuilder WithImage(VecI size, byte[] pbgraData)
        {
            ImageSize = size;
            ImageBgra8888Bytes = pbgraData;
            return this;
        }

        public ReferenceLayerBuilder WithShape(Corners rect)
        {
            Shape = new ShapeCorners
            {
                TopLeft = rect.TopLeft.ToVecD(),
                TopRight = rect.TopRight.ToVecD(),
                BottomLeft = rect.BottomLeft.ToVecD(),
                BottomRight = rect.BottomRight.ToVecD()
            };

            return this;
        }
    }

    public DocumentViewModelBuilder WithSerializerData(string documentSerializerName, string documentSerializerVersion)
    {
        SerializerName = documentSerializerName;
        SerializerVersion = documentSerializerVersion;
        return this;
    }

    public DocumentViewModelBuilder WithPixiParserVersion(Version version)
    {
        PixiParserVersionUsed = version;
        return this;
    }
}

internal class AnimationDataBuilder
{
    public int FrameRate { get; set; } = 24;
    public List<KeyFrameBuilder> KeyFrameGroups { get; set; } = new List<KeyFrameBuilder>();
    public int OnionFrames { get; set; }
    public double OnionOpacity { get; set; } = 50;

    public AnimationDataBuilder WithFrameRate(int frameRate)
    {
        FrameRate = frameRate;
        return this;
    }

    public AnimationDataBuilder WithOnionFrames(int onionFrames)
    {
        OnionFrames = onionFrames;
        return this;
    }

    public AnimationDataBuilder WithOnionOpacity(double onionOpacity)
    {
        OnionOpacity = onionOpacity;
        return this;
    }

    public AnimationDataBuilder WithKeyFrameGroups(Action<List<KeyFrameBuilder>> builder)
    {
        builder(KeyFrameGroups);
        return this;
    }
}

internal class KeyFrameBuilder()
{
    public int NodeId { get; set; }
    public int KeyFrameId { get; set; }

    public KeyFrameBuilder WithKeyFrameId(int layerId)
    {
        KeyFrameId = layerId;
        return this;
    }

    public KeyFrameBuilder WithNodeId(int nodeId)
    {
        NodeId = nodeId;
        return this;
    }
}

internal class GroupKeyFrameBuilder : KeyFrameBuilder
{
    public List<KeyFrameBuilder> Children { get; set; } = new List<KeyFrameBuilder>();

    public GroupKeyFrameBuilder WithChild<T>(Action<T> child) where T : KeyFrameBuilder, new()
    {
        var childBuilder = new T();
        child(childBuilder);
        Children.Add(childBuilder);
        return this;
    }

    public new GroupKeyFrameBuilder WithNodeId(int layerGuid) =>
        base.WithKeyFrameId(layerGuid) as GroupKeyFrameBuilder;
}

internal class NodeGraphBuilder
{
    public List<NodeBuilder> AllNodes { get; set; } = new List<NodeBuilder>();


    public NodeGraphBuilder WithNode(Action<NodeBuilder> nodeBuilder)
    {
        var node = new NodeBuilder();
        nodeBuilder(node);

        AllNodes.Add(node);

        return this;
    }

    public NodeGraphBuilder WithOutputNode(int? toConnectNodeId, string? toConnectPropName)
    {
        var node = this.WithNodeOfType(typeof(OutputNode))
            .WithId(AllNodes.Count);

        if (toConnectNodeId != null && toConnectPropName != null)
        {
            node.WithConnections(new[]
            {
                new PropertyConnection
                {
                    OutputNodeId = toConnectNodeId.Value,
                    OutputPropertyName = toConnectPropName,
                    InputPropertyName = OutputNode.InputPropertyName
                }
            });
        }

        return this;
    }

    public NodeGraphBuilder WithImageLayerNode(string name, Surface image, ColorSpace colorSpace, out int id)
    {
        this.WithNodeOfType(typeof(ImageLayerNode))
            .WithName(name)
            .WithId(AllNodes.Count)
            .WithKeyFrames(
            [
                new KeyFrameData
                {
                    AffectedElement = ImageLayerNode.ImageLayerKey,
                    Data = new ChunkyImage(image, colorSpace),
                    Duration = 0,
                    StartFrame = 0,
                    IsVisible = true
                }
            ]);

        id = AllNodes.Count;
        return this;
    }

    public NodeGraphBuilder WithImageLayerNode(string name, VecI size, ColorSpace colorSpace, out int id)
    {
        this.WithNodeOfType(typeof(ImageLayerNode))
            .WithName(name)
            .WithId(AllNodes.Count)
            .WithKeyFrames(
            [
                new KeyFrameData
                {
                    AffectedElement = ImageLayerNode.ImageLayerKey,
                    Data = new ChunkyImage(size, colorSpace),
                    Duration = 0,
                    StartFrame = 0,
                    IsVisible = true
                }
            ]);

        id = AllNodes.Count;
        return this;
    }

    public NodeBuilder WithNodeOfType(Type nodeType)
    {
        var node = new NodeBuilder();
        node.WithUniqueNodeName(nodeType.GetCustomAttribute<NodeInfoAttribute>().UniqueName);

        AllNodes.Add(node);

        return node;
    }

    public NodeBuilder WithNodeOfType<T>(out int id) where T : IReadOnlyNode
    {
        NodeBuilder builder = this.WithNodeOfType(typeof(T))
            .WithId(AllNodes.Count);

        id = AllNodes.Count;
        return builder;
    }

    internal class NodeBuilder
    {
        public int Id { get; set; }
        public Vector2 Position { get; set; }
        public string Name { get; set; }
        public string UniqueNodeName { get; set; }
        public Dictionary<string, object> InputValues { get; set; }
        public KeyFrameData[] KeyFrames { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; }
        public Dictionary<int, List<(string inputPropName, string outputPropName)>> InputConnections { get; set; }

        public NodeBuilder WithId(int id)
        {
            Id = id;
            return this;
        }

        public NodeBuilder WithPosition(Vector2 position)
        {
            Position = position;
            return this;
        }

        public NodeBuilder WithName(string name)
        {
            Name = name;
            return this;
        }

        public NodeBuilder WithUniqueNodeName(string uniqueNodeName)
        {
            UniqueNodeName = uniqueNodeName;
            return this;
        }

        public NodeBuilder WithInputValues(Dictionary<string, object> values)
        {
            InputValues = values;
            return this;
        }

        public NodeBuilder WithAdditionalData(Dictionary<string, object> data)
        {
            AdditionalData = data;
            return this;
        }

        public NodeBuilder WithConnections(PropertyConnection[] nodeInputConnections)
        {
            InputConnections = new Dictionary<int, List<(string, string)>>();

            foreach (var connection in nodeInputConnections)
            {
                if (!InputConnections.ContainsKey(connection.OutputNodeId))
                {
                    InputConnections.Add(connection.OutputNodeId, new List<(string, string)>());
                }

                InputConnections[connection.OutputNodeId]
                    .Add((connection.InputPropertyName, connection.OutputPropertyName));
            }

            return this;
        }

        public NodeBuilder WithKeyFrames(KeyFrameData[] keyFrames)
        {
            KeyFrames = keyFrames;
            return this;
        }
    }
}
