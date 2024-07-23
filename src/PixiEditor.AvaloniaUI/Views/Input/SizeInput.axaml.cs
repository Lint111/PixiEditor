﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using PixiEditor.AvaloniaUI.Models.Dialogs;

namespace PixiEditor.AvaloniaUI.Views.Input;

/// <summary>
///     Interaction logic for SizeInput.xaml.
/// </summary>
internal partial class SizeInput : UserControl
{
    public static readonly StyledProperty<int> SizeProperty =
        AvaloniaProperty.Register<SizeInput, int>(nameof(Size), defaultValue: 1, coerce: Coerce);

    public static readonly StyledProperty<int> MaxSizeProperty =
        AvaloniaProperty.Register<SizeInput, int>(nameof(MaxSize), defaultValue: int.MaxValue);

    public static readonly StyledProperty<bool> BehaveLikeSmallEmbeddedFieldProperty =
        AvaloniaProperty.Register<SizeInput, bool>(nameof(BehaveLikeSmallEmbeddedField), defaultValue: true);

    public static readonly StyledProperty<SizeUnit> UnitProperty =
        AvaloniaProperty.Register<SizeInput, SizeUnit>(nameof(Unit), defaultValue: SizeUnit.Pixel);

    public static readonly StyledProperty<bool> FocusNextProperty = AvaloniaProperty.Register<SizeInput, bool>(
        nameof(FocusNext), defaultValue: true);

    public bool FocusNext
    {
        get => GetValue(FocusNextProperty);
        set => SetValue(FocusNextProperty, value);
    }

    public Action OnScrollAction
    {
        get { return GetValue(OnScrollActionProperty); }
        set { SetValue(OnScrollActionProperty, value); }
    }

    public static readonly StyledProperty<Action> OnScrollActionProperty =
        AvaloniaProperty.Register<SizeInput, Action>(nameof(OnScrollAction));

    public int Size
    {
        get => (int)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public int MaxSize
    {
        get => (int)GetValue(MaxSizeProperty);
        set => SetValue(MaxSizeProperty, value);
    }

    public bool BehaveLikeSmallEmbeddedField
    {
        get => (bool)GetValue(BehaveLikeSmallEmbeddedFieldProperty);
        set => SetValue(BehaveLikeSmallEmbeddedFieldProperty, value);
    }

    static SizeInput()
    {
        SizeProperty.Changed.Subscribe(InputSizeChanged);
    }

    public SizeInput()
    {
        InitializeComponent();
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        if (input.IsFocused) return;
        FocusAndSelect();
    }

    public void FocusAndSelect()
    {
        input.Focus();
        input.SelectAll();
    }

    private void Border_MouseLeftButtonDown(object? sender, PointerPressedEventArgs e)
    {
        /*Point pos = Mouse.GetPosition(textBox);
        int charIndex = textBox.GetCharacterIndexFromPoint(pos, true);
        var charRect = textBox.GetRectFromCharacterIndex(charIndex);
        double middleX = (charRect.Left + charRect.Right) / 2;
        if (pos.X > middleX)
            textBox.CaretIndex = charIndex + 1;
        else
            textBox.CaretIndex = charIndex;*/
        //TODO: Above functions not found in Avalonia
        input.SelectAll();
        e.Handled = true;
        if (!input.IsFocused)
            input.Focus();
    }

    public SizeUnit Unit
    {
        get => (SizeUnit)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }


    private static int Coerce(AvaloniaObject sender, int value)
    {
        if (value <= 0)
        {
            return 1;
        }

        int maxSize = sender.GetValue(MaxSizeProperty);
        
        if (value > maxSize)
        {
            return maxSize;
        }
        
        return value;
    }

    private static void InputSizeChanged(AvaloniaPropertyChangedEventArgs<int> e)
    {
        int newValue = e.NewValue.Value;
        int maxSize = (int)e.Sender.GetValue(MaxSizeProperty);

        if (newValue > maxSize)
        {
            e.Sender.SetValue(SizeProperty, maxSize);

            return;
        }
        else if (newValue <= 0)
        {
            e.Sender.SetValue(SizeProperty, 1);
        }
    }

    private void Border_MouseWheel(object? sender, PointerWheelEventArgs e)
    {
        int step = (int)e.Delta.Y / 100;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            Size += step * 2;
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (step < 0)
            {
                Size /= 2;
            }
            else
            {
                Size *= 2;
            }
        }
        else
        {
            Size += step;
        }

        OnScrollAction?.Invoke();
    }
}
