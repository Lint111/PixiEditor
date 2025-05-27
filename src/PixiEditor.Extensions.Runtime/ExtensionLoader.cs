﻿using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using PixiEditor.Extensions.Metadata;
using PixiEditor.Extensions.WasmRuntime;
using PixiEditor.Platform;

namespace PixiEditor.Extensions.Runtime;

public class ExtensionLoader
{
    private readonly Dictionary<string, OfficialExtensionData> _officialExtensionsKeys =
        new Dictionary<string, OfficialExtensionData>();

    public List<Extension> LoadedExtensions { get; } = new();

    public string[] PackagesPath { get; }
    public string UnpackedExtensionsPath { get; }

    private WasmRuntime.WasmRuntime _wasmRuntime = new WasmRuntime.WasmRuntime();

    public ExtensionLoader(string[] packagesPaths, string unpackedExtensionsPath)
    {
        PackagesPath = packagesPaths;
        UnpackedExtensionsPath = unpackedExtensionsPath;
        ValidateExtensionFolder();
    }

    public void AddOfficialExtension(string uniqueName, OfficialExtensionData data)
    {
        _officialExtensionsKeys.Add(uniqueName, data);
    }

    public void LoadExtensions()
    {
        foreach (var packagesPath in PackagesPath)
        {
            LoadExtensionsFromPath(packagesPath);
        }
    }

    private void LoadExtensionsFromPath(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(path))
        {
            if (file.EndsWith(".pixiext"))
            {
                LoadExtension(file);
            }
        }
    }

    // Uncomment when PixiEditor.Core extension concept is implemented
    /*private void LoadCore()
    {
        Type entry = typeof(PixiEditorCoreExtension);
        Assembly assembly = entry.Assembly;
        var serializer = new JsonSerializer();

        Uri uri = new Uri("avares://PixiEditor.Core/extension.json");

        if (!AssetLoader.Exists(uri))
        {
            throw new FileNotFoundException("Core metadata not found", uri.ToString());
        }

        using var sr = new StreamReader(AssetLoader.Open(uri));
        using var jsonTextReader = new JsonTextReader(sr);
        ExtensionMetadata? metadata = serializer.Deserialize<ExtensionMetadata>(jsonTextReader);
        LoadExtensionFrom(assembly, entry, metadata);
    }*/

    public void InitializeExtensions(ExtensionServices apiServices)
    {
        try
        {
            foreach (var extension in LoadedExtensions)
            {
                extension.Initialize(apiServices);
            }
        }
        catch (Exception ex)
        {
#if DEBUG
            throw;
#endif
            // TODO: Log exception
            // Maybe it's not a good idea to send webhook exceptions in the extension loader
            //CrashHelper.SendExceptionInfoToWebhook(ex);
        }
    }

    public Extension? LoadExtension(string extension)
    {
        var extZip = ZipFile.OpenRead(extension);
        try
        {
            ExtensionMetadata metadata = ExtractMetadata(extZip);
            if (IsDifferentThanCached(metadata, extension))
            {
                UnpackExtension(extZip, metadata);
            }

            string extensionJson = Path.Combine(UnpackedExtensionsPath, metadata.UniqueName, "extension.json");
            if (!File.Exists(extensionJson))
            {
                return null;
            }

            return LoadExtensionFromCache(extensionJson);
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    public void UnpackExtension(ZipArchive extZip, ExtensionMetadata metadata)
    {
        string extensionPath = Path.Combine(UnpackedExtensionsPath, metadata.UniqueName);
        if (Directory.Exists(extensionPath))
        {
            Directory.Delete(extensionPath, true);
        }

        extZip.ExtractToDirectory(extensionPath);
    }

    private ExtensionMetadata ExtractMetadata(ZipArchive extZip)
    {
        var metadataEntry = extZip.GetEntry("extension.json");
        if (metadataEntry == null)
        {
            throw new FileNotFoundException("Extension metadata not found");
        }

        using var stream = metadataEntry.Open();
        using var sr = new StreamReader(stream);
        using var jsonTextReader = new JsonTextReader(sr);
        var serializer = new JsonSerializer();
        return serializer.Deserialize<ExtensionMetadata>(jsonTextReader);
    }

    private bool IsDifferentThanCached(ExtensionMetadata metadata, string extension)
    {
        string extensionJson = Path.Combine(UnpackedExtensionsPath, metadata.UniqueName, "extension.json");
        if (!File.Exists(extensionJson))
        {
            return true;
        }

        string json = File.ReadAllText(extensionJson);
        ExtensionMetadata? cachedMetadata = JsonConvert.DeserializeObject<ExtensionMetadata>(json);

        if (cachedMetadata is null)
        {
            return true;
        }

        if (metadata.UniqueName != cachedMetadata.UniqueName)
        {
            return true;
        }

        bool isDifferent = metadata.Version != cachedMetadata.Version;

        if (isDifferent)
        {
            return true;
        }

        return PackageWriteTimeIsBigger(Path.Combine(UnpackedExtensionsPath, metadata.UniqueName), extension);
    }

    private bool PackageWriteTimeIsBigger(string unpackedDirectory, string extension)
    {
        DateTime extensionWriteTime = File.GetLastWriteTime(extension);
        DateTime unpackedWriteTime = Directory.GetLastWriteTime(unpackedDirectory);
        return extensionWriteTime > unpackedWriteTime;
    }

    private Extension LoadExtensionFromCache(string extension)
    {
        string json = File.ReadAllText(extension);
        try
        {
            var metadata = JsonConvert.DeserializeObject<ExtensionMetadata>(json);
            string directory = Path.GetDirectoryName(extension);
            ExtensionEntry? entry = GetEntry(directory);
            if (entry is null)
            {
                throw new NoEntryException(directory);
            }

            if (!ValidateMetadata(metadata, entry))
            {
                return null;
            }

            return LoadExtensionFrom(entry, metadata);
        }
        catch (JsonException)
        {
#if DEBUG
            throw;
#endif
            //MessageBox.Show(new LocalizedString("ERROR_INVALID_PACKAGE", packageJsonPath), "ERROR");
        }
        catch (ExtensionException ex)
        {
#if DEBUG
            throw;
#endif
            //MessageBox.Show(ex.DisplayMessage, "ERROR");
        }
        catch (Exception ex)
        {
#if DEBUG
            throw;
#endif
            //MessageBox.Show(new LocalizedString("ERROR_LOADING_PACKAGE", packageJsonPath), "ERROR");
            //CrashHelper.SendExceptionInfoToWebhook(ex);
        }

        return null;
    }

    private Extension LoadExtensionFrom(ExtensionEntry entry, ExtensionMetadata metadata)
    {
        var extension = LoadExtensionEntry(entry, metadata);
        extension.Load();
        LoadedExtensions.Add(extension);
        return extension;
    }

    private ExtensionEntry? GetEntry(string assemblyFolder)
    {
        string[] dlls = Directory.GetFiles(assemblyFolder, "*.dll");
        Assembly? entryAssembly = GetEntryAssembly(dlls, out Type extensionType);

        if (entryAssembly != null)
        {
            return new DllExtensionEntry(entryAssembly, extensionType);
        }

        string[] wasm = Directory.GetFiles(assemblyFolder, "*.wasm");
        WasmExtensionInstance? entryWasm = GetEntryWasm(wasm);

        if (entryWasm != null)
        {
            return new WasmExtensionEntry(entryWasm);
        }

        return null;
    }

    private bool ValidateMetadata(ExtensionMetadata metadata, ExtensionEntry assembly)
    {
        if (string.IsNullOrEmpty(metadata.UniqueName))
        {
            throw new MissingMetadataException("Description");
        }

        string fixedUniqueName = metadata.UniqueName.ToLower().Trim();

        if (fixedUniqueName.StartsWith("pixieditor".Trim(), StringComparison.OrdinalIgnoreCase))
        {
            if (!IsOfficialAssemblyLegit(fixedUniqueName, assembly))
            {
                throw new ForbiddenUniqueNameExtension();
            }

            if (!IsAdditionalContentInstalled(fixedUniqueName))
            {
                return false;
            }
        }
        // TODO: Validate if unique name is unique

        if (string.IsNullOrEmpty(metadata.DisplayName))
        {
            throw new MissingMetadataException("DisplayName");
        }

        if (string.IsNullOrEmpty(metadata.Version))
        {
            throw new MissingMetadataException("Version");
        }

        return true;
    }

    private bool IsAdditionalContentInstalled(string fixedUniqueName)
    {
        if (!_officialExtensionsKeys.ContainsKey(fixedUniqueName)) return false;
        AdditionalContentProduct? product = _officialExtensionsKeys[fixedUniqueName].Product;

        if (product == null) return true;

        return IPlatform.Current.AdditionalContentProvider?.IsContentInstalled(product.Value) ?? false;
    }

    private bool IsOfficialAssemblyLegit(string metadataUniqueName, ExtensionEntry entry)
    {
        if (entry == null) return false; // All official extensions must have a valid assembly
        if (!_officialExtensionsKeys.ContainsKey(metadataUniqueName)) return false;

        if (entry is DllExtensionEntry dllExtensionEntry)
        {
            return VerifyAssemblySignature(metadataUniqueName, dllExtensionEntry.Assembly);
        }

        if (entry is WasmExtensionEntry wasmExtensionEntry)
        {
            return true;
            //TODO: Verify wasm signature somehow
            //return VerifyAssemblySignature(metadataUniqueName, wasmExtensionEntry.Instance);
        }

        return false;
    }

    private bool VerifyAssemblySignature(string metadataUniqueName, Assembly assembly)
    {
        bool wasVerified = false;
        bool verified = StrongNameSignatureVerificationEx(assembly.Location, true, ref wasVerified);
        if (!verified || !wasVerified) return false;

        byte[]? assemblyPublicKey = assembly.GetName().GetPublicKey();
        if (assemblyPublicKey == null) return false;

        return PublicKeysMatch(assemblyPublicKey, _officialExtensionsKeys[metadataUniqueName].PublicKeyName);
    }

    private bool PublicKeysMatch(byte[] assemblyPublicKey, string pathToPublicKey)
    {
        Assembly currentAssembly = Assembly.GetExecutingAssembly();
        using Stream? stream =
            currentAssembly.GetManifestResourceStream(
                $"{currentAssembly.GetName().Name}.OfficialExtensions.{pathToPublicKey}");
        if (stream == null) return false;

        using MemoryStream memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        byte[] publicKey = memoryStream.ToArray();

        return assemblyPublicKey.SequenceEqual(publicKey);
    }

    //TODO: uhh, other platforms dumbass?
    [DllImport("mscoree.dll", CharSet = CharSet.Unicode)]
    static extern bool StrongNameSignatureVerificationEx(string wszFilePath, bool fForceVerification,
        ref bool pfWasVerified);

    private Extension LoadExtensionEntry(ExtensionEntry entry, ExtensionMetadata metadata)
    {
        Extension extension = entry.CreateExtension();
        extension.ProvideMetadata(metadata);
        return extension;
    }

    private Assembly? GetEntryAssembly(string[] dlls, out Type extensionType)
    {
        foreach (var dll in dlls)
        {
            try
            {
                var assembly = Assembly.LoadFrom(dll);
                extensionType = assembly.GetExportedTypes().FirstOrDefault(x => x.IsSubclassOf(typeof(Extension)));
                if (extensionType is not null)
                {
                    return assembly;
                }
            }
            catch
            {
                // ignored
            }
        }

        extensionType = null;
        return null;
    }

    private WasmExtensionInstance? GetEntryWasm(string[] wasmFiles)
    {
        foreach (var wasm in wasmFiles)
        {
            try
            {
                WasmExtensionInstance instance = _wasmRuntime.LoadModule(wasm);
                return instance;
            }
            catch (Exception ex)
            {
#if DEBUG
                throw;
#endif
            }
        }

        return null;
    }

    private void ValidateExtensionFolder()
    {
        try
        {
            if (!Directory.Exists(UnpackedExtensionsPath))
            {
                Directory.CreateDirectory(UnpackedExtensionsPath);
            }
        }
        catch (Exception ex)
        {
#if DEBUG
            throw;
#endif
        }
    }

    public string? GetTypeId(Type id)
    {
        if (id.Assembly == Assembly.GetCallingAssembly())
        {
            return $"PixiEditor.{id.Name}";
        }

        foreach (var extension in LoadedExtensions)
        {
            Type? foundType = extension.Assembly.GetTypes().FirstOrDefault(x => x == id);
            if (foundType != null)
            {
                return $"{extension.Metadata.UniqueName}:{foundType.Name}";
            }
        }

        return null;
    }
}

public struct OfficialExtensionData
{
    public string PublicKeyName { get; }
    public AdditionalContentProduct? Product { get; }
    public string? PurchaseLink { get; }

    public OfficialExtensionData(string publicKeyName, AdditionalContentProduct product, string? purchaseLink = null)
    {
        PublicKeyName = publicKeyName;
        Product = product;
    }
}
