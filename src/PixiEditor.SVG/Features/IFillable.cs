﻿using PixiEditor.SVG.Enums;
using PixiEditor.SVG.Units;

namespace PixiEditor.SVG.Features;

public interface IFillable
{
    public SvgProperty<SvgColorUnit> Fill { get; }
    public SvgProperty<SvgNumericUnit> FillOpacity { get; }
}
