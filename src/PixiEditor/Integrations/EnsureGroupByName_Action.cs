using PixiEditor.ChangeableDocument.Actions;

namespace PixiEditor.Integrations;
internal class EnsureGroupByName_Action : IMakeChangeAction
{
    public string GroupName { get; }
    internal EnsureGroupByName_Action(string groupName)
    {
        GroupName = groupName;
    }
}
