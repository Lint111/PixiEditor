﻿using Avalonia;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using PixiEditor.Extensions;
using PixiEditor.Extensions.CommonApi.Windowing;
using PixiEditor.Extensions.Runtime;
using PixiEditor.Helpers.Extensions;
using PixiEditor.Models.ExtensionServices;
using PixiEditor.UI.Common.Localization;
using PixiEditor.Views;
using PixiEditor.Views.Auth;
using PixiEditor.Views.Windows;

namespace PixiEditor.ViewModels.SubViewModels;

internal class ExtensionsViewModel : SubViewModel<ViewModelMain>
{
    public ExtensionLoader ExtensionLoader { get; }

    public ExtensionsViewModel(ViewModelMain owner, ExtensionLoader loader) : base(owner)
    {
        ExtensionLoader = loader;
    }

    public void Init()
    {
        WindowProvider windowProvider = (WindowProvider)Owner.Services.GetService<IWindowProvider>();
        RegisterCoreWindows(windowProvider);
        Owner.OnEarlyStartupEvent += Owner_OnEarlyStartupEvent;
        Owner.OnUserReady += Owner_OnUserReady;
        if (Owner.AttachedWindow != null)
        {
            OwnerOnAttachedToWindow(Owner.AttachedWindow);
        }
        else
        {
            Owner.AttachedToWindow += OwnerOnAttachedToWindow;
        }
    }

    private void OwnerOnAttachedToWindow(MainWindow obj)
    {
        if (obj.IsLoaded)
        {
            MainWindowLoaded(obj, null);
        }
        else
        {
            obj.Loaded += MainWindowLoaded;
        }
    }

    public void LoadExtensionAdHoc(string extension)
    {
        if (extension.EndsWith(".pixiext"))
        {
            var loadedExtension = ExtensionLoader.LoadExtension(extension);
            if (loadedExtension is null)
            {
                return;
            }

            ILocalizationProvider.Current.LoadExtensionData(loadedExtension.Metadata.Localization?.Languages,
                loadedExtension.Location);
            loadedExtension.Initialize(new ExtensionServices(Owner.Services));
            if (Owner.AttachedWindow != null && Owner.AttachedWindow.IsLoaded)
            {
                loadedExtension.MainWindowLoaded();
            }

            if (Owner.IsUserReady)
            {
                loadedExtension.UserReady();
            }
        }
    }

    private void RegisterCoreWindows(WindowProvider? windowProvider)
    {
        windowProvider?.RegisterWindow<PalettesBrowser>();
        windowProvider?.RegisterWindow<HelloTherePopup>();
        windowProvider?.RegisterWindow<LoginPopup>();
    }

    private void Owner_OnEarlyStartupEvent()
    {
        ExtensionLoader.InitializeExtensions(new ExtensionServices(Owner.Services));
    }

    private void Owner_OnUserReady()
    {
        ExtensionLoader.InvokeOnUserReady();
    }

    private void MainWindowLoaded(object? sender, RoutedEventArgs e)
    {
        ExtensionLoader.InvokeMainWindowLoaded();
    }
}
