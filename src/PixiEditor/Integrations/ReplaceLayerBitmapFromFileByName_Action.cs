using PixiEditor.ChangeableDocument.Actions;

namespace PixiEditor.Integrations;
internal class ReplaceLayerBitmapFromFileByName_Action : IAction
{
    private string groupName;
    private string colorName;
    private string colorPath;
    private bool ignoreInUndo;

    public ReplaceLayerBitmapFromFileByName_Action(string groupName, string colorName, string colorPath, bool IgnoreInUndo)
    {
        this.groupName = groupName;
        this.colorName = colorName;
        this.colorPath = colorPath;
        ignoreInUndo = IgnoreInUndo;
    }
}
