﻿using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using PixiEditor.AvaloniaUI.Models.Commands;
using PixiEditor.AvaloniaUI.Models.Commands.Commands;
using PixiEditor.AvaloniaUI.Models.Input;
using PixiEditor.AvaloniaUI.ViewModels.Tools;

namespace PixiEditor.AvaloniaUI.Models.Controllers;

internal class ShortcutController
{
    public static bool ShortcutExecutionBlocked => _shortcutExecutionBlockers.Count > 0;

    private static readonly List<string> _shortcutExecutionBlockers = new List<string>();

    public IEnumerable<Command> LastCommands { get; private set; }

    public Dictionary<KeyCombination, ToolViewModel> TransientShortcuts { get; set; } = new();
    
    public Type? ActiveContext { get; private set; }

    public static void BlockShortcutExecution(string blocker)
    {
        if (_shortcutExecutionBlockers.Contains(blocker)) return;
        _shortcutExecutionBlockers.Add(blocker);
    }

    public static void UnblockShortcutExecution(string blocker)
    {
        if (!_shortcutExecutionBlockers.Contains(blocker)) return;
        _shortcutExecutionBlockers.Remove(blocker);
    }

    public static void UnblockShortcutExecutionAll()
    {
        _shortcutExecutionBlockers.Clear();
    }

    public KeyCombination GetToolShortcut<T>()
    {
        return GetToolShortcut(typeof(T));
    }

    public KeyCombination GetToolShortcut(Type type)
    {
        return CommandController.Current.Commands.First(x => x is Command.ToolCommand tool && tool.ToolType == type).Shortcut;
    }

    public void KeyPressed(Key key, KeyModifiers modifiers)
    {
        KeyCombination shortcut = new(key, modifiers);

        if (!ShortcutExecutionBlocked)
        {
            var commands = CommandController.Current.Commands[shortcut].Where(x => x.ShortcutContext is null || x.ShortcutContext == ActiveContext).ToList();

            if (!commands.Any())
            {
                return;
            }

            LastCommands = commands;

            foreach (var command in commands)
            {
                command.Execute();
            }
        }
    }

    public void OverwriteContext(Type getType)
    {
        ActiveContext = getType;
    }
    
    public void ClearContext(Type clearFrom)
    {
        if (ActiveContext == clearFrom)
        {
            ActiveContext = null;
        }
    }
}
