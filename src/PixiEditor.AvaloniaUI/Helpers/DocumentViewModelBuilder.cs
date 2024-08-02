﻿using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using ChunkyImageLib;
using ChunkyImageLib.DataHolders;
using PixiEditor.AvaloniaUI.Helpers.Extensions;
using PixiEditor.AvaloniaUI.Views.Animations;
using PixiEditor.DrawingApi.Core;
using PixiEditor.DrawingApi.Core.Numerics;
using PixiEditor.DrawingApi.Core.Surfaces;
using PixiEditor.DrawingApi.Core.Surfaces.Surface;
using PixiEditor.Extensions.CommonApi.Palettes;
using PixiEditor.Numerics;
using PixiEditor.Parser;
using PixiEditor.Parser.Helpers;
using BlendMode = PixiEditor.ChangeableDocument.Enums.BlendMode;

namespace PixiEditor.AvaloniaUI.Helpers;

internal class DocumentViewModelBuilder : ChildrenBuilder
{
    public int Width { get; set; }
    public int Height { get; set; }

    public List<PaletteColor> Swatches { get; set; } = new List<PaletteColor>();
    public List<PaletteColor> Palette { get; set; } = new List<PaletteColor>();

    public ReferenceLayerBuilder ReferenceLayer { get; set; }
    public List<KeyFrameBuilder> AnimationData { get; set; } = new List<KeyFrameBuilder>();

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

    public DocumentViewModelBuilder WithReferenceLayer<T>(T reference, Action<T, ReferenceLayerBuilder> builder)
    {
        if (reference != null)
        {
            WithReferenceLayer(x => builder(reference, x));
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
    
    public DocumentViewModelBuilder WithAnimationData(AnimationData? animationData, Folder documentRootFolder)
    {
        AnimationData = new List<KeyFrameBuilder>();

        if (animationData != null && animationData.KeyFrameGroups.Count > 0)
        {
            BuildKeyFrames(animationData.KeyFrameGroups.Cast<IKeyFrame>().ToList(), AnimationData, documentRootFolder);
        }

        return this;
    }

    private static void BuildKeyFrames(List<IKeyFrame> root, List<KeyFrameBuilder> data, Folder documentRootFolder)
    {
        foreach (var keyFrame in root)
        {
            if (keyFrame is KeyFrameGroup group)
            {
                GroupKeyFrameBuilder builder = new GroupKeyFrameBuilder()
                    .WithVisibility(group.Enabled)
                    .WithId(group.LayerGuid)
                    .WithLayerGuid(group.LayerGuid);

                foreach (var child in group.Children)
                {
                    if(child is KeyFrameGroup childGroup)
                    {
                        builder.WithChild<GroupKeyFrameBuilder>(x => BuildKeyFrames(childGroup.Children, null, documentRootFolder));
                    }
                    else if (child is RasterKeyFrame rasterKeyFrame)
                    {
                        builder.WithChild<RasterKeyFrameBuilder>(x => x
                            .WithVisibility(builder.IsVisible)
                            .WithId(rasterKeyFrame.Guid)
                            .WithLayerGuid(rasterKeyFrame.LayerGuid)
                            .WithStartFrame(rasterKeyFrame.StartFrame)
                            .WithDuration(rasterKeyFrame.Duration)
                            .WithSurface(Surface.Load(rasterKeyFrame.ImageBytes)));
                    }
                }

                data?.Add(builder);
            }

        }
    }

    public abstract class StructureMemberBuilder
    {
        private MaskBuilder maskBuilder;

        public int OrderInStructure { get; set; }

        public string Name { get; set; }

        public bool IsVisible { get; set; }

        public float Opacity { get; set; }

        public BlendMode BlendMode { get; set; }

        public bool ClipToMemberBelow { get; set; }

        public bool HasMask => maskBuilder is not null;

        [NotNull] public MaskBuilder Mask => maskBuilder ??= new MaskBuilder();

        public Guid Id { get; set; }

        public StructureMemberBuilder()
        {
            IsVisible = true;
            Opacity = 1;
        }

        public StructureMemberBuilder WithOrderInStructure(int order)
        {
            OrderInStructure = order;
            return this;
        }

        public StructureMemberBuilder WithName(string name)
        {
            Name = name;
            return this;
        }

        public StructureMemberBuilder WithVisibility(bool visibility)
        {
            IsVisible = visibility;
            return this;
        }

        public StructureMemberBuilder WithOpacity(float opacity)
        {
            Opacity = opacity;
            return this;
        }

        public StructureMemberBuilder WithBlendMode(BlendMode blendMode)
        {
            BlendMode = blendMode;
            return this;
        }

        public StructureMemberBuilder WithMask(Action<MaskBuilder> mask)
        {
            mask(Mask);
            return this;
        }

        public StructureMemberBuilder WithMask<T>(T reference, Action<MaskBuilder, T> mask)
        {
            return reference != null ? WithMask(x => mask(x, reference)) : this;
        }

        public StructureMemberBuilder WithGuid(Guid guid)
        {
            Id = guid;
            return this;
        }

        public StructureMemberBuilder WithClipToBelow(bool value)
        {
            ClipToMemberBelow = value;
            return this;
        }
    }

    public class LayerBuilder : StructureMemberBuilder
    {
        private int? width;
        private int? height;

        public SurfaceBuilder? Surface { get; set; }

        public int Width
        {
            get => width ?? default;
            set => width = value;
        }

        public int Height
        {
            get => height ?? default;
            set => height = value;
        }

        public int OffsetX { get; set; }

        public int OffsetY { get; set; }

        public bool LockAlpha { get; set; }

        public new LayerBuilder WithName(string name) => base.WithName(name) as LayerBuilder;

        public new LayerBuilder WithVisibility(bool visibility) => base.WithVisibility(visibility) as LayerBuilder;

        public new LayerBuilder WithOpacity(float opacity) => base.WithOpacity(opacity) as LayerBuilder;

        public new LayerBuilder WithBlendMode(BlendMode blendMode) => base.WithBlendMode(blendMode) as LayerBuilder;

        public new LayerBuilder WithClipToBelow(bool value) => base.WithClipToBelow(value) as LayerBuilder;

        public LayerBuilder WithLockAlpha(bool layerLockAlpha)
        {
            LockAlpha = layerLockAlpha;
            return this;
        }

        public new LayerBuilder WithMask(Action<MaskBuilder> mask) => base.WithMask(mask) as LayerBuilder;

        public new LayerBuilder WithGuid(Guid guid) => base.WithGuid(guid) as LayerBuilder;

        public LayerBuilder WithSurface(Surface surface)
        {
            Surface = new(surface);
            return this;
        }

        public LayerBuilder WithSize(int width, int height)
        {
            Width = width;
            Height = height;
            return this;
        }

        public LayerBuilder WithSize(VecI size) => WithSize(size.X, size.Y);

        public LayerBuilder WithRect(int width, int height, int offsetX, int offsetY)
        {
            Width = width;
            Height = height;
            OffsetX = offsetX;
            OffsetY = offsetY;
            return this;
        }

        public LayerBuilder WithSurface(Action<SurfaceBuilder> surface)
        {
            if (width is null || height is null)
            {
                throw new InvalidOperationException(
                    "You must first set the width and height of the layer. You can do this by calling WithRect() or setting the Width and Height properties.");
            }

            var surfaceBuilder = new SurfaceBuilder(new Surface(new VecI(Width, Height)));
            surface(surfaceBuilder);
            Surface = surfaceBuilder;
            return this;
        }
    }

    public class FolderBuilder : StructureMemberBuilder
    {
        public List<StructureMemberBuilder> Children { get; set; } = new List<StructureMemberBuilder>();

        public new FolderBuilder WithName(string name) => base.WithName(name) as FolderBuilder;

        public new FolderBuilder WithVisibility(bool visibility) => base.WithVisibility(visibility) as FolderBuilder;

        public new FolderBuilder WithOpacity(float opacity) => base.WithOpacity(opacity) as FolderBuilder;

        public new FolderBuilder WithBlendMode(BlendMode blendMode) => base.WithBlendMode(blendMode) as FolderBuilder;

        public new FolderBuilder WithMask(Action<MaskBuilder> mask) => base.WithMask(mask) as FolderBuilder;

        public new FolderBuilder WithGuid(Guid guid) => base.WithGuid(guid) as FolderBuilder;

        public FolderBuilder WithClipToBelow(bool value) => base.WithClipToBelow(value) as FolderBuilder;

        public FolderBuilder WithChildren(Action<ChildrenBuilder> children)
        {
            ChildrenBuilder childrenBuilder = new();
            children(childrenBuilder);
            Children = childrenBuilder.Children;
            return this;
        }
    }

    public class SurfaceBuilder
    {
        public Surface Surface { get; set; }

        public SurfaceBuilder(Surface surface)
        {
            Surface = surface;
        }

        public SurfaceBuilder WithImage(ReadOnlySpan<byte> buffer) => WithImage(buffer, 0, 0);

        public SurfaceBuilder WithImage(ReadOnlySpan<byte> buffer, int x, int y)
        {
            if (buffer.IsEmpty) return this;

            Surface.DrawingSurface.Canvas.DrawBitmap(Bitmap.Decode(buffer), x, y);
            return this;
        }
    }

    public class MaskBuilder
    {
        public bool IsVisible { get; set; }

        public SurfaceBuilder Surface { get; set; }

        public MaskBuilder()
        {
            IsVisible = true;
        }

        public MaskBuilder WithVisibility(bool isVisible)
        {
            IsVisible = isVisible;
            return this;
        }

        public MaskBuilder WithSurface(Surface surface)
        {
            Surface = new SurfaceBuilder(surface);
            return this;
        }

        public MaskBuilder WithSurface(int width, int height, Action<SurfaceBuilder> surface)
        {
            var surfaceBuilder = new SurfaceBuilder(new Surface(new VecI(Math.Max(width, 1), Math.Max(height, 1))));
            surface(surfaceBuilder);
            Surface = surfaceBuilder;
            return this;
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
}

internal class ChildrenBuilder
{
    public List<DocumentViewModelBuilder.StructureMemberBuilder> Children { get; set; } =
        new List<DocumentViewModelBuilder.StructureMemberBuilder>();

    public ChildrenBuilder WithLayer(Action<DocumentViewModelBuilder.LayerBuilder> layer)
    {
        var layerBuilder = new DocumentViewModelBuilder.LayerBuilder();
        layer(layerBuilder);
        Children.Add(layerBuilder);
        return this;
    }

    public ChildrenBuilder WithFolder(Action<DocumentViewModelBuilder.FolderBuilder> folder)
    {
        var folderBuilder = new DocumentViewModelBuilder.FolderBuilder();
        folder(folderBuilder);
        Children.Add(folderBuilder);
        return this;
    }
}

internal class KeyFrameBuilder()
{
    public int StartFrame { get; set; }
    public int Duration { get; set; }
    public bool IsVisible { get; set; }
    public Guid LayerGuid { get; set; }
    public Guid Id { get; set; }

    public KeyFrameBuilder WithStartFrame(int startFrame)
    {
        StartFrame = startFrame;
        return this;
    }

    public KeyFrameBuilder WithDuration(int duration)
    {
        Duration = duration;
        return this;
    }

    public KeyFrameBuilder WithVisibility(bool isVisible)
    {
        IsVisible = isVisible;
        return this;
    }

    public KeyFrameBuilder WithLayerGuid(Guid layerGuid)
    {
        LayerGuid = layerGuid;
        return this;
    }

    public KeyFrameBuilder WithId(Guid id)
    {
        Id = id;
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
    
    public new GroupKeyFrameBuilder WithVisibility(bool isVisible) => base.WithVisibility(isVisible) as GroupKeyFrameBuilder;
    public new GroupKeyFrameBuilder WithLayerGuid(Guid layerGuid) => base.WithLayerGuid(layerGuid) as GroupKeyFrameBuilder;
    public new GroupKeyFrameBuilder WithId(Guid id) => base.WithId(id) as GroupKeyFrameBuilder;
    public new GroupKeyFrameBuilder WithStartFrame(int startFrame) => base.WithStartFrame(startFrame) as GroupKeyFrameBuilder;
    public new GroupKeyFrameBuilder WithDuration(int duration) => base.WithDuration(duration) as GroupKeyFrameBuilder;
}

internal class RasterKeyFrameBuilder : KeyFrameBuilder
{
    public DocumentViewModelBuilder.SurfaceBuilder Surface { get; set; }

    public RasterKeyFrameBuilder WithSurface(Surface surface)
    {
        Surface = new DocumentViewModelBuilder.SurfaceBuilder(new Surface(surface));
        return this;
    }

    public new RasterKeyFrameBuilder WithVisibility(bool isVisible) => base.WithVisibility(isVisible) as RasterKeyFrameBuilder;
    public new RasterKeyFrameBuilder WithLayerGuid(Guid layerGuid) => base.WithLayerGuid(layerGuid) as RasterKeyFrameBuilder;
    public new RasterKeyFrameBuilder WithId(Guid id) => base.WithId(id) as RasterKeyFrameBuilder;
    public new RasterKeyFrameBuilder WithStartFrame(int startFrame) => base.WithStartFrame(startFrame) as RasterKeyFrameBuilder;
    public new RasterKeyFrameBuilder WithDuration(int duration) => base.WithDuration(duration) as RasterKeyFrameBuilder;
}
