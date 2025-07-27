using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixiEditor.IdentityProvider;

namespace PixiEditor.ViewModels.User;

public class OwnedProductViewModel : ObservableObject
{
    public ProductData ProductData { get; }

    private bool isInstalled;

    public bool IsInstalled
    {
        get => isInstalled;
        set => SetProperty(ref isInstalled, value);
    }

    private bool isInstalling;

    public bool IsInstalling
    {
        get => isInstalling;
        set => SetProperty(ref isInstalling, value);
    }

    private bool updateAvailable;

    public bool UpdateAvailable
    {
        get => updateAvailable;
        set => SetProperty(ref updateAvailable, value);
    }

    private bool restartRequired;
    public bool RestartRequired
    {
        get => restartRequired;
        set => SetProperty(ref restartRequired, value);
    }

    public IAsyncRelayCommand InstallCommand { get; }

    public OwnedProductViewModel(ProductData productData, bool isInstalled, string? installedVersion,
        IAsyncRelayCommand<string> installContentCommand, Func<string, bool> isInstalledFunc)
    {
        ProductData = productData;
        IsInstalled = isInstalled;
        if (productData.LatestVersion != null && installedVersion != null)
        {
            UpdateAvailable = productData.LatestVersion != installedVersion;
        }
        else
        {
            UpdateAvailable = false;
        }

        InstallCommand = new AsyncRelayCommand(
            async () =>
            {
                IsInstalling = true;
                bool wasUpdating = UpdateAvailable;
                UpdateAvailable = false;
                RestartRequired = false;
                await installContentCommand.ExecuteAsync(ProductData.Id);
                IsInstalling = false;
                if (wasUpdating)
                {
                    RestartRequired = true;
                }
                else
                {
                    IsInstalled = isInstalledFunc(ProductData.Id);
                }
            }, () => !IsInstalled && !IsInstalling || UpdateAvailable);
    }
}
