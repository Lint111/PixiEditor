using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Xaml.Interactivity;
using PixiEditor.ChangeableDocument;
using PixiEditor.ChangeableDocument.Actions.Undo;
using PixiEditor.ChangeableDocument.Changeables.Interfaces;
using PixiEditor.Integrations.Models;
using PixiEditor.Models.DocumentModels;

using IAction = PixiEditor.ChangeableDocument.Actions.IAction;

namespace PixiEditor.Integrations;
internal interface IFrameIngestor
{
    IReadOnlyList<(ActionSource source, IAction action)> BuildPacket(IReadOnlyDocument document, FrameDescriptor frame);
}

internal class LayerIngestor : IFrameIngestor
{
    private const string GroupName = "Blender Sync";

    public IReadOnlyList<(ActionSource source, IAction action)> BuildPacket(IReadOnlyDocument document, FrameDescriptor frame)
    {
        var actions = new List<(ActionSource source, IAction action)>
        {
            (ActionSource.Automated, new EnsureGroupByName_Action(GroupName))
        };

        string colorName = $"frame_{frame.FrameIndex:0000}_color";
        string? normalName = frame.NormalPath is null ? null
                             : $"frame_{frame.FrameIndex:0000}_normal";

        actions.Add((ActionSource.Automated,
            new EnsureImageLayerByName_Action(GroupName, colorName, frame.Width, frame.Height)));

        actions.Add((ActionSource.Automated,
            new ReplaceLayerBitmapFromFileByName_Action(GroupName, colorName, frame.ColorPath, IgnoreInUndo:true)));

        if (normalName is not null)
        {
            actions.Add((ActionSource.Automated,
                new EnsureImageLayerByName_Action(GroupName, normalName, frame.Width, frame.Height)));

            actions.Add((ActionSource.Automated,
                new ReplaceLayerBitmapFromFileByName_Action(GroupName, normalName, frame.NormalPath, IgnoreInUndo:true)));
        }

        actions.Add((ActionSource.Automated, new ChangeBoundary_Action()));

        return actions;

    }
}
