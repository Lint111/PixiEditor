﻿using System.Diagnostics;
using Avalonia.Media;
using PixiEditor.AvaloniaUI.Models.Commands.Evaluators;
using PixiEditor.AvaloniaUI.Models.Input;
using PixiEditor.AvaloniaUI.ViewModels;
using PixiEditor.Extensions.Common.Localization;

namespace PixiEditor.AvaloniaUI.Models.Commands.Commands;

[DebuggerDisplay("{InternalName,nq} ('{DisplayName,nq}')")]
internal abstract partial class Command : PixiObservableObject
{
    private KeyCombination _shortcut;

    public bool IsDebug { get; init; }

    public string InternalName { get; init; }

    public string Icon { get; init; }

    public IconEvaluator IconEvaluator { get; init; }

    public LocalizedString DisplayName { get; set; }

    public LocalizedString Description { get; set; }

    public CommandMethods Methods { get; init; }

    public KeyCombination DefaultShortcut { get; init; }

    public KeyCombination Shortcut
    {
        get => _shortcut;
        set
        {
            var oldValue = _shortcut;
            if (SetProperty(ref _shortcut, value))
            {
                ShortcutChanged?.Invoke(this, new(oldValue, value));
            }
        }
    }
    
    public Type? ShortcutContext { get; init; }

    public string? MenuItemPath { get; init; }

    public int MenuItemOrder { get; init; } = 100;

    public event ShortcutChangedEventHandler ShortcutChanged;
    public event Action CanExecuteChanged;

    public abstract object GetParameter();

    protected Command(Action<object> onExecute, CanExecuteEvaluator canExecute)
    {
        Methods = new(this, onExecute, canExecute);
        ILocalizationProvider.Current.OnLanguageChanged += OnLanguageChanged;
        /*InputLanguageManager.Current.InputLanguageChanged += (_, _) => this.OnPropertyChanged(nameof(Shortcut)); TODO: Didn't find implementation of this in Avalonia*/
    }

    private void OnLanguageChanged(Language obj)
    {
        DisplayName = new LocalizedString(DisplayName.Key, DisplayName.Parameters);
        Description = new LocalizedString(Description.Key, Description.Parameters);

        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Description));
    }

    public void Execute() => Methods.Execute(GetParameter());

    public bool CanExecute() => Methods.CanExecute(GetParameter());

    public IImage GetIcon() => IconEvaluator == null ? null : IconEvaluator.CallEvaluate(this, GetParameter());

    public delegate void ShortcutChangedEventHandler(Command command, ShortcutChangedEventArgs args);

    public void OnCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke();
    }
}
