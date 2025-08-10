using System;
using System.Reflection.Metadata;
using PixiEditor.ChangeableDocument.Actions;
using PixiEditor.Models.DocumentModels;

namespace PixiEditor.Integrations;
internal class ReplaceImageLayer : IAction
{
    private ActionAccumulator acc;
    private Document document;
    private Guid groupId;
    private string colorName;
    private int width;
    private int height;

    public ReplaceImageLayer(ActionAccumulator acc, Document document, Guid groupId, string colorName, int width, int height)
    {
        this.acc = acc;
        this.document = document;
        this.groupId = groupId;
        this.colorName = colorName;
        this.width = width;
        this.height = height;
    }
}
