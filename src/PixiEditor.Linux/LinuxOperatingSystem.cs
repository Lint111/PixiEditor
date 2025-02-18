﻿using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using PixiEditor.OperatingSystem;

namespace PixiEditor.Linux;

public sealed class LinuxOperatingSystem : IOperatingSystem
{
    public string Name { get; } = "Linux";
    public string AnalyticsId => "Linux";
    public string AnalyticsName => LinuxOSInformation.FromReleaseFile().ToString();
    public IInputKeys InputKeys { get; } = new LinuxInputKeys();
    public IProcessUtility ProcessUtility { get; } = new LinuxProcessUtility();

    public string ExecutableExtension { get; } = string.Empty;

    public void OpenUri(string uri)
    {
        ProcessUtility.Execute($"xdg-open", uri);
    }

    public void OpenFolder(string path)
    {
        try
        {
            ProcessUtility.Execute($"dbus-send", $"--session --dest=org.freedesktop.FileManager1 --type=method_call /org/freedesktop/FileManager1 org.freedesktop.FileManager1.ShowItems array:string:\"file://{path}\" string:\"\"");
        }
        catch (Exception e)
        {
            ProcessUtility.Execute($"xdg-open", Path.GetDirectoryName(path));
        }
    }

    public bool HandleNewInstance(Dispatcher? dispatcher, Action<string, bool> openInExistingAction, IApplicationLifetime lifetime)
    {
        return true;
    }

    public void HandleActivatedWithFile(FileActivatedEventArgs fileActivatedEventArgs)
    {
        // TODO: Check if this is executed on Linux at all
    }

    public void HandleActivatedWithUri(ProtocolActivatedEventArgs openUriEventArgs)
    {
        // TODO: Check if this is executed on Linux at all
    }

    class LinuxOSInformation
    {
        const string FilePath = "/etc/os-release";
        
        private LinuxOSInformation(string? name, string? version, bool available)
        {
            Name = name;
            Version = version;
            Available = available;
        }

        public static LinuxOSInformation FromReleaseFile()
        {
            if (!File.Exists(FilePath))
            {
                return new LinuxOSInformation(null, null, false);
            }
            
            // Parse /etc/os-release file (e.g. 'NAME="Ubuntu"')
            var lines = File.ReadAllLines(FilePath).Select<string, (string Key, string Value)>(x =>
            {
                var separatorIndex = x.IndexOf('=');
                return (x[..separatorIndex], x[(separatorIndex + 1)..]);
            }).ToList();
            
            var name = lines.FirstOrDefault(x => x.Key == "NAME").Value.Trim('"');
            var version = lines.FirstOrDefault(x => x.Key == "VERSION").Value.Trim('"');
            
            return new LinuxOSInformation(name, version, true);
        }
        
        public bool Available { get; }
        
        public string? Name { get; private set; }
        
        public string? Version { get; private set; }

        public override string ToString() => $"{Name} {Version}";
    }

    public string GetActiveDesktopEnvironment()
    {
        var desktopSession = Environment.GetEnvironmentVariable("DESKTOP_SESSION");
        if (desktopSession != null)
        {
            return desktopSession;
        }

        var desktopSessionFile = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
        if (desktopSessionFile != null)
        {
            return desktopSessionFile;
        }

        return "Unknown";
    }
}
