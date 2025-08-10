namespace BlendPixi;

using PixiEditor.Extensions.Sdk;

public class BlendPixiExtension : PixiEditorExtension
{
    public override void OnLoaded()
    {
    }
    
    public override void OnInitialized()
    {
        Api.Logger.Log("F!");
    }

}
