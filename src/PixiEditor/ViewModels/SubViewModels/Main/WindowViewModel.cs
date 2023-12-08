﻿using System.Collections.ObjectModel;
using System.Windows.Input;
using AvalonDock.Layout;
using PixiEditor.DrawingApi.Core.Numerics;
using PixiEditor.Helpers;
using PixiEditor.Models.Commands;
using PixiEditor.ViewModels.SubViewModels.Document;
using PixiEditor.Views;
using PixiEditor.Views.Dialogs;
using Command = PixiEditor.Models.Commands.Attributes.Commands.Command;

namespace PixiEditor.ViewModels.SubViewModels.Main;

#nullable enable
[Command.Group("PixiEditor.Window", "WINDOWS")]
internal class WindowViewModel : SubViewModel<ViewModelMain>
{
    private CommandController commandController;
    private ShortcutPopup? shortcutPopup;
    private ShortcutPopup ShortcutPopup => shortcutPopup ??= new(commandController);
    public RelayCommand<string> ShowAvalonDockWindowCommand { get; set; }
    public ObservableCollection<ViewportWindowViewModel> Viewports { get; } = new();
    public event EventHandler<ViewportWindowViewModel>? ActiveViewportChanged;

    private object? activeWindow;
    public object? ActiveWindow
    {
        get => activeWindow;
        set
        {
            if (activeWindow == value)
                return;
            activeWindow = value;
            RaisePropertyChanged(nameof(ActiveWindow));
            if (activeWindow is ViewportWindowViewModel viewport)
                ActiveViewportChanged?.Invoke(this, viewport);
        }
    }

    public WindowViewModel(ViewModelMain owner, CommandController commandController)
        : base(owner)
    {
        ShowAvalonDockWindowCommand = new(ShowAvalonDockWindow);
        this.commandController = commandController;
    }

    [Command.Basic("PixiEditor.Window.CreateNewViewport", "NEW_WINDOW_FOR_IMG", "NEW_WINDOW_FOR_IMG", IconPath = "@Images/Plus-square.png", CanExecute = "PixiEditor.HasDocument")]
    public void CreateNewViewport()
    {
        var doc = ViewModelMain.Current?.DocumentManagerSubViewModel.ActiveDocument;
        if (doc is null)
            return;
        CreateNewViewport(doc);
    }
    
    [Command.Basic("PixiEditor.Window.CenterActiveViewport", "CENTER_ACTIVE_VIEWPORT", "CENTER_ACTIVE_VIEWPORT", CanExecute = "PixiEditor.HasDocument")]
    public void CenterCurrentViewport()
    {
        if (ActiveWindow is ViewportWindowViewModel viewport)
            viewport.CenterViewportTrigger.Execute(this, viewport.Document.SizeBindable);
    }
    
    [Command.Basic("PixiEditor.Window.FlipHorizontally", "FLIP_VIEWPORT_HORIZONTALLY", "FLIP_VIEWPORT_HORIZONTALLY", CanExecute = "PixiEditor.HasDocument", IconPath = "FlipHorizontal.png")]
    public void FlipViewportHorizontally()
    {
        if (ActiveWindow is ViewportWindowViewModel viewport)
        {
            viewport.FlipX = !viewport.FlipX;
        }
    }
    
    [Command.Basic("PixiEditor.Window.FlipVertically", "FLIP_VIEWPORT_VERTICALLY", "FLIP_VIEWPORT_VERTICALLY", CanExecute = "PixiEditor.HasDocument", IconPath = "FlipVertical.png")]
    public void FlipViewportVertically()
    {
        if (ActiveWindow is ViewportWindowViewModel viewport)
        {
            viewport.FlipY = !viewport.FlipY;
        }
    }

    public void CreateNewViewport(DocumentViewModel doc)
    {
        Viewports.Add(new ViewportWindowViewModel(this, doc));
        foreach (var viewport in Viewports.Where(vp => vp.Document == doc))
        {
            viewport.RaisePropertyChanged(nameof(viewport.Index));
        }
    }

    public void MakeDocumentViewportActive(DocumentViewModel? doc)
    {
        if (doc is null)
        {
            ActiveWindow = null;
            Owner.DocumentManagerSubViewModel.MakeActiveDocumentNull();
            return;
        }
        ActiveWindow = Viewports.Where(viewport => viewport.Document == doc).FirstOrDefault();
    }

    public string CalculateViewportIndex(ViewportWindowViewModel viewport)
    {
        ViewportWindowViewModel[] viewports = Viewports.Where(a => a.Document == viewport.Document).ToArray();
        if (viewports.Length < 2)
            return "";
        return $"[{Array.IndexOf(viewports, viewport) + 1}]";
    }

    public void OnViewportWindowCloseButtonPressed(ViewportWindowViewModel viewport)
    {
        var viewports = Viewports.Where(vp => vp.Document == viewport.Document).ToArray();
        if (viewports.Length == 1)
        {
            Owner.DisposeDocumentWithSaveConfirmation(viewport.Document, false);
        }
        else
        {
            Viewports.Remove(viewport);
            foreach (var sibling in viewports)
            {
                sibling.RaisePropertyChanged(nameof(sibling.Index));
            }
        }
    }

    public void CloseViewportsForDocument(DocumentViewModel document)
    {
        var viewports = Viewports.Where(vp => vp.Document == document).ToArray();
        foreach (ViewportWindowViewModel viewport in viewports)
        {
            Viewports.Remove(viewport);
        }
    }

    [Command.Basic("PixiEditor.Window.OpenSettingsWindow", "OPEN_SETTINGS", "OPEN_SETTINGS_DESCRIPTIVE", Key = Key.OemComma, Modifiers = ModifierKeys.Control)]
    public static void OpenSettingsWindow(int page)
    {
        if (page < 0)
        {
            page = 0;
        }

        var settings = new SettingsWindow(page);
        settings.Show();
    }

    [Command.Basic("PixiEditor.Window.OpenStartupWindow", "OPEN_STARTUP_WINDOW", "OPEN_STARTUP_WINDOW")]
    public void OpenHelloThereWindow()
    {
        new HelloTherePopup(Owner.FileSubViewModel).Show();
    }

    [Command.Basic("PixiEditor.Window.OpenShortcutWindow", "OPEN_SHORTCUT_WINDOW", "OPEN_SHORTCUT_WINDOW", Key = Key.F1)]
    public void ShowShortcutWindow()
    {
        ShortcutPopup.Show();
        ShortcutPopup.Activate();
    }

    [Command.Basic("PixiEditor.Window.OpenPalettesBrowserWindow", "OPEN_PALETTE_BROWSER", "OPEN_PALETTE_BROWSER",
        IconPath = "Database.png")]
    public void ShowPalettesBrowserWindow()
    {
        PalettesBrowser.Open(Owner.ColorsSubViewModel.PaletteProvider, Owner.ColorsSubViewModel.ImportPaletteCommand,
            Owner.DocumentManagerSubViewModel.ActiveDocument?.Palette);
    }
        
    [Command.Basic("PixiEditor.Window.OpenAboutWindow", "OPEN_ABOUT_WINDOW", "OPEN_ABOUT_WINDOW")]
    public void OpenAboutWindow()
    {
        new AboutPopup().Show();
    }

    [Command.Basic("PixiEditor.Window.OpenNavigationWindow", "navigation", "OPEN_NAVIGATION_WINDOW", "OPEN_NAVIGATION_WINDOW")]
    public static void ShowAvalonDockWindow(string id)
    {
        if (MainWindow.Current?.LayoutRoot?.Manager?.Layout == null) return;
        var anchorables = new List<LayoutAnchorable>(MainWindow.Current.LayoutRoot.Manager.Layout
            .Descendents()
            .OfType<LayoutAnchorable>());

        foreach (var la in anchorables)
        {
            if (la.ContentId == id)
            {
                la.Show();
                la.IsActive = true;
            }
        }
    }
}
