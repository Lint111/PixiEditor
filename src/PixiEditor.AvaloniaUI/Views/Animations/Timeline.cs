﻿using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using PixiEditor.AvaloniaUI.Helpers;
using PixiEditor.AvaloniaUI.ViewModels.Document;
using PixiEditor.ChangeableDocument.Actions.Generated;

namespace PixiEditor.AvaloniaUI.Views.Animations;

[TemplatePart("PART_PlayToggle", typeof(ToggleButton))]
[TemplatePart("PART_TimelineSlider", typeof(TimelineSlider))]
[TemplatePart("PART_ContentGrid", typeof(Grid))]
[TemplatePart("PART_TimelineKeyFramesScroll", typeof(ScrollViewer))]
[TemplatePart("PART_TimelineHeaderScroll", typeof(ScrollViewer))]
[TemplatePart("PART_SelectionRectangle", typeof(Rectangle))]
[TemplatePart("PART_KeyFramesHost", typeof(ItemsControl))]
internal class Timeline : TemplatedControl, INotifyPropertyChanged
{
    private const float MarginMultiplier = 1.5f;
    
    public static readonly StyledProperty<KeyFrameCollection> KeyFramesProperty =
        AvaloniaProperty.Register<Timeline, KeyFrameCollection>(
            nameof(KeyFrames));

    public static readonly StyledProperty<int> ActiveFrameProperty =
        AvaloniaProperty.Register<Timeline, int>(nameof(ActiveFrame), 1);

    public static readonly StyledProperty<bool> IsPlayingProperty = AvaloniaProperty.Register<Timeline, bool>(
        nameof(IsPlaying));

    public static readonly StyledProperty<ICommand> NewKeyFrameCommandProperty =
        AvaloniaProperty.Register<Timeline, ICommand>(
            nameof(NewKeyFrameCommand));

    public static readonly StyledProperty<double> ScaleProperty = AvaloniaProperty.Register<Timeline, double>(
        nameof(Scale), 100);

    public static readonly StyledProperty<int> FpsProperty = AvaloniaProperty.Register<Timeline, int>(nameof(Fps), 60);

    public static readonly StyledProperty<Vector> ScrollOffsetProperty = AvaloniaProperty.Register<Timeline, Vector>(
        nameof(ScrollOffset));

    public Vector ScrollOffset
    {
        get => GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    public double Scale
    {
        get => GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    public ICommand NewKeyFrameCommand
    {
        get => GetValue(NewKeyFrameCommandProperty);
        set => SetValue(NewKeyFrameCommandProperty, value);
    }

    public static readonly StyledProperty<ICommand> DuplicateKeyFrameCommandProperty =
        AvaloniaProperty.Register<Timeline, ICommand>(
            nameof(DuplicateKeyFrameCommand));

    public static readonly StyledProperty<ICommand> DeleteKeyFrameCommandProperty =
        AvaloniaProperty.Register<Timeline, ICommand>(
            nameof(DeleteKeyFrameCommand));

    public static readonly StyledProperty<double> MinLeftOffsetProperty = AvaloniaProperty.Register<Timeline, double>(
        nameof(MinLeftOffset), 30);

    public static readonly StyledProperty<ICommand> ChangeKeyFramesLengthCommandProperty = AvaloniaProperty.Register<Timeline, ICommand>(
        nameof(ChangeKeyFramesLengthCommand));

    public static readonly StyledProperty<int> DefaultEndFrameProperty = AvaloniaProperty.Register<Timeline, int>(
        nameof(DefaultEndFrame));

    public int DefaultEndFrame
    {
        get => GetValue(DefaultEndFrameProperty);
        set => SetValue(DefaultEndFrameProperty, value);
    }

    public ICommand ChangeKeyFramesLengthCommand
    {
        get => GetValue(ChangeKeyFramesLengthCommandProperty);
        set => SetValue(ChangeKeyFramesLengthCommandProperty, value);
    }

    public double MinLeftOffset
    {
        get => GetValue(MinLeftOffsetProperty);
        set => SetValue(MinLeftOffsetProperty, value);
    }

    public ICommand DeleteKeyFrameCommand
    {
        get => GetValue(DeleteKeyFrameCommandProperty);
        set => SetValue(DeleteKeyFrameCommandProperty, value);
    }

    public ICommand DuplicateKeyFrameCommand
    {
        get => GetValue(DuplicateKeyFrameCommandProperty);
        set => SetValue(DuplicateKeyFrameCommandProperty, value);
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public KeyFrameCollection KeyFrames
    {
        get => GetValue(KeyFramesProperty);
        set => SetValue(KeyFramesProperty, value);
    }

    public int ActiveFrame
    {
        get { return (int)GetValue(ActiveFrameProperty); }
        set { SetValue(ActiveFrameProperty, value); }
    }

    public int Fps
    {
        get { return (int)GetValue(FpsProperty); }
        set { SetValue(FpsProperty, value); }
    }

    public ICommand DraggedKeyFrameCommand { get; }
    public ICommand ReleasedKeyFrameCommand { get; }
    public ICommand ClearSelectedKeyFramesCommand { get; }
    public ICommand PressedKeyFrameCommand { get; }

    public IReadOnlyCollection<KeyFrameViewModel> SelectedKeyFrames => KeyFrames != null
        ? KeyFrames.SelectChildrenBy<KeyFrameViewModel>(x => x.IsSelected).ToList()
        : [];

    private ToggleButton? _playToggle;
    private DispatcherTimer _playTimer;
    private Grid? _contentGrid;
    private TimelineSlider? _timelineSlider;
    private ScrollViewer? _timelineKeyFramesScroll;
    private ScrollViewer? _timelineHeaderScroll;
    private Control? extendingElement;
    private Rectangle _selectionRectangle;
    private ItemsControl? _keyFramesHost;
    
    private Vector clickPos;
    
    private bool shouldClearNextSelection = true;
    private KeyFrameViewModel clickedKeyFrame;
    private bool dragged;
    private int dragStartFrame;
    
    public event PropertyChangedEventHandler? PropertyChanged;

    static Timeline()
    {
        IsPlayingProperty.Changed.Subscribe(IsPlayingChanged);
        FpsProperty.Changed.Subscribe(FpsChanged);
        KeyFramesProperty.Changed.Subscribe(OnKeyFramesChanged);
    }

    public Timeline()
    {
        _playTimer =
            new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(1000f / Fps) };
        _playTimer.Tick += PlayTimerOnTick;
        PressedKeyFrameCommand = new RelayCommand<PointerPressedEventArgs>(KeyFramePressed);
        ClearSelectedKeyFramesCommand = new RelayCommand<KeyFrameViewModel>(ClearSelectedKeyFrames);
        DraggedKeyFrameCommand = new RelayCommand<PointerEventArgs>(KeyFramesDragged);
        ReleasedKeyFrameCommand = new RelayCommand<KeyFrameViewModel>(KeyFramesReleased);
    }
    
    public void SelectKeyFrame(KeyFrameViewModel? keyFrame, bool clearSelection = true)
    {
        if (clearSelection)
        {
            ClearSelectedKeyFrames();
        }

        keyFrame?.Document.AnimationDataViewModel.AddSelectedKeyFrame(keyFrame.Id);
    }

    public bool DragAllSelectedKeyFrames(int delta)
    {
        bool canDrag = SelectedKeyFrames.All(x => x.StartFrameBindable + delta > 0);
        if (!canDrag)
        {
            return false;
        }
        
        Guid[] ids = SelectedKeyFrames.Select(x => x.Id).ToArray();
        
        ChangeKeyFramesLengthCommand.Execute((ids, delta, false));
        return true;
    }
    
    public void EndDragging()
    {
        if (dragged)
        {
            ChangeKeyFramesLengthCommand.Execute((SelectedKeyFrames.Select(x => x.Id).ToArray(), 0, true));
        }
        clickedKeyFrame = null;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _playToggle = e.NameScope.Find<ToggleButton>("PART_PlayToggle");

        if (_playToggle != null)
        {
            _playToggle.Click += PlayToggleOnClick;
        }

        _contentGrid = e.NameScope.Find<Grid>("PART_ContentGrid");

        _timelineSlider = e.NameScope.Find<TimelineSlider>("PART_TimelineSlider");
        _timelineSlider.PointerWheelChanged += TimelineSliderOnPointerWheelChanged;

        _timelineKeyFramesScroll = e.NameScope.Find<ScrollViewer>("PART_TimelineKeyFramesScroll");
        _timelineHeaderScroll = e.NameScope.Find<ScrollViewer>("PART_TimelineHeaderScroll");
        
        _selectionRectangle = e.NameScope.Find<Rectangle>("PART_SelectionRectangle");

        _timelineKeyFramesScroll.ScrollChanged += TimelineKeyFramesScrollOnScrollChanged;
        _contentGrid.PointerPressed += ContentOnPointerPressed;
        _contentGrid.PointerMoved += ContentOnPointerMoved;
        _contentGrid.PointerCaptureLost += ContentOnPointerLost;
        
        extendingElement = new Control();
        extendingElement.SetValue(MarginProperty, new Thickness(0, 0, 0, 0));
        _contentGrid.Children.Add(extendingElement);
        
        _keyFramesHost = e.NameScope.Find<ItemsControl>("PART_KeyFramesHost");
    }
    
    private void KeyFramesReleased(KeyFrameViewModel? e)
    {
        if (!dragged)
        {
            SelectKeyFrame(e, shouldClearNextSelection);
            shouldClearNextSelection = true;
        }
        else
        {
            EndDragging();
        }

        dragged = false;
        clickedKeyFrame = null;
    }

    private void KeyFramesDragged(PointerEventArgs? e)
    {
        if (clickedKeyFrame == null) return;

        int frameUnderMouse = MousePosToFrame(e);
        int delta = frameUnderMouse - dragStartFrame;

        if (delta != 0)
        {
            if (!clickedKeyFrame.IsSelected)
            {
                SelectKeyFrame(clickedKeyFrame);
            }

            dragged = true;
            if (DragAllSelectedKeyFrames(delta))
            {
                dragStartFrame += delta;
            }
        }
    }

    private void ClearSelectedKeyFrames(KeyFrameViewModel? keyFrame)
    {
        ClearSelectedKeyFrames();
    }

    private void KeyFramePressed(PointerPressedEventArgs? e)
    {
        shouldClearNextSelection = !e.KeyModifiers.HasFlag(KeyModifiers.Control);
        KeyFrame target = null;
        if (e.Source is Control obj)
        {
            if (obj is KeyFrame frame)
                target = frame;
            else if (obj.TemplatedParent is KeyFrame keyFrame) target = keyFrame;
        }

        e.Pointer.Capture(target);
        clickedKeyFrame = target.Item;
        dragStartFrame = MousePosToFrame(e);
        e.Handled = true;
    }

    private void ClearSelectedKeyFrames()
    {
        foreach (var keyFrame in SelectedKeyFrames)
        {
            keyFrame.Document.AnimationDataViewModel.RemoveSelectedKeyFrame(keyFrame.Id);
        }
    }

    private void TimelineKeyFramesScrollOnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        ScrollOffset = new Vector(scrollViewer.Offset.X, scrollViewer.Offset.Y);
        _timelineSlider.Offset = new Vector(scrollViewer.Offset.X, 0);
        _timelineHeaderScroll!.Offset = new Vector(0, scrollViewer.Offset.Y);
    }

    private void PlayTimerOnTick(object? sender, EventArgs e)
    {
        if (ActiveFrame >= (KeyFrames.Count > 0 ? KeyFrames.FrameCount : DefaultEndFrame))
        {
            ActiveFrame = 1;
        }
        else
        {
            ActiveFrame++;
        }
    }

    private void PlayToggleOnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton toggleButton)
        {
            return;
        }

        if (toggleButton.IsChecked == true)
        {
            IsPlaying = true;
        }
        else
        {
            IsPlaying = false;
        }
    }

    private void TimelineSliderOnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        double newScale = Scale;

        int ticks = e.KeyModifiers.HasFlag(KeyModifiers.Control) ? 1 : 10;

        int towardsFrame = MousePosToFrame(e);

        if (e.Delta.Y > 0)
        {
            newScale += ticks;
        }
        else if (e.Delta.Y < 0)
        {
            newScale -= ticks;
        }
        
        newScale = Math.Clamp(newScale, 1, 900);
        Scale = newScale;
        
        double mouseXInViewport = e.GetPosition(_timelineKeyFramesScroll).X;
            
        double currentFrameUnderMouse = towardsFrame;
        double newOffsetX = currentFrameUnderMouse * newScale - mouseXInViewport + MinLeftOffset;

        if (towardsFrame * MarginMultiplier > KeyFrames.FrameCount)
        {
            // 50 is a magic number I found working ok, for bigger frames it is a bit too big, maybe find a better way to calculate this?
            extendingElement.Margin = new Thickness(newOffsetX * 50, 0, 0, 0);
        }
        else
        {
            extendingElement.Margin = new Thickness(_timelineKeyFramesScroll.Viewport.Width, 0, 0, 0);
        }

        Dispatcher.UIThread.Post(
            () =>
        {
            newOffsetX = Math.Clamp(newOffsetX, 0, _timelineKeyFramesScroll.ScrollBarMaximum.X);
            
            ScrollOffset = new Vector(newOffsetX, 0);
        }, DispatcherPriority.Render);

        e.Handled = true;
    }
    
    private void ContentOnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Grid content)
        {
            return;
        }
        
        var mouseButton = e.GetMouseButton(content);

        if (mouseButton == MouseButton.Left)
        {
            _selectionRectangle.IsVisible = true;
            _selectionRectangle.Width = 0;
            _selectionRectangle.Height = 0;
            
        }
        else if (mouseButton == MouseButton.Middle)
        {
            Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Pointer.Capture(content);

            if (_timelineKeyFramesScroll.ScrollBarMaximum.X == ScrollOffset.X)
            {
                extendingElement.Margin = new Thickness(_timelineKeyFramesScroll.Viewport.Width, 0, 0, 0);
            }
            
        }
        
        clickPos = e.GetPosition(content);
        e.Handled = true;
    }
    
    private void ContentOnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.Source is not Grid content)
        {
            return;
        }

        if (e.GetCurrentPoint(content).Properties.IsLeftButtonPressed)
        {
            HandleMoveSelection(e, content);
        }
        else if (e.GetCurrentPoint(content).Properties.IsMiddleButtonPressed)
        {
            HandleTimelinePan(e, content);
        }
    }

    private void HandleTimelinePan(PointerEventArgs e, Grid content)
    {
        double deltaX = clickPos.X - e.GetPosition(content).X;
        double deltaY = clickPos.Y - e.GetPosition(content).Y;
        double newOffsetX = ScrollOffset.X + deltaX;
        double newOffsetY = ScrollOffset.Y + deltaY;
        newOffsetX = Math.Clamp(newOffsetX, 0, _timelineKeyFramesScroll.ScrollBarMaximum.X);
        newOffsetY = Math.Clamp(newOffsetY, 0, _timelineKeyFramesScroll.ScrollBarMaximum.Y);
        ScrollOffset = new Vector(newOffsetX, newOffsetY);
            
        extendingElement.Margin += new Thickness(deltaX, 0, 0, 0);
    }

    private void HandleMoveSelection(PointerEventArgs e, Grid content)
    {
        double x = e.GetPosition(content).X;
        double y = e.GetPosition(content).Y;
        double width = x - clickPos.X;
        double height = y - clickPos.Y;
        _selectionRectangle.Width = Math.Abs(width);
        _selectionRectangle.Height = Math.Abs(height);
        Thickness margin = new Thickness(Math.Min(clickPos.X, x), Math.Min(clickPos.Y, y), 0, 0);
        _selectionRectangle.Margin = margin;
        ClearSelectedKeyFrames();

        SelectAllWithinBounds(_selectionRectangle.Bounds);
    }

    private void SelectAllWithinBounds(Rect bounds)
    {
        var frames = _keyFramesHost.ItemsPanelRoot.GetVisualDescendants().OfType<KeyFrame>();
        foreach (var frame in frames)
        {
            var translated = frame.TranslatePoint(new Point(0, 0), _contentGrid);
            Rect frameBounds = new Rect(translated.Value.X, translated.Value.Y, frame.Bounds.Width, frame.Bounds.Height);
            if (bounds.Contains(frameBounds))
            {
                SelectKeyFrame(frame.Item, false);
            }
        }
    }

    private void ContentOnPointerLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (e.Source is not Grid content)
        {
            return;
        }

        Cursor = new Cursor(StandardCursorType.Arrow);
        _selectionRectangle.IsVisible = false;
    }

    private int MousePosToFrame(PointerEventArgs e, bool round = true)
    {
        double x = e.GetPosition(_contentGrid).X;
        x -= MinLeftOffset;
        int frame;
        if (round)
        {
            frame = (int)Math.Round(x / Scale) + 1;
        }
        else
        {
            frame = (int)Math.Floor(x / Scale) + 1;
        }

        frame = Math.Max(1, frame);
        return frame;
    }

    private static void IsPlayingChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Sender is not Timeline timeline)
        {
            return;
        }

        if (timeline.IsPlaying)
        {
            timeline._playTimer.Start();
        }
        else
        {
            timeline._playTimer.Stop();
        }
    }

    private static void FpsChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Sender is not Timeline timeline)
        {
            return;
        }

        timeline._playTimer.Interval = TimeSpan.FromMilliseconds(1000f / timeline.Fps);
    }

    private static void OnKeyFramesChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Sender is not Timeline timeline)
        {
            return;
        }

        if (e.OldValue is KeyFrameCollection oldCollection)
        {
            oldCollection.KeyFrameAdded -= timeline.KeyFrames_KeyFrameAdded;
            oldCollection.KeyFrameRemoved -= timeline.KeyFrames_KeyFrameRemoved;
        }

        if (e.NewValue is KeyFrameCollection newCollection)
        {
            newCollection.KeyFrameAdded += timeline.KeyFrames_KeyFrameAdded;
            newCollection.KeyFrameRemoved += timeline.KeyFrames_KeyFrameRemoved;
        }
    }

    private void KeyFrames_KeyFrameAdded(KeyFrameViewModel keyFrame)
    {
        keyFrame.PropertyChanged += KeyFrameOnPropertyChanged;
        PropertyChanged(this, new PropertyChangedEventArgs(nameof(SelectedKeyFrames)));
    }

    private void KeyFrames_KeyFrameRemoved(KeyFrameViewModel keyFrame)
    {
        if (SelectedKeyFrames.Contains(keyFrame))
        {
            keyFrame.Document.AnimationDataViewModel.RemoveSelectedKeyFrame(keyFrame.Id);
            keyFrame.PropertyChanged -= KeyFrameOnPropertyChanged;
        }
        
        PropertyChanged(this, new PropertyChangedEventArgs(nameof(SelectedKeyFrames)));
    }
    
    private void KeyFrameOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is KeyFrameViewModel keyFrame)
        {
            if (e.PropertyName == nameof(KeyFrameViewModel.IsSelected))
            {
                PropertyChanged(this, new PropertyChangedEventArgs(nameof(SelectedKeyFrames)));
            }
        }
    }
}
