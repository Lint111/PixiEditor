﻿using System.Diagnostics.CodeAnalysis;
using Drawie.Backend.Core;
using Drawie.Backend.Core.ColorsImpl;
using Drawie.Backend.Core.Numerics;
using Drawie.Backend.Core.Surfaces.ImageData;
using Drawie.Backend.Core.Surfaces.PaintImpl;
using Drawie.Backend.Core.Text;
using Drawie.Backend.Core.Vector;
using Drawie.Numerics;
using PixiEditor.ChangeableDocument.Changeables.Graph.Interfaces;
using PixiEditor.ChangeableDocument.Changeables.Graph.Nodes;
using PixiEditor.ChangeableDocument.Changeables.Graph.Nodes.Shapes.Data;
using PixiEditor.Extensions.Common.Localization;
using PixiEditor.Helpers;
using PixiEditor.Models.Dialogs;
using PixiEditor.Parser.Graph;
using PixiEditor.SVG;
using PixiEditor.SVG.Elements;
using PixiEditor.SVG.Enums;
using PixiEditor.SVG.Exceptions;
using PixiEditor.ViewModels.Tools.Tools;

namespace PixiEditor.Models.IO.CustomDocumentFormats;

internal class SvgDocumentBuilder : IDocumentBuilder
{
    public IReadOnlyCollection<string> Extensions { get; } = [".svg"];

    public void Build(DocumentViewModelBuilder builder, string path)
    {
        string xml = File.ReadAllText(path);
        SvgDocument document = SvgDocument.Parse(xml);

        if (document == null)
        {
            throw new SvgParsingException("Failed to parse SVG document");
        }

        StyleContext styleContext = new(document);

        VecI size = new((int)document.ViewBox.Unit.Value.Value.Width, (int)document.ViewBox.Unit.Value.Value.Height);
        if (size.ShortestAxis < 1)
        {
            size = new VecI(1024, 1024);
        }

        builder.WithSize(size)
            .WithSrgbColorBlending(true) // apparently svgs blend colors in SRGB space
            .WithGraph(graph =>
            {
                int? lastId = null;
                foreach (SvgElement element in document.Children)
                {
                    StyleContext style = styleContext.WithElement(element);
                    if (element is SvgPrimitive primitive)
                    {
                        lastId = AddPrimitive(element, style, graph, lastId);
                    }
                    else if (element is SvgGroup group)
                    {
                        lastId = AddGroup(group, graph, style, lastId);
                    }
                    else if (element is SvgImage svgImage)
                    {
                        lastId = AddImage(svgImage, style, graph, lastId);
                    }
                }

                graph.WithOutputNode(lastId, "Output");
            });
    }

    [return: NotNull]
    private int? AddPrimitive(SvgElement element, StyleContext styleContext,
        NodeGraphBuilder graph,
        int? lastId, string connectionName = "Background")
    {
        LocalizedString name = "";
        ShapeVectorData shapeData = null;
        if (element is SvgEllipse or SvgCircle)
        {
            shapeData = AddEllipse(element);
            name = VectorEllipseToolViewModel.NewLayerKey;
        }
        else if (element is SvgLine line)
        {
            shapeData = AddLine(line);
            name = VectorLineToolViewModel.NewLayerKey;
        }
        else if (element is SvgPath pathElement)
        {
            shapeData = AddPath(pathElement, styleContext);
            name = VectorPathToolViewModel.NewLayerKey;
        }
        else if (element is SvgRectangle rect)
        {
            shapeData = AddRect(rect);
            name = VectorRectangleToolViewModel.NewLayerKey;
        }
        else if (element is SvgText text)
        {
            shapeData = AddText(text);
            name = TextToolViewModel.NewLayerKey;
        }

        name = element.Id.Unit?.Value ?? name;

        AddCommonShapeData(shapeData, styleContext);

        NodeGraphBuilder.NodeBuilder nBuilder = graph.WithNodeOfType<VectorLayerNode>(out int id)
            .WithName(name)
            .WithInputValues(new Dictionary<string, object>()
            {
                { StructureNode.OpacityPropertyName, (float)(styleContext.Opacity.Unit?.Value ?? 1f) }
            })
            .WithAdditionalData(new Dictionary<string, object>() { { "ShapeData", shapeData } });

        if (lastId != null)
        {
            nBuilder.WithConnections([
                new PropertyConnection()
                {
                    InputPropertyName = connectionName, OutputPropertyName = "Output", OutputNodeId = lastId.Value
                }
            ]);
        }

        lastId = id;
        return lastId;
    }

    private int? AddGroup(SvgGroup group, NodeGraphBuilder graph, StyleContext style, int? lastId,
        string connectionName = "Background")
    {
        int? childId = null;
        var connectTo = "Background";
        foreach (var child in group.Children)
        {
            StyleContext childStyle = style.WithElement(child);

            if (child is SvgPrimitive primitive)
            {
                childId = AddPrimitive(child, childStyle, graph, childId, connectTo);
            }
            else if (child is SvgGroup childGroup)
            {
                childId = AddGroup(childGroup, graph, childStyle, childId, connectTo);
            }
            else if (child is SvgImage image)
            {
                childId = AddImage(image, childStyle, graph, childId);
            }
        }

        NodeGraphBuilder.NodeBuilder nBuilder = graph.WithNodeOfType<FolderNode>(out int id)
            .WithName(group.Id.Unit != null ? group.Id.Unit.Value.Value : new LocalizedString("NEW_FOLDER"));

        int connectionsCount = 0;
        if (lastId != null) connectionsCount++;
        if (childId != null) connectionsCount++;

        PropertyConnection[] connections = new PropertyConnection[connectionsCount];
        if (lastId != null)
        {
            connections[0] = new PropertyConnection()
            {
                InputPropertyName = connectionName, OutputPropertyName = "Output", OutputNodeId = lastId.Value
            };
        }

        if (childId != null)
        {
            connections[^1] = new PropertyConnection()
            {
                InputPropertyName = "Content", OutputPropertyName = "Output", OutputNodeId = childId.Value
            };
        }

        if (connections.Length > 0)
        {
            nBuilder.WithConnections(connections);
        }

        lastId = id;

        return lastId;
    }

    private int? AddImage(SvgImage image, StyleContext style, NodeGraphBuilder graph, int? lastId)
    {
        byte[] bytes = TryReadImage(image.Href.Unit?.Value ?? "");

        Surface? imgSurface = bytes is { Length: > 0 } ? Surface.Load(bytes) : null;
        Surface? finalSurface = null;

        if (imgSurface != null)
        {
            if (imgSurface.Size.X != (int)image.Width.Unit?.PixelsValue ||
                imgSurface.Size.Y != (int)image.Height.Unit?.PixelsValue)
            {
                var resized = imgSurface.ResizeNearestNeighbor(
                    new VecI((int)image.Width.Unit?.PixelsValue, (int)image.Height.Unit?.PixelsValue));
                imgSurface.Dispose();
                imgSurface = resized;
            }
        }

        if (style.ViewboxSize.ShortestAxis > 0 && imgSurface != null)
        {
            finalSurface = new Surface((VecI)style.ViewboxSize);
            double x = image.X.Unit?.PixelsValue ?? 0;
            double y = image.Y.Unit?.PixelsValue ?? 0;
            finalSurface.DrawingSurface.Canvas.DrawSurface(imgSurface.DrawingSurface, (int)x, (int)y);
            imgSurface.Dispose();
        }

        var graphBuilder = graph.WithImageLayerNode(
            image.Id.Unit?.Value ?? new LocalizedString("NEW_LAYER").Value,
            finalSurface, ColorSpace.CreateSrgb(), out int id);

        if (lastId != null)
        {
            var nodeBuilder = graphBuilder.AllNodes[^1];

            Dictionary<string, object> inputValues = new()
            {
                { StructureNode.OpacityPropertyName, (float)(style.Opacity.Unit?.Value ?? 1f) }
            };

            nodeBuilder.WithInputValues(inputValues);
            nodeBuilder.WithConnections([
                new PropertyConnection()
                {
                    InputPropertyName = "Background", OutputPropertyName = "Output", OutputNodeId = lastId.Value
                }
            ]);
        }

        lastId = id;

        return lastId;
    }

    private byte[] TryReadImage(string svgHref)
    {
        if (string.IsNullOrEmpty(svgHref))
        {
            return [];
        }

        if (svgHref.StartsWith("data:image/png;base64,"))
        {
            return Convert.FromBase64String(svgHref.Replace("data:image/png;base64,", ""));
        }

        // TODO: Implement downloading images from the internet
        /*if (Uri.TryCreate(svgHref, UriKind.Absolute, out Uri? uri))
        {
            try
            {
                using WebClient client = new();
                return client.DownloadData(uri);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return [];
            }
        }*/

        return [];
    }

    private EllipseVectorData AddEllipse(SvgElement element)
    {
        if (element is SvgCircle circle)
        {
            return new EllipseVectorData(
                new VecD(circle.Cx.Unit?.PixelsValue ?? 0, circle.Cy.Unit?.PixelsValue ?? 0),
                new VecD(circle.R.Unit?.PixelsValue ?? 0, circle.R.Unit?.PixelsValue ?? 0));
        }

        if (element is SvgEllipse ellipse)
        {
            return new EllipseVectorData(
                new VecD(ellipse.Cx.Unit?.PixelsValue ?? 0, ellipse.Cy.Unit?.PixelsValue ?? 0),
                new VecD(ellipse.Rx.Unit?.PixelsValue ?? 0, ellipse.Ry.Unit?.PixelsValue ?? 0));
        }

        return null;
    }

    private LineVectorData AddLine(SvgLine element)
    {
        return new LineVectorData(
            new VecD(element.X1.Unit?.PixelsValue ?? 0, element.Y1.Unit?.PixelsValue ?? 0),
            new VecD(element.X2.Unit?.PixelsValue ?? 0, element.Y2.Unit?.PixelsValue ?? 0));
    }

    private PathVectorData AddPath(SvgPath element, StyleContext styleContext)
    {
        VectorPath? path = null;
        if (element.PathData.Unit != null)
        {
            path = VectorPath.FromSvgPath(element.PathData.Unit.Value.Value);
        }

        if (element.FillRule.Unit != null)
        {
            path.FillType = element.FillRule.Unit.Value.Value switch
            {
                SvgFillRule.EvenOdd => PathFillType.EvenOdd,
                SvgFillRule.NonZero => PathFillType.Winding,
                _ => PathFillType.Winding
            };
        }

        StrokeCap strokeLineCap = StrokeCap.Butt;
        StrokeJoin strokeLineJoin = StrokeJoin.Miter;

        if (styleContext.StrokeLineCap.Unit != null)
        {
            strokeLineCap = (StrokeCap)(styleContext.StrokeLineCap.Unit?.Value ?? SvgStrokeLineCap.Butt);
        }

        if (styleContext.StrokeLineJoin.Unit != null)
        {
            strokeLineJoin = (StrokeJoin)(styleContext.StrokeLineJoin.Unit?.Value ?? SvgStrokeLineJoin.Miter);
        }

        return new PathVectorData(path) { StrokeLineCap = strokeLineCap, StrokeLineJoin = strokeLineJoin, };
    }

    private RectangleVectorData AddRect(SvgRectangle element)
    {
        return new RectangleVectorData(
            element.X.Unit?.PixelsValue ?? 0, element.Y.Unit?.PixelsValue ?? 0,
            element.Width.Unit?.PixelsValue ?? 0, element.Height.Unit?.PixelsValue ?? 0);
    }

    private TextVectorData AddText(SvgText element)
    {
        Font font = element.FontFamily.Unit.HasValue
            ? Font.FromFamilyName(element.FontFamily.Unit.Value.Value)
            : Font.CreateDefault();
        FontFamilyName? missingFont = null;
        if (font == null)
        {
            font = Font.CreateDefault();
            missingFont = new FontFamilyName(element.FontFamily.Unit.Value.Value);
        }

        font.Size = element.FontSize.Unit?.PixelsValue ?? 12;
        font.Bold = element.FontWeight.Unit?.Value == SvgFontWeight.Bold;
        font.Italic = element.FontStyle.Unit?.Value == SvgFontStyle.Italic;

        return new TextVectorData(element.Text.Unit.Value.Value)
        {
            Position = new VecD(
                element.X.Unit?.PixelsValue ?? 0,
                element.Y.Unit?.PixelsValue ?? 0),
            Font = font,
            MissingFontFamily = missingFont,
            MissingFontText = "MISSING_FONT"
        };
    }

    private void AddCommonShapeData(ShapeVectorData? shapeData, StyleContext styleContext)
    {
        if (shapeData == null)
        {
            return;
        }

        bool hasFill = styleContext.Fill.Unit?.Paintable is { AnythingVisible: true };
        bool hasStroke = styleContext.Stroke.Unit?.Paintable is { AnythingVisible: true } ||
                         styleContext.StrokeWidth.Unit is { PixelsValue: > 0 };
        bool hasTransform = styleContext.Transform.Unit is { MatrixValue.IsIdentity: false };

        shapeData.Fill = hasFill;
        if (hasFill)
        {
            var target = styleContext.Fill.Unit;
            float opacity = (float)(styleContext.FillOpacity.Unit?.Value ?? 1);
            opacity = Math.Clamp(opacity, 0, 1);
            shapeData.FillPaintable = target.Value.Paintable;
            shapeData.FillPaintable?.ApplyOpacity(opacity);
        }

        if (hasStroke)
        {
            var targetColor = styleContext.Stroke.Unit;
            var targetWidth = styleContext.StrokeWidth.Unit;

            shapeData.Stroke = targetColor?.Paintable ?? Colors.Black;
            shapeData.StrokeWidth = (float)(targetWidth?.PixelsValue ?? 1);
        }

        if (hasTransform)
        {
            var target = styleContext.Transform.Unit;
            shapeData.TransformationMatrix = target.Value.MatrixValue;
        }

        if (styleContext.ViewboxOrigin != VecD.Zero)
        {
            shapeData.TransformationMatrix = shapeData.TransformationMatrix.PostConcat(
                Matrix3X3.CreateTranslation((float)styleContext.ViewboxOrigin.X, (float)styleContext.ViewboxOrigin.Y));
        }
    }
}
