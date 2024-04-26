﻿using System.ComponentModel;
using DiscordRPC;
using PixiEditor.AvaloniaUI.Helpers;
using PixiEditor.AvaloniaUI.Models.Controllers;
using PixiEditor.AvaloniaUI.ViewModels.Document;
using PixiEditor.Extensions.Common.UserPreferences;

namespace PixiEditor.AvaloniaUI.ViewModels.SubViewModels;

internal class DiscordViewModel : SubViewModel<ViewModelMain>, IDisposable
{
    private DiscordRpcClient client;
    private string clientId;
    private DocumentViewModel currentDocument;

    public bool Enabled
    {
        get => client != null;
        set
        {
            client = null;
            return;
            
            if (Enabled != value)
            {
                if (value)
                {
                    Start();
                }
                else
                {
                    Stop();
                }
            }
        }
    }

    private bool showDocumentName = IPreferences.Current.GetPreference(nameof(ShowDocumentName), false);

    public bool ShowDocumentName
    {
        get => showDocumentName;
        set
        {
            if (showDocumentName != value)
            {
                showDocumentName = value;
                UpdatePresence(currentDocument);
            }
        }
    }

    private bool showDocumentSize = IPreferences.Current.GetPreference(nameof(ShowDocumentSize), true);

    public bool ShowDocumentSize
    {
        get => showDocumentSize;
        set
        {
            if (showDocumentSize != value)
            {
                showDocumentSize = value;
                UpdatePresence(currentDocument);
            }
        }
    }

    private bool showLayerCount = IPreferences.Current.GetPreference(nameof(ShowLayerCount), true);

    public bool ShowLayerCount
    {
        get => showLayerCount;
        set
        {
            if (showLayerCount != value)
            {
                showLayerCount = value;
                UpdatePresence(currentDocument);
            }
        }
    }

    public DiscordViewModel(ViewModelMain owner, string clientId)
        : base(owner)
    {
        return;
        
        Owner.DocumentManagerSubViewModel.ActiveDocumentChanged += DocumentChanged;
        this.clientId = clientId;

        Enabled = IPreferences.Current.GetPreference("EnableRichPresence", true);
        IPreferences.Current.AddCallback("EnableRichPresence", x => Enabled = (bool)x);
        IPreferences.Current.AddCallback(nameof(ShowDocumentName), x => ShowDocumentName = (bool)x);
        IPreferences.Current.AddCallback(nameof(ShowDocumentSize), x => ShowDocumentSize = (bool)x);
        IPreferences.Current.AddCallback(nameof(ShowLayerCount), x => ShowLayerCount = (bool)x);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Enabled = false;
    }

    public void Start()
    {
        client = new DiscordRpcClient(clientId);
        client.OnReady += OnReady;
        client.Initialize();
    }

    public void Stop()
    {
        client.ClearPresence();
        client.Dispose();
        client = null;
    }

    public void UpdatePresence(DocumentViewModel? document)
    {
        if (client == null)
        {
            return;
        }

        RichPresence richPresence = NewDefaultRP();

        if (document != null)
        {
            richPresence.WithTimestamps(new Timestamps(document.OpenedUTC));

            richPresence.Details = ShowDocumentName
                ? $"Editing {document.FileName.Limit(128)}" : "Editing an image";

            string state = string.Empty;

            if (ShowDocumentSize)
            {
                state = $"{document.Width}x{document.Height}";
            }

            if (ShowDocumentSize && ShowLayerCount)
            {
                state += ", ";
            }

            if (ShowLayerCount)
            {
                int count = CountLayers(document.StructureRoot);
                state += count == 1 ? "1 layer" : $"{count} layers";
            }

            richPresence.State = state;
        }

        client.SetPresence(richPresence);
    }

    private int CountLayers(FolderViewModel folder)
    {
        int counter = 0;
        foreach (var child in folder.Children)
        {
            if (child is LayerViewModel)
                counter++;
            else if (child is FolderViewModel innerFolder)
                counter += CountLayers(innerFolder);
        }
        return counter;
    }

    public void Dispose()
    {
        Enabled = false;
        GC.SuppressFinalize(this);
    }

    private static RichPresence NewDefaultRP()
    {
        return new RichPresence
        {
            Details = "Staring at absolutely",
            State = "nothing",

            Assets = new Assets
            {
                LargeImageKey = "editorlogo",
                LargeImageText = "You've discovered PixiEditor's logo",
                SmallImageKey = "github",
                SmallImageText = "Download PixiEditor (pixieditor.net/download)!"
            },
            Timestamps = new Timestamps()
            {
                Start = DateTime.UtcNow
            }
        };
    }
    
    private void DocumentChanged(object sender, DocumentChangedEventArgs e)
    {
        if (currentDocument != null)
        {
            currentDocument.PropertyChanged -= DocumentPropertyChanged;
            currentDocument.LayersChanged -= DocumentLayerChanged;
        }

        currentDocument = e.NewDocument;

        if (currentDocument != null)
        {
            UpdatePresence(currentDocument);
            currentDocument.PropertyChanged += DocumentPropertyChanged;
            currentDocument.LayersChanged += DocumentLayerChanged;
        }
    }

    private void DocumentLayerChanged(object sender, LayersChangedEventArgs e)
    {
        UpdatePresence(currentDocument);
    }

    private void DocumentPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(currentDocument.FileName)
            || e.PropertyName == nameof(currentDocument.Width)
            || e.PropertyName == nameof(currentDocument.Height))
        {
            UpdatePresence(currentDocument);
        }
    }

    private void OnReady(object sender, DiscordRPC.Message.ReadyMessage args)
    {
        UpdatePresence(Owner.DocumentManagerSubViewModel.ActiveDocument);
    }

    ~DiscordViewModel()
    {
        Enabled = false;
    }
}
