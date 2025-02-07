﻿using Drawie.Backend.Core.ColorsImpl;
using Drawie.Backend.Core.Surfaces;
using Drawie.Backend.Core.Surfaces.PaintImpl;
using Drawie.Backend.Core.Text;
using Drawie.Backend.Core.Vector;
using Drawie.Numerics;
using PixiEditor.ChangeableDocument.Changeables.Graph.Interfaces.Shapes;

namespace PixiEditor.ChangeableDocument.Changeables.Graph.Nodes.Shapes.Data;

public class TextVectorData : ShapeVectorData, IReadOnlyTextData
{
    private string text;
    private Font font = Font.CreateDefault();
    private double? spacing = null;
    private double strokeWidth = 1;
    private VectorPath? path;

    public string Text
    {
        get => text;
        set
        {
            text = value;
            richText = new RichText(value) { Spacing = Spacing, MaxWidth = MaxWidth, StrokeWidth = StrokeWidth };

            lastBounds = richText.MeasureBounds(Font);
        }
    }

    public VecD Position { get; set; }


    public double MaxWidth { get; set; } = double.MaxValue;

    public Font Font
    {
        get => font;
        set
        {
            if (value != null)
            {
                value.Changed -= FontChanged;
            }

            font = value;
            if (value != null)
            {
                value.Changed += FontChanged;
            }

            lastBounds = richText.MeasureBounds(value);
        }
    }

    private void FontChanged()
    {
        if (richText == null)
        {
            return;
        }

        lastBounds = richText.MeasureBounds(Font);
    }

    public Font ConstructFont()
    {
        Font newFont = Font.FromFontFamily(Font.Family);
        newFont.Size = Font.Size;
        newFont.Edging = Font.Edging;
        newFont.SubPixel = Font.SubPixel;
        newFont.Bold = Font.Bold;
        newFont.Italic = Font.Italic;

        return newFont;
    }

    double IReadOnlyTextData.Spacing => Spacing ?? Font.Size;

    public double? Spacing
    {
        get => spacing;
        set
        {
            spacing = value;
            richText.Spacing = value;
            lastBounds = richText.MeasureBounds(Font);
        }
    }
    
    public bool AntiAlias { get; set; } = true;

    protected override void OnStrokeWidthChanged()
    {
        if(richText == null)
        {
            return;
        }

        richText.StrokeWidth = StrokeWidth;
        lastBounds = richText.MeasureBounds(Font);
    }

    public override RectD GeometryAABB
    {
        get
        {
            return lastBounds.Offset(Position);
        }
    }

    public override ShapeCorners TransformationCorners =>
        new ShapeCorners(GeometryAABB).WithMatrix(TransformationMatrix);

    public override RectD VisualAABB => GeometryAABB;

    public VectorPath? Path
    {
        get => path;
        set
        {
            path = value;
            // TODO: properly calculate bounds
            //lastBounds = richText.MeasureBounds(Font);
        }
    }

    public FontFamilyName? MissingFontFamily { get; set; }
    public string MissingFontText { get; set; }

    private RichText richText;
    private RectD lastBounds;
    private double _spacing;

    public TextVectorData()
    {

    }

    public TextVectorData(string text)
    {
        Text = text;
    }


    public override VectorPath ToPath()
    {
        var path = richText.ToPath(Font);
        path.Offset(Position);

        return path;
    }

    public override void RasterizeGeometry(Canvas canvas)
    {
        Rasterize(canvas, false);
    }

    public override void RasterizeTransformed(Canvas canvas)
    {
        Rasterize(canvas, true);
    }

    private void Rasterize(Canvas canvas, bool applyTransform)
    {
        int num = 0;
        if (applyTransform)
        {
            num = canvas.Save();
            ApplyTransformTo(canvas);
        }

        using Paint paint = new Paint() { IsAntiAliased = AntiAlias };

        richText.Fill = Fill;
        richText.FillColor = FillColor;
        richText.StrokeColor = StrokeColor;
        richText.StrokeWidth = StrokeWidth;
        richText.Spacing = Spacing;

        if (MissingFontFamily != null)
        {
            paint.Color = Fill ? FillColor : StrokeColor;
            canvas.DrawText($"{MissingFontText}: " + MissingFontFamily.Value.Name, Position, Font, paint);
        }
        else
        {
            PaintText(canvas, paint);
        }

        if (applyTransform)
        {
            canvas.RestoreToCount(num);
        }
    }

    private void PaintText(Canvas canvas, Paint paint)
    {
        richText.Paint(canvas, Position, Font, paint, Path);
    }

    public override bool IsValid()
    {
        return !string.IsNullOrEmpty(Text);
    }

    public override int GetCacheHash()
    {
        return HashCode.Combine(Text, Position, Font, StrokeColor, FillColor, StrokeWidth, TransformationMatrix);
    }

    protected override void AdjustCopy(ShapeVectorData copy)
    {
        if (copy is TextVectorData textData)
        {
            textData.Font = Font.FromFontFamily(Font.Family);
            textData.Font.Size = Font.Size;
            textData.Font.Edging = Font.Edging;
            textData.Font.SubPixel = Font.SubPixel;
            textData.Font.Bold = Font.Bold;
            textData.Font.Italic = Font.Italic;

            textData.lastBounds = lastBounds;
        }
    }

    public override int CalculateHash()
    {
        return GetCacheHash();
    }
}
