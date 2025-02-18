﻿using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Drawie.Backend.Core.ColorsImpl;
using PixiEditor.Extensions.Common.Localization;
using PixiEditor.Extensions.CommonApi.UserPreferences;
using PixiEditor.Helpers;
using PixiEditor.Helpers.Collections;
using PixiEditor.Models.AnalyticsAPI;
using PixiEditor.Models.Commands;
using PixiEditor.Models.Config;
using PixiEditor.Models.Controllers;
using PixiEditor.Models.Dialogs;
using PixiEditor.Models.DocumentModels;
using PixiEditor.Models.Files;
using PixiEditor.Models.Handlers;
using PixiEditor.OperatingSystem;
using PixiEditor.ViewModels.Document;
using PixiEditor.ViewModels.Menu;
using PixiEditor.ViewModels.SubViewModels;
using PixiEditor.ViewModels.SubViewModels.AdditionalContent;
using PixiEditor.ViewModels.Tools;
using PixiEditor.Views.Dialogs;

namespace PixiEditor.ViewModels;

internal partial class ViewModelMain : ViewModelBase, ICommandsHandler
{
    public static ViewModelMain Current { get; set; }
    public IServiceProvider Services { get; private set; }

    public Action CloseAction { get; set; }
    public event Action OnStartupEvent;
    public FileViewModel FileSubViewModel { get; set; }
    public UpdateViewModel UpdateSubViewModel { get; set; }
    public IToolsHandler ToolsSubViewModel { get; set; }
    public IoViewModel IoSubViewModel { get; set; }
    public LayersViewModel LayersSubViewModel { get; set; }
    public ClipboardViewModel ClipboardSubViewModel { get; set; }
    public UndoViewModel UndoSubViewModel { get; set; }
    public SelectionViewModel SelectionSubViewModel { get; set; }
    public ViewOptionsViewModel ViewportSubViewModel { get; set; }
    public ColorsViewModel ColorsSubViewModel { get; set; }
    public MiscViewModel MiscSubViewModel { get; set; }
    public DiscordViewModel DiscordViewModel { get; set; }
    public DebugViewModel DebugSubViewModel { get; set; }
    public DocumentManagerViewModel DocumentManagerSubViewModel { get; set; }
    public CommandController CommandController { get; set; }
    public ShortcutController ShortcutController { get; set; }
    public StylusViewModel StylusSubViewModel { get; set; }
    public WindowViewModel WindowSubViewModel { get; set; }
    public SearchViewModel SearchSubViewModel { get; set; }
    public RegistryViewModel RegistrySubViewModel { get; set; }
    public AdditionalContentViewModel AdditionalContentSubViewModel { get; set; }
    public ExtensionsViewModel ExtensionsSubViewModel { get; set; }
    public LayoutViewModel LayoutSubViewModel { get; set; }
    public MenuBarViewModel MenuBarViewModel { get; set; }
    public AnimationsViewModel AnimationsSubViewModel { get; set; }
    public NodeGraphManagerViewModel NodeGraphManager { get; set; }

    public IPreferences Preferences { get; set; }
    public ILocalizationProvider LocalizationProvider { get; set; }

    public ConfigManager Config { get; set; }    
    
    public LocalizedString ActiveActionDisplay
    {
        get
        {
            if (ActionDisplays.HasActive())
            {
                return ActionDisplays.GetActive();
            }

            var documentDisplay = DocumentManagerSubViewModel.ActiveDocument?.ActionDisplays;
            if (documentDisplay != null && documentDisplay.HasActive())
            {
                return documentDisplay.GetActive();
            }

            return ToolsSubViewModel.ActiveTool?.ActionDisplay ?? default;
        }
    }

    public ActionDisplayList ActionDisplays { get; }
    public bool UserWantsToClose { get; private set; }

    public ViewModelMain()
    {
        Current = this;
        ActionDisplays = new ActionDisplayList(() => OnPropertyChanged(nameof(ActiveActionDisplay)));
    }

    public void Setup(IServiceProvider services)
    {
        Services = services;

        Config = new ConfigManager(); 

        Preferences = services.GetRequiredService<IPreferences>();
        Preferences.Init();
        
        SupportedFilesHelper.InitFileTypes(services.GetServices<IoFileType>());

        CommandController = services.GetService<CommandController>();

        LocalizationProvider = services.GetRequiredService<ILocalizationProvider>();
        string code = Preferences.GetPreference<string>("LanguageCode", null);
        LocalizationProvider.LoadData(code);

        WindowSubViewModel = services.GetService<WindowViewModel>();
        LayoutSubViewModel = services.GetService<LayoutViewModel>();

        DocumentManagerSubViewModel = services.GetRequiredService<DocumentManagerViewModel>();
        SelectionSubViewModel = services.GetService<SelectionViewModel>();

        FileSubViewModel = services.GetService<FileViewModel>();
        ToolsSubViewModel = services.GetService<IToolsHandler>();
        ToolsSubViewModel.SelectedToolChanged += ToolsSubViewModel_SelectedToolChanged;

        IoSubViewModel = services.GetService<IoViewModel>();
        LayersSubViewModel = services.GetService<LayersViewModel>();
        ClipboardSubViewModel = services.GetService<ClipboardViewModel>();
        UndoSubViewModel = services.GetService<UndoViewModel>();
        ViewportSubViewModel = services.GetService<ViewOptionsViewModel>();
        ColorsSubViewModel = services.GetService<ColorsViewModel>();
        ColorsSubViewModel?.SetupPaletteProviders(services);

        ToolSetsConfig toolSetConfig = Config.GetConfig<ToolSetsConfig>("ToolSetsConfig");
        ToolsSubViewModel?.SetupTools(services, toolSetConfig);

        DiscordViewModel = services.GetService<DiscordViewModel>();
        UpdateSubViewModel = services.GetService<UpdateViewModel>();
        DebugSubViewModel = services.GetService<DebugViewModel>();

        StylusSubViewModel = services.GetService<StylusViewModel>();
        RegistrySubViewModel = services.GetService<RegistryViewModel>();

        AdditionalContentSubViewModel = services.GetService<AdditionalContentViewModel>();
        MenuBarViewModel = new MenuBarViewModel(AdditionalContentSubViewModel);

        CommandController.Init(services);
        LayoutSubViewModel.LayoutManager.InitLayout(this);
        MenuBarViewModel.Init(services, CommandController);

        MiscSubViewModel = services.GetService<MiscViewModel>();

        ShortcutController = new ShortcutController();

        ToolsSubViewModel?.SetupToolsTooltipShortcuts();

        SearchSubViewModel = services.GetService<SearchViewModel>();
        
        AnimationsSubViewModel = services.GetService<AnimationsViewModel>();
        
        NodeGraphManager = services.GetService<NodeGraphManagerViewModel>();
        
        ExtensionsSubViewModel = services.GetService<ExtensionsViewModel>(); // Must be last

        DocumentManagerSubViewModel.ActiveDocumentChanged += OnActiveDocumentChanged;
    }

    public bool DocumentIsNotNull(object property)
    {
        return DocumentManagerSubViewModel.ActiveDocument is not null;
    }

    public bool DocumentIsNotNull((Color oldColor, Color newColor) obj)
    {
        return DocumentIsNotNull(null);
    }

    [RelayCommand]
    public async Task CloseWindow()
    {
        UserWantsToClose = await DisposeAllDocumentsWithSaveConfirmation();

        if (UserWantsToClose)
        {
            var analytics = Services.GetService<AnalyticsPeriodicReporter>();
            if (analytics != null)
            {
                await analytics.StopAsync();
            }
        }
    }

    private void ToolsSubViewModel_SelectedToolChanged(object sender, SelectedToolEventArgs e)
    {
        if (e.OldTool != null)
            ((ToolViewModel)e.OldTool).PropertyChanged -= SelectedTool_PropertyChanged;
        ((ToolViewModel)e.NewTool).PropertyChanged += SelectedTool_PropertyChanged;

        NotifyToolActionDisplayChanged();
    }

    private void SelectedTool_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ToolViewModel.ActionDisplay))
        {
            NotifyToolActionDisplayChanged();
        }
    }

    public void NotifyToolActionDisplayChanged()
    {
        if (!ActionDisplays.Any()) OnPropertyChanged(nameof(ActiveActionDisplay));
    }

    /// <summary>
    /// Closes documents with unsaved changes confirmation dialog.
    /// </summary>
    /// <returns>If documents was removed successfully.</returns>
    private async Task<bool> DisposeAllDocumentsWithSaveConfirmation()
    {
        int docCount = DocumentManagerSubViewModel.Documents.Count;
        for (int i = 0; i < docCount; i++)
        {
            WindowSubViewModel.MakeDocumentViewportActive(DocumentManagerSubViewModel.Documents.First());
            bool canceled = !await DisposeActiveDocumentWithSaveConfirmation();
            if (canceled)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Disposes the active document after showing the unsaved changes confirmation dialog.
    /// </summary>
    /// <returns>If the document was closed successfully.</returns>
    public async Task<bool> DisposeActiveDocumentWithSaveConfirmation()
    {
        if (DocumentManagerSubViewModel.ActiveDocument is null)
            return false;
        return await DisposeDocumentWithSaveConfirmation(DocumentManagerSubViewModel.ActiveDocument);
    }

    public async Task<bool> DisposeDocumentWithSaveConfirmation(DocumentViewModel document)
    {
        const string ConfirmationDialogTitle = "UNSAVED_CHANGES";
        const string ConfirmationDialogMessage = "DOCUMENT_MODIFIED_SAVE";


        ConfirmationType result = ConfirmationType.No;
        if (!document.AllChangesSaved)
        {
            result = await ConfirmationDialog.Show(ConfirmationDialogMessage, ConfirmationDialogTitle);
            if (result == ConfirmationType.Yes)
            {
                if (!await FileSubViewModel.SaveDocument(document, false))
                    return false;
            }
        }

        if (result != ConfirmationType.Canceled)
        {
            if (!DocumentManagerSubViewModel.Documents.Remove(document))
                throw new InvalidOperationException("Trying to close a document that's not in the documents collection. Likely, the document wasn't added there after creation by mistake.");

            if (DocumentManagerSubViewModel.ActiveDocument == document)
            {
                if (DocumentManagerSubViewModel.Documents.Count > 0)
                    WindowSubViewModel.MakeDocumentViewportActive(DocumentManagerSubViewModel.Documents.Last());
                else
                    WindowSubViewModel.MakeDocumentViewportActive(null);
            }

            WindowSubViewModel.CloseViewportsForDocument(document);
            document.Dispose();

            return true;
        }
        return false;
    }

    public void OnStartup()
    {
        OnStartupEvent?.Invoke();
    }

    private void OnActiveDocumentChanged(object sender, DocumentChangedEventArgs e)
    {
        NotifyToolActionDisplayChanged();
        if (e.OldDocument is not null)
            e.OldDocument.SizeChanged -= ActiveDocument_DocumentSizeChanged;
        if (e.NewDocument is not null)
            e.NewDocument.SizeChanged += ActiveDocument_DocumentSizeChanged;
    }

    private void ActiveDocument_DocumentSizeChanged(object sender, DocumentSizeChangedEventArgs e)
    {
        foreach (var viewport in WindowSubViewModel.Viewports.Where(viewport => viewport.Document == e.Document))
        {
            viewport.CenterViewportTrigger.Execute(this, e.NewSize);
        }
    }
}
