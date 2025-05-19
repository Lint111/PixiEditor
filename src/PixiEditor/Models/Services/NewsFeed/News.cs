﻿using System.ComponentModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using PixiEditor.Extensions.Helpers;
using PixiEditor.Helpers.Converters.JsonConverters;
using PixiEditor.UI.Common.Fonts;
using PixiEditor.UI.Common.Localization;

namespace PixiEditor.Models.Services.NewsFeed;

[JsonConverter(typeof(DefaultUnknownEnumConverter), (int)Misc)]
internal enum NewsType
{
    [Description(PixiPerfectIcons.Download)]
    NewVersion,
    [Description(PixiPerfectIcons.YouTube)]
    YtVideo,
    [Description(PixiPerfectIcons.Write)]
    BlogPost,
    [Description(PixiPerfectIcons.Message)]
    OfficialAnnouncement,
    [Description(PixiPerfectIcons.Misc)]
    Misc
}

internal record News
{
    public string Title { get; init; } = string.Empty;
    public NewsType NewsType { get; init; } = NewsType.Misc;
    public string Url { get; init; }
    public DateTime Date { get; init; }
    public string CoverImageUrl { get; init; } = string.Empty;

    [JsonIgnore]
    public string ResolvedIcon => NewsType.GetDescription();

    [JsonIgnore]
    public bool IsNew { get; set; } = false;

    public int GetIdentifyingNumber()
    {
        MD5 md5Hasher = MD5.Create();
        string data = Title + Url + Date.ToString(CultureInfo.InvariantCulture) + CoverImageUrl;
        var hashed = md5Hasher.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToInt32(hashed, 0);
    }
}
