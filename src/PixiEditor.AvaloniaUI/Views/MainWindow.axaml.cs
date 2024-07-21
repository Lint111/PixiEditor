using System.Threading.Tasks;
using AsyncImageLoader.Loaders;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.OpenGL;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using PixiEditor.AvaloniaUI.Helpers;
using PixiEditor.AvaloniaUI.Initialization;
using PixiEditor.AvaloniaUI.Models.ExceptionHandling;
using PixiEditor.AvaloniaUI.Models.IO;
using PixiEditor.AvaloniaUI.ViewModels.SubViewModels;
using PixiEditor.DrawingApi.Core.Bridge;
using PixiEditor.DrawingApi.Skia;
using PixiEditor.Engine;
using PixiEditor.Extensions.CommonApi.UserPreferences;
using PixiEditor.Extensions.Runtime;
using PixiEditor.Platform;
using ViewModelMain = PixiEditor.AvaloniaUI.ViewModels.ViewModelMain;
using Window = Avalonia.Controls.Window;

namespace PixiEditor.AvaloniaUI.Views;

internal partial class MainWindow : Window
{
    private readonly IPreferences preferences;
    private readonly IPlatform platform;
    private readonly IServiceProvider services;
    private static ExtensionLoader extLoader;

    public new ViewModelMain DataContext { get => (ViewModelMain)base.DataContext; set => base.DataContext = value; }
    
    public static MainWindow? Current {
        get 
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.MainWindow as MainWindow;
            if (Application.Current is null)
                return null;
            throw new NotSupportedException("ApplicationLifetime is not supported");
        }
    }

    public MainWindow(ExtensionLoader extensionLoader)
    {
        (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).MainWindow = this;
        extLoader = extensionLoader;

        services = new ServiceCollection()
            .AddPlatform()
            .AddPixiEditor(extensionLoader)
            .AddExtensionServices(extensionLoader)
            .BuildServiceProvider();

        AsyncImageLoader.ImageLoader.AsyncImageLoader = new DiskCachedWebImageLoader();
        
        //GRContext recordingContext = GetGrRecordingContext();

        /*SkiaDrawingBackend skiaDrawingBackend = new SkiaDrawingBackend(recordingContext);
        DrawingBackendApi.SetupBackend(skiaDrawingBackend);*/
        
        SkiaPixiEngine engine = SkiaPixiEngine.Create();
        engine.SetupBackendWindowLess();

        preferences = services.GetRequiredService<IPreferences>();
        platform = services.GetRequiredService<IPlatform>();
        DataContext = services.GetRequiredService<ViewModelMain>();
        DataContext.Setup(services);

        InitializeComponent();
    }

    /*private static GRContext GetGrRecordingContext()
    {
        Compositor compositor = Compositor.TryGetDefaultCompositor();
        var interop = compositor.TryGetCompositionGpuInterop();
        var contextSharingFeature =
            compositor.TryGetRenderInterfaceFeature(typeof(IOpenGlTextureSharingRenderInterfaceContextFeature)).Result as IOpenGlTextureSharingRenderInterfaceContextFeature;

        if (contextSharingFeature.CanCreateSharedContext)
        {

            IGlContext? glContext = contextSharingFeature.CreateSharedContext();
            glContext.MakeCurrent();
            return GRContext.CreateGl(GRGlInterface.Create(glContext.GlInterface.GetProcAddress));
        }

        var contextFactory = AvaloniaLocator.Current.GetRequiredService<IPlatformGraphicsOpenGlContextFactory>();
        var ctx = contextFactory.CreateContext(null);
        ctx.MakeCurrent();
        var ctxInterface = GRGlInterface.Create(ctx.GlInterface.GetProcAddress);
        var grContext = GRContext.CreateGl(ctxInterface);
        return grContext;
    }*/

    public static MainWindow CreateWithRecoveredDocuments(CrashReport report, out bool showMissingFilesDialog)
    {
        var window = GetMainWindow();
        var fileVM = window.services.GetRequiredService<FileViewModel>();

        if (!report.TryRecoverDocuments(out var documents))
        {
            showMissingFilesDialog = true;
            return window;
        }

        var i = 0;

        foreach (var document in documents)
        {
            try
            {
                fileVM.OpenRecoveredDotPixi(document.Path, document.GetRecoveredBytes());
                i++;
            }
            catch (Exception e)
            {
                CrashHelper.SendExceptionInfoToWebhook(e);
            }
        }

        showMissingFilesDialog = documents.Count != i;

        return window;

        MainWindow GetMainWindow()
        {
            try
            {
                var app = (App)Application.Current;
                ClassicDesktopEntry entry = new(app.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime);
                return new MainWindow(entry.InitApp());
            }
            catch (Exception e)
            {
                CrashHelper.SendExceptionInfoToWebhook(e, true);
                throw;
            }
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        LoadingWindow.Instance?.SafeClose();
        Activate();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!DataContext.UserWantsToClose)
        {
            e.Cancel = true;
            Task.Run(async () =>
            {
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await DataContext.CloseWindowCommand.ExecuteAsync(null);
                    if (DataContext.UserWantsToClose)
                    {
                        Close();
                    }
                });
            });
        }

        base.OnClosing(e);
    }

    private void MainWindow_Initialized(object? sender, EventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            CrashHelper.SaveCrashInfo((Exception)e.ExceptionObject, DataContext.DocumentManagerSubViewModel.Documents);
        };
    }
}
