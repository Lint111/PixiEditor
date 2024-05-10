﻿using System.IO;
using PixiEditor.Extensions.Common.UserPreferences;
using PixiEditor.Helpers;
using PixiEditor.Models.DocumentModels;
using PixiEditor.Models.DocumentModels.Autosave;
using PixiEditor.Models.DocumentModels.Autosave.Enums;
using PixiEditor.Models.DocumentModels.Autosave.Structs;
using PixiEditor.Models.IO;

namespace PixiEditor.ViewModels.SubViewModels.Document;


internal class AutosaveDocumentViewModel : NotifyableObject
{
    private AutosaveStateData? autosaveStateData;
    public AutosaveStateData? AutosaveStateData
    {
        get => autosaveStateData;
        set => SetProperty(ref autosaveStateData, value);
    }

    private bool currentDocumentAutosaveEnabled = true;
    public bool CurrentDocumentAutosaveEnabled
    {
        get => currentDocumentAutosaveEnabled;
        set
        {
            if (currentDocumentAutosaveEnabled == value)
                return;
            
            SetProperty(ref currentDocumentAutosaveEnabled, value);
            StopOrStartAutosaverIfNecessary();
        }
    }

    private DocumentAutosaver? autosaver;
    private DocumentViewModel Document { get; }
    private Guid autosaveFileGuid = Guid.NewGuid();
    public string AutosavePath => AutosaveHelper.GetAutosavePath(autosaveFileGuid);
    
    public string LastAutosavedPath { get; set; }
    
    private static bool SaveUserFileEnabled => IPreferences.Current!.GetPreference(PreferencesConstants.AutosaveToDocumentPath, PreferencesConstants.AutosaveToDocumentPathDefault);
    private static double AutosavePeriod => IPreferences.Current!.GetPreference(PreferencesConstants.AutosavePeriodMinutes, PreferencesConstants.AutosavePeriodDefault); 
    private static bool AutosaveEnabledGlobally => IPreferences.Current!.GetPreference(PreferencesConstants.AutosaveEnabled, PreferencesConstants.AutosaveEnabledDefault); 
    
    public AutosaveDocumentViewModel(DocumentViewModel document, DocumentInternalParts internals)
    {
        Document = document;
        internals.ChangeController.UpdateableChangeEnded += ((_, _) => autosaver?.OnUpdateableChangeEnded());
        IPreferences.Current!.AddCallback(PreferencesConstants.AutosaveEnabled, PreferenceUpdateCallback);
        IPreferences.Current!.AddCallback(PreferencesConstants.AutosavePeriodMinutes, PreferenceUpdateCallback);
        IPreferences.Current!.AddCallback(PreferencesConstants.AutosaveToDocumentPath, PreferenceUpdateCallback);
        StopOrStartAutosaverIfNecessary();
    }

    private void PreferenceUpdateCallback(object _)
    {
        StopOrStartAutosaverIfNecessary();
    }

    private void StopAutosaver()
    {
        autosaver?.Dispose();
        autosaver = null;
        AutosaveStateData = null;
    }

    private void StopOrStartAutosaverIfNecessary()
    {
        StopAutosaver();
        if (!AutosaveEnabledGlobally || !CurrentDocumentAutosaveEnabled)
            return;
        
        autosaver = new DocumentAutosaver(Document, TimeSpan.FromMinutes(AutosavePeriod), SaveUserFileEnabled);
        autosaver.JobChanged += (_, _) => AutosaveStateData = autosaver.State;
        AutosaveStateData = autosaver.State;
    }

    public bool AutosaveOnClose()
    {
        if (Document.AllChangesSaved)
            return true;

        try
        {
            string filePath = AutosavePath;
            Directory.CreateDirectory(Directory.GetParent(filePath)!.FullName);
            bool success = Exporter.TrySave(Document, filePath) == SaveResult.Success;
            if (success)
                AddAutosaveHistoryEntry(AutosaveHistoryType.OnClose, AutosaveHistoryResult.SavedBackup);
            
            return success;
        }
        catch (Exception e)
        {
            return false;
        }
    }

    public void AddAutosaveHistoryEntry(AutosaveHistoryType type, AutosaveHistoryResult result)
    {
        List<AutosaveHistorySession>? historySessions = IPreferences.Current!.GetLocalPreference<List<AutosaveHistorySession>>(PreferencesConstants.AutosaveHistory);
        if (historySessions is null)
            historySessions = new();

        AutosaveHistorySession currentSession;
        if (historySessions.Count == 0 || historySessions[^1].SessionGuid != ViewModelMain.Current.CurrentSessionId)
        {
            currentSession = new AutosaveHistorySession(ViewModelMain.Current.CurrentSessionId, ViewModelMain.Current.LaunchDateTime);
            historySessions.Add(currentSession);
        }
        else
        {
            currentSession = historySessions[^1];
        }

        AutosaveHistoryEntry entry = new(DateTime.Now, type, result, autosaveFileGuid);
        currentSession.AutosaveEntries.Add(entry);
        
        IPreferences.Current.UpdateLocalPreference(PreferencesConstants.AutosaveHistory, historySessions);
    }

    public void PanicAutosaveFromDeadlockDetector()
    {
        /*
        string filePath = Path.Join(Paths.PathToUnsavedFilesFolder, $"autosave-{tempGuid}.pixi");
        Directory.CreateDirectory(Directory.GetParent(filePath)!.FullName);

        var result = Exporter.TrySave(Document, filePath);

        if (result == SaveResult.Success)
        {
            LastSavedPath = filePath;
        }*/
    }
    
    public void SetTempFileGuidAndLastSavedPath(Guid guid, string lastSavedPath)
    {
        autosaveFileGuid = guid;
        LastAutosavedPath = lastSavedPath;
    }

    public void OnDocumentClosed()
    {
        CurrentDocumentAutosaveEnabled = false;
        IPreferences.Current!.RemoveCallback(PreferencesConstants.AutosaveEnabled, PreferenceUpdateCallback);
        IPreferences.Current!.RemoveCallback(PreferencesConstants.AutosavePeriodMinutes, PreferenceUpdateCallback);
        IPreferences.Current!.RemoveCallback(PreferencesConstants.AutosaveToDocumentPath, PreferenceUpdateCallback);
    }
}
