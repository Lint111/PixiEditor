﻿using PixiEditor.DrawingApi.Core;
using PixiEditor.DrawingApi.Core.Surfaces.ImageData;
using PixiEditor.Extensions.Common.Localization;
using PixiEditor.Models.IO;
using PixiEditor.ViewModels.Document;

namespace PixiEditor.Models.Files;

internal abstract class VideoFileType : IoFileType
{
    public override FileTypeDialogDataSet.SetKind SetKind { get; } = FileTypeDialogDataSet.SetKind.Video;

    public override async Task<SaveResult> TrySave(string pathWithExtension, DocumentViewModel document,
        ExportConfig config, ExportJob? job)
    {
        if (config.AnimationRenderer is null)
            return SaveResult.UnknownError;

        List<Image> frames = new(); 
        
        job?.Report(0, new LocalizedString("WARMING_UP"));
        
        int frameRendered = 0;
        int totalFrames = document.AnimationDataViewModel.FramesCount;

        document.RenderFrames(frames, surface =>
        {
            job?.CancellationTokenSource.Token.ThrowIfCancellationRequested();
            frameRendered++;
            job?.Report(((double)frameRendered / totalFrames) / 2, new LocalizedString("RENDERING_FRAME", frameRendered, totalFrames));
            if (config.ExportSize != surface.Size)
            {
                return surface.ResizeNearestNeighbor(config.ExportSize);
            }

            return surface;
        });
        
        job?.Report(0.5, new LocalizedString("RENDERING_VIDEO"));
        CancellationToken token = job?.CancellationTokenSource.Token ?? CancellationToken.None;
        var result = await config.AnimationRenderer.RenderAsync(frames, pathWithExtension, token, progress =>
        {
            job?.Report((progress / 100f) * 0.5f + 0.5, new LocalizedString("RENDERING_VIDEO"));
        });
        
        job?.Report(1, new LocalizedString("FINISHED"));
        
        foreach (var frame in frames)
        {
            frame.Dispose();
        } 

        return result ? SaveResult.Success : SaveResult.UnknownError;
    }
}
