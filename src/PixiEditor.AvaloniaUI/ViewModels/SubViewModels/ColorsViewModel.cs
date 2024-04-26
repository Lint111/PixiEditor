﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using ColorPicker.Models;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PixiEditor.AvaloniaUI.Helpers.Extensions;
using PixiEditor.AvaloniaUI.Models.Commands.Attributes.Evaluators;
using PixiEditor.AvaloniaUI.Models.Commands.Search;
using PixiEditor.AvaloniaUI.Models.Controllers;
using PixiEditor.AvaloniaUI.Models.Dialogs;
using PixiEditor.AvaloniaUI.Models.ExtensionServices;
using PixiEditor.AvaloniaUI.Models.ExternalServices;
using PixiEditor.AvaloniaUI.Models.Handlers;
using PixiEditor.AvaloniaUI.Models.Palettes;
using PixiEditor.AvaloniaUI.Views.Dialogs;
using PixiEditor.AvaloniaUI.Views.Windows;
using PixiEditor.Extensions.Common.Localization;
using PixiEditor.Extensions.Palettes;
using PixiEditor.Extensions.Palettes.Parsers;
using Color = PixiEditor.DrawingApi.Core.ColorsImpl.Color;
using Colors = PixiEditor.DrawingApi.Core.ColorsImpl.Colors;
using Command = PixiEditor.AvaloniaUI.Models.Commands.Attributes.Commands.Command;
using ContextMenu = PixiEditor.AvaloniaUI.Models.Commands.XAML.ContextMenu;

namespace PixiEditor.AvaloniaUI.ViewModels.SubViewModels;

[Command.Group("PixiEditor.Colors", "PALETTE_COLORS")]
internal class ColorsViewModel : SubViewModel<ViewModelMain>, IColorsHandler
{
    public AsyncRelayCommand<List<PaletteColor>> ImportPaletteCommand { get; set; }
    private PaletteProvider paletteProvider;

    public PaletteProvider PaletteProvider
    {
        get => paletteProvider;
        set => this.SetProperty(ref paletteProvider, value);
    }

    public LocalPalettesFetcher LocalPaletteFetcher => _localPaletteFetcher ??=
        (LocalPalettesFetcher)PaletteProvider.DataSources.FirstOrDefault(x => x is LocalPalettesFetcher)!;

    private Color primaryColor = Colors.Black;
    private Color secondaryColor = Colors.White;
    private ColorState primaryColorState;
    private LocalPalettesFetcher _localPaletteFetcher;

    public ColorState PrimaryColorState
    {
        get => primaryColorState;
        set
        {
            primaryColorState = value;
            OnPropertyChanged(nameof(PrimaryColorState));
        }
    }

    public Color PrimaryColor // Primary color, hooked with left mouse button
    {
        get => primaryColor;
        set
        {
            if (primaryColor != value)
            {
                primaryColor = value;
                OnPropertyChanged(nameof(PrimaryColor));
            }
        }
    }

    public Color SecondaryColor
    {
        get => secondaryColor;
        set
        {
            if (secondaryColor != value)
            {
                secondaryColor = value;
                OnPropertyChanged(nameof(SecondaryColor));
            }
        }
    }

    public ColorsViewModel(ViewModelMain owner)
        : base(owner)
    {
        primaryColorState = new ColorState();
        primaryColorState.SetARGB(PrimaryColor.A, PrimaryColor.R, PrimaryColor.G, PrimaryColor.B);

        ImportPaletteCommand = new AsyncRelayCommand<List<PaletteColor>>(ImportPalette, CanImportPalette);
        Owner.OnStartupEvent += OwnerOnStartupEvent;
    }

    [Evaluator.CanExecute("PixiEditor.Colors.CanReplaceColors")]
    public bool CanReplaceColors()
    {
        return ViewModelMain.Current?.DocumentManagerSubViewModel?.ActiveDocument is not null;
    }

    [Command.Internal("PixiEditor.Colors.ReplaceColors")]
    public void ReplaceColors((PaletteColor oldColor, PaletteColor newColor) colors)
    {
        var doc = Owner.DocumentManagerSubViewModel.ActiveDocument;
        if (doc is null || colors.oldColor == colors.newColor)
            return;
        doc.Operations.ReplaceColor(colors.oldColor, colors.newColor);
    }

    [Command.Basic("PixiEditor.Colors.ReplaceSecondaryByPrimaryColor", false, "REPLACE_SECONDARY_BY_PRIMARY", "REPLACE_SECONDARY_BY_PRIMARY", IconEvaluator = "PixiEditor.Colors.ReplaceColorIcon")]
    [Command.Basic("PixiEditor.Colors.ReplacePrimaryBySecondaryColor", true, "REPLACE_PRIMARY_BY_SECONDARY", "REPLACE_PRIMARY_BY_SECONDARY_DESCRIPTIVE", IconEvaluator = "PixiEditor.Colors.ReplaceColorIcon")]
    public void ReplaceColors(bool replacePrimary)
    {
        PaletteColor oldColor = replacePrimary ? PrimaryColor.ToPaletteColor() : SecondaryColor.ToPaletteColor();
        PaletteColor newColor = replacePrimary ? SecondaryColor.ToPaletteColor() : PrimaryColor.ToPaletteColor();
        
        ReplaceColors((oldColor, newColor));
    }

    [Evaluator.Icon("PixiEditor.Colors.ReplaceColorIcon")]
    public IImage ReplaceColorsIcon(object command)
    {
        bool replacePrimary = command switch
        {
            CommandSearchResult result => (bool)result.Command.GetParameter(),
            Models.Commands.Commands.Command cmd => (bool)cmd.GetParameter(),
            _ => false
        };
        
        var oldColor = replacePrimary ? PrimaryColor : SecondaryColor;
        var newColor = replacePrimary ? SecondaryColor : PrimaryColor;
        
        var oldDrawing = new GeometryDrawing { Brush = new SolidColorBrush(oldColor.ToOpaqueMediaColor()), Pen = new Pen(Brushes.Gray, .5) };
        var oldGeometry = new EllipseGeometry(new Rect(5, 5, 5, 5));
        
        oldDrawing.Geometry = oldGeometry;
        
        var newDrawing = new GeometryDrawing { Brush = new SolidColorBrush(newColor.ToOpaqueMediaColor()), Pen = new Pen(Brushes.White, 1) };
        var newGeometry = new EllipseGeometry(new Rect(10, 10, 6, 6));

        newDrawing.Geometry = newGeometry;
        
        return new DrawingImage(new DrawingGroup
        {
            Children = new DrawingCollection
            {
                oldDrawing,
                newDrawing
            }
        });
    }

    private async void OwnerOnStartupEvent(object sender, EventArgs e)
    {
        await ImportLospecPalette();
    }

    [Command.Basic("PixiEditor.Colors.OpenPaletteBrowser", "OPEN_PALETTE_BROWSER", "OPEN_PALETTE_BROWSER", CanExecute = "PixiEditor.HasDocument", IconPath = "Globe.png")]
    public void OpenPalettesBrowser() 
    {
        var doc = Owner.DocumentManagerSubViewModel.ActiveDocument;
        if (doc is not null)
            PalettesBrowser.Open();
    } 

    private async Task ImportLospecPalette()
    {
        var args = StartupArgs.Args;
        var lospecPaletteArg = args.FirstOrDefault(x => x.StartsWith("lospec-palette://"));

        if (lospecPaletteArg != null)
        {
            var browser = PalettesBrowser.Open();

            browser.IsFetching = true;
            var palette = await LospecPaletteFetcher.FetchPalette(lospecPaletteArg.Split(@"://")[1].Replace("/", ""));
            if (palette != null)
            {
                if (LocalPalettesFetcher.PaletteExists(palette.Name))
                {
                    var consent = await ConfirmationDialog.Show(
                        new LocalizedString("OVERWRITE_PALETTE_CONSENT", palette.Name),
                        new LocalizedString("PALETTE_EXISTS"));
                    if (consent == ConfirmationType.No)
                    {
                        palette.Name = LocalPalettesFetcher.GetNonExistingName(palette.Name);
                    }
                    else if (consent == ConfirmationType.Canceled)
                    {
                        browser.IsFetching = false;
                        return;
                    }
                }

                await SavePalette(palette, browser);
            }
            else
            {
                await browser.UpdatePaletteList();
            }
        }
    }

    private async Task SavePalette(Palette palette, PalettesBrowser browser)
    {
        palette.FileName = $"{palette.Name}.pal";

        await LocalPaletteFetcher.SavePalette(
            palette.FileName,
            palette.Colors.Select(x => new PaletteColor(x.R, x.G, x.B)).ToArray());

        await browser.UpdatePaletteList();
        if (browser.SortedResults.Any(x => x.FileName == palette.FileName))
        {
            int indexOfImported =
                browser.SortedResults.IndexOf(browser.SortedResults.First(x => x.FileName == palette.FileName));
            browser.SortedResults.Move(indexOfImported, 0);
        }
        else
        {
            browser.SortedResults.Insert(0, palette);
        }
    }

    [Evaluator.CanExecute("PixiEditor.Colors.CanImportPalette")]
    public bool CanImportPalette(List<PaletteColor> paletteColors)
    {
        return paletteColors is not null && Owner.DocumentIsNotNull(paletteColors) && paletteColors.Count > 0;
    }

    [Command.Internal("PixiEditor.Colors.ImportPalette", CanExecute = "PixiEditor.Colors.CanImportPalette")]
    public async Task ImportPalette(List<PaletteColor> palette)
    {
        var doc = Owner.DocumentManagerSubViewModel.ActiveDocument;
        if (doc is null || doc.Palette.SequenceEqual(palette))
            return;

        if (doc.Palette.Count == 0 || await ConfirmationDialog.Show(new LocalizedString(
                    "REPLACE_PALETTE_CONSENT"),
                new LocalizedString("REPLACE_PALETTE")) == ConfirmationType.Yes)
        {
            doc.Palette.ReplaceRange(palette.Select(x => new PaletteColor(x.R, x.G, x.B)));
        }
    }

    [Evaluator.CanExecute("PixiEditor.Colors.CanSelectPaletteColor")]
    public bool CanSelectPaletteColor(int index)
    {
        var document = Owner.DocumentManagerSubViewModel.ActiveDocument;
        return document?.Palette is not null && document.Palette.Count > index;
    }

    [Evaluator.Icon("PixiEditor.Colors.FirstPaletteColorIcon")]
    public IImage GetPaletteColorIcon1() => GetPaletteColorIcon(0);
    [Evaluator.Icon("PixiEditor.Colors.SecondPaletteColorIcon")]
    public IImage GetPaletteColorIcon2() => GetPaletteColorIcon(1);
    [Evaluator.Icon("PixiEditor.Colors.ThirdPaletteColorIcon")]
    public IImage GetPaletteColorIcon3() => GetPaletteColorIcon(2);
    [Evaluator.Icon("PixiEditor.Colors.FourthPaletteColorIcon")]
    public IImage GetPaletteColorIcon4() => GetPaletteColorIcon(3);
    [Evaluator.Icon("PixiEditor.Colors.FifthPaletteColorIcon")]
    public IImage GetPaletteColorIcon5() => GetPaletteColorIcon(4);
    [Evaluator.Icon("PixiEditor.Colors.SixthPaletteColorIcon")]
    public IImage GetPaletteColorIcon6() => GetPaletteColorIcon(5);
    [Evaluator.Icon("PixiEditor.Colors.SeventhPaletteColorIcon")]
    public IImage GetPaletteColorIcon7() => GetPaletteColorIcon(6);
    [Evaluator.Icon("PixiEditor.Colors.EighthPaletteColorIcon")]
    public IImage GetPaletteColorIcon8() => GetPaletteColorIcon(7);
    [Evaluator.Icon("PixiEditor.Colors.NinthPaletteColorIcon")]
    public IImage GetPaletteColorIcon9() => GetPaletteColorIcon(8);
    [Evaluator.Icon("PixiEditor.Colors.TenthPaletteColorIcon")]
    public IImage GetPaletteColorIcon10() => GetPaletteColorIcon(9);


    private IImage GetPaletteColorIcon(int index)
    {
        var document = Owner.DocumentManagerSubViewModel.ActiveDocument;

        Color color;
        if (document?.Palette is null || document.Palette.Count <= index)
        {
            color = Colors.Gray;
        }
        else
        {
            PaletteColor paletteColor = document.Palette[index];
            color = new Color(paletteColor.R, paletteColor.G, paletteColor.B);
        }

        return ColorSearchResult.GetIcon(color);
    }

    [Command.Basic("PixiEditor.Colors.SelectFirstPaletteColor", "SELECT_COLOR_1", "SELECT_COLOR_1_DESCRIPTIVE", Key = Key.D1, Parameter = 0, CanExecute = "PixiEditor.Colors.CanSelectPaletteColor", IconEvaluator = "PixiEditor.Colors.FirstPaletteColorIcon")]
    [Command.Basic("PixiEditor.Colors.SelectSecondPaletteColor", "SELECT_COLOR_2", "SELECT_COLOR_2_DESCRIPTIVE", Key = Key.D2, Parameter = 1, CanExecute = "PixiEditor.Colors.CanSelectPaletteColor", IconEvaluator = "PixiEditor.Colors.SecondPaletteColorIcon")]
    [Command.Basic("PixiEditor.Colors.SelectThirdPaletteColor", "SELECT_COLOR_3", "SELECT_COLOR_3_DESCRIPTIVE", Key = Key.D3, Parameter = 2, CanExecute = "PixiEditor.Colors.CanSelectPaletteColor", IconEvaluator = "PixiEditor.Colors.ThirdPaletteColorIcon")]
    [Command.Basic("PixiEditor.Colors.SelectFourthPaletteColor", "SELECT_COLOR_4", "SELECT_COLOR_4_DESCRIPTIVE", Key = Key.D4, Parameter = 3, CanExecute = "PixiEditor.Colors.CanSelectPaletteColor", IconEvaluator = "PixiEditor.Colors.FourthPaletteColorIcon")]
    [Command.Basic("PixiEditor.Colors.SelectFifthPaletteColor", "SELECT_COLOR_5", "SELECT_COLOR_5_DESCRIPTIVE", Key = Key.D5, Parameter = 4, CanExecute = "PixiEditor.Colors.CanSelectPaletteColor", IconEvaluator = "PixiEditor.Colors.FifthPaletteColorIcon")]
    [Command.Basic("PixiEditor.Colors.SelectSixthPaletteColor", "SELECT_COLOR_6", "SELECT_COLOR_6_DESCRIPTIVE", Key = Key.D6, Parameter = 5, CanExecute = "PixiEditor.Colors.CanSelectPaletteColor", IconEvaluator = "PixiEditor.Colors.SixthPaletteColorIcon")]
    [Command.Basic("PixiEditor.Colors.SelectSeventhPaletteColor", "SELECT_COLOR_7", "SELECT_COLOR_7_DESCRIPTIVE", Key = Key.D7, Parameter = 6, CanExecute = "PixiEditor.Colors.CanSelectPaletteColor", IconEvaluator = "PixiEditor.Colors.SeventhPaletteColorIcon")]
    [Command.Basic("PixiEditor.Colors.SelectEighthPaletteColor", "SELECT_COLOR_8", "SELECT_COLOR_8_DESCRIPTIVE", Key = Key.D8, Parameter = 7, CanExecute = "PixiEditor.Colors.CanSelectPaletteColor", IconEvaluator = "PixiEditor.Colors.EighthPaletteColorIcon")]
    [Command.Basic("PixiEditor.Colors.SelectNinthPaletteColor", "SELECT_COLOR_9", "SELECT_COLOR_9_DESCRIPTIVE", Key = Key.D9, Parameter = 8, CanExecute = "PixiEditor.Colors.CanSelectPaletteColor", IconEvaluator = "PixiEditor.Colors.NinthPaletteColorIcon")]
    [Command.Basic("PixiEditor.Colors.SelectTenthPaletteColor", "SELECT_COLOR_10", "SELECT_COLOR_10_DESCRIPTIVE", Key = Key.D0, Parameter = 9, CanExecute = "PixiEditor.Colors.CanSelectPaletteColor", IconEvaluator = "PixiEditor.Colors.TenthPaletteColorIcon")]
    public void SelectPaletteColor(int index)
    {
        var document = Owner.DocumentManagerSubViewModel.ActiveDocument;
        if (document?.Palette is not null && document.Palette.Count > index)
        {
            PaletteColor paletteColor = document.Palette[index];
            PrimaryColor = new Color(paletteColor.R, paletteColor.G, paletteColor.B);
        }
    }

    [Command.Basic("PixiEditor.Colors.Swap", "SWAP_COLORS", "SWAP_COLORS_DESCRIPTIVE", Key = Key.X)]
    public void SwapColors(object parameter)
    {
        (PrimaryColor, SecondaryColor) = (SecondaryColor, PrimaryColor);
    }

    public void AddSwatch(PaletteColor color)
    {
        var doc = Owner.DocumentManagerSubViewModel.ActiveDocument;
        if (doc is null)
            return;
        if (!doc.Swatches.Contains(color))
        {
            doc.Swatches.Add(color);
        }
    }

    [Command.Internal("PixiEditor.Colors.RemoveSwatch")]
    public void RemoveSwatch(PaletteColor color)
    {
        var doc = Owner.DocumentManagerSubViewModel.ActiveDocument;
        if (doc is null)
            return;
        if (doc.Swatches.Contains(color))
        {
            doc.Swatches.Remove(color);
        }
    }

    [Command.Internal("PixiEditor.Colors.SelectColor")]
    public void SelectColor(PaletteColor color)
    {
        PrimaryColor = color.ToColor();
    }

    [Command.Basic("PixIEditor.Colors.AddPrimaryToPalettes", "ADD_PRIMARY_COLOR_TO_PALETTE", "ADD_PRIMARY_COLOR_TO_PALETTE_DESCRIPTIVE", CanExecute = "PixiEditor.HasDocument", IconPath = "CopyAdd.png")]
    public void AddPrimaryColorToPalette()
    {
        var palette = Owner.DocumentManagerSubViewModel.ActiveDocument.Palette;

        if (!palette.Contains(PrimaryColor.ToPaletteColor()))
        {
            palette.Add(PrimaryColor.ToPaletteColor());
        }
    }

    [Command.Internal("PixiEditor.CloseContextMenu")]
    public void CloseContextMenu(ContextMenu menu)
    {
        menu.Close();
    }

    public void SetupPaletteProviders(IServiceProvider services)
    {
        PaletteProvider = (PaletteProvider)services.GetService<IPaletteProvider>();
        //PaletteProvider.AvailableParsers =
        //    new ObservableCollection<PaletteFileParser>(services.GetServices<PaletteFileParser>());
        //var dataSources = services.GetServices<PaletteListDataSource>();

        //foreach (var dataSource in dataSources)
        //{
        //    PaletteProvider.RegisterDataSource(dataSource);
        //}
    }
}
