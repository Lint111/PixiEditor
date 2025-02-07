using PixiEditor.ChangeableDocument.Changeables.Graph.Nodes.CombineSeparate;
using PixiEditor.Helpers.Extensions;
using PixiEditor.Models.Events;
using PixiEditor.Models.Handlers;
using PixiEditor.ViewModels.Nodes;
using PixiEditor.ViewModels.Nodes.Properties;

namespace PixiEditor.ViewModels.Document.Nodes.CombineSeparate;

[NodeViewModel("COMBINE_COLOR_NODE", "COLOR", "\uE808")]
internal class CombineColorNodeViewModel() : CombineSeparateColorNodeViewModel<CombineColorNode>(true);
