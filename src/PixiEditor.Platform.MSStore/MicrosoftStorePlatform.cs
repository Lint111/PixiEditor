﻿namespace PixiEditor.Platform.MSStore;

public sealed class MicrosoftStorePlatform : IPlatform
{
    public string Id { get; } = "ms-store";
    public string Name => "Microsoft Store";

    public bool PerformHandshake()
    {
        return true;
    }

    public void Update()
    {

    }

    public IAdditionalContentProvider? AdditionalContentProvider { get; } = new MSAdditionalContentProvider();
}
