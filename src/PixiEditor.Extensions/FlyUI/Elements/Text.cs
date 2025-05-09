﻿using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using PixiEditor.Extensions.CommonApi.FlyUI.Properties;
using PixiEditor.Extensions.Extensions;
using PixiEditor.Extensions.FlyUI.Converters;
using FontStyle = PixiEditor.Extensions.CommonApi.FlyUI.Properties.FontStyle;
using FontWeight = PixiEditor.Extensions.CommonApi.FlyUI.Properties.FontWeight;

namespace PixiEditor.Extensions.FlyUI.Elements;

public class Text : LayoutElement
{
    private TextWrap _textWrap = TextWrap.None;
    private string _value = null!;
    private TextStyle textStyle = TextStyle.Default;

    public string Value { get => _value; set => SetField(ref _value, value); }
    public TextWrap TextWrap { get => _textWrap; set => SetField(ref _textWrap, value); }

    public TextStyle TextStyle { get => textStyle; set => SetField(ref textStyle, value); }
    public Text()
    {
    }

    public Text(string value = "", TextWrap textWrap = TextWrap.None, TextStyle? textStyle = null)
    {
        Value = value;
        TextWrap = textWrap;
        TextStyle = textStyle ?? TextStyle.Default;
    }

    protected override Control CreateNativeControl()
    {
        TextBlock textBlock = new();
        Binding valueBinding = new()
        {
            Source = this,
            Path = nameof(Value),
        };
        
        Binding textWrapBinding = new()
        {
            Source = this,
            Path = nameof(TextWrap),
            Converter = new EnumToEnumConverter<TextWrap, TextWrapping>(),
        };
        
        Binding fontStyleBinding = new()
        {
            Source = this,
            Path = "TextStyle.FontStyle",
            Converter = new EnumToEnumConverter<FontStyle, Avalonia.Media.FontStyle>(),
        };
        
        Binding fontSizeBinding = new()
        {
            Source = this,
            Path = "TextStyle.FontSize",
        };

        Binding fontWeightBinding = new()
        {
            Source = this,
            Path = "TextStyle.FontWeight",
            Converter = new EnumToEnumConverter<FontWeight, Avalonia.Media.FontWeight>(),
        };

        Binding fontFamilyBinding = new()
        {
            Source = this,
            Path = "TextStyle.FontFamily",
        };

        Binding colorBinding = new()
        {
            Source = this,
            Path = "TextStyle.Color",
            Converter = new ColorToAvaloniaBrushConverter(),
        };
        
        textBlock.Bind(TextBlock.TextProperty, valueBinding);
        textBlock.Bind(TextBlock.TextWrappingProperty, textWrapBinding);
        if (TextStyle.FontStyle != null)
        {
            textBlock.Bind(TextBlock.FontStyleProperty, fontStyleBinding);
        }

        if (TextStyle.FontSize != null)
        {
            textBlock.Bind(TextBlock.FontSizeProperty, fontSizeBinding);
        }

        if (TextStyle.FontWeight != null)
        {
            textBlock.Bind(TextBlock.FontWeightProperty, fontWeightBinding);
        }

        if (TextStyle.FontFamily != null)
        {
            textBlock.Bind(TextBlock.FontFamilyProperty, fontFamilyBinding);
        }

        if (TextStyle.Color != null)
        {
            textBlock.Bind(TextBlock.ForegroundProperty, colorBinding);
        }

        return textBlock;
    }

    protected override IEnumerable<object> GetControlProperties()
    {
        yield return Value;
        yield return TextWrap;
        yield return TextStyle;
    }

    protected override void DeserializeControlProperties(List<object> values)
    {
        Value = (string)values.ElementAtOrDefault(0);
        TextWrap = (TextWrap)values.ElementAtOrDefault(1);
        TextStyle = (TextStyle)values.ElementAtOrDefault(2, TextStyle.Default);
    }
}
