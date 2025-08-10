using PixiEditor.ChangeableDocument.Actions;

namespace PixiEditor.Integrations;
internal class EnsureImageLayerByName_Action : IAction
{
    public EnsureImageLayerByName_Action(string groupName, string colorName, int width, int height)
    {
        GroupName = groupName;
        ColorName = colorName;
        Width = width;
        Height = height;
    }

    public string GroupName { get; }
    public string ColorName { get; }
    public int Width { get; }
    public int Height { get; }
}