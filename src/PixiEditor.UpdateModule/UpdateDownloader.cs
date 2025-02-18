﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace PixiEditor.UpdateModule;

public static class UpdateDownloader
{
    public static string DownloadLocation { get; } = Path.Join(Path.GetTempPath(), "PixiEditor");

    public static async Task DownloadReleaseZip(ReleaseInfo release)
    {
        Asset? matchingAsset = GetMatchingAsset(release);
        
        if(matchingAsset == null)
        {
            throw new FileNotFoundException("No matching update for your system found.");
        }

        using HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "PixiEditor");
        client.DefaultRequestHeaders.Add("Accept", "application/octet-stream");
        var response = await client.GetAsync(matchingAsset.Url);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            byte[] bytes = await response.Content.ReadAsByteArrayAsync();
            CreateTempDirectory();
            await File.WriteAllBytesAsync(Path.Join(DownloadLocation, $"update-{release.TagName}.zip"), bytes);
        }
    }

    public static async Task DownloadInstaller(ReleaseInfo info)
    {
        Asset? matchingAsset = GetMatchingAsset(info, "application/x-msdownload");

        if(matchingAsset == null)
        {
            throw new FileNotFoundException("No matching update for your system found.");
        }

        using HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "PixiEditor");
        client.DefaultRequestHeaders.Add("Accept", "application/octet-stream");
        var response = await client.GetAsync(matchingAsset.Url);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            byte[] bytes = await response.Content.ReadAsByteArrayAsync();
            CreateTempDirectory();
            await File.WriteAllBytesAsync(Path.Join(DownloadLocation, $"update-{info.TagName}.exe"), bytes);
        }
    }

    public static void CreateTempDirectory()
    {
        if (!Directory.Exists(DownloadLocation))
        {
            Directory.CreateDirectory(DownloadLocation);
        }
    }

    private static Asset? GetMatchingAsset(ReleaseInfo release, string assetType = "zip")
    {
        string arch = "x64";
        string os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsLinux() ? "linux" : "mac";
        return release.Assets.FirstOrDefault(x => x.ContentType.Contains(assetType)
                                         && x.Name.Contains(arch) && x.Name.Contains(os));
    }
}
