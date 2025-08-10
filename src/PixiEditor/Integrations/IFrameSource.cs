using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Threading.Tasks;
using PixiEditor.Integrations.Models;

namespace PixiEditor.Integrations;
internal interface IFrameSource
{
     IAsyncEnumerable<FrameDescriptor> GetFramesAsync(CancellationToken ct);
}

internal class BlenderFileWatcherSource : IFrameSource , IDisposable
{
    private readonly FileSystemWatcher _fsColor;
    private readonly string _folder;
    private readonly Channel<FrameDescriptor> _ch = Channel.CreateUnbounded<FrameDescriptor>();
    private readonly Regex _rx = new(@"(?i)(?:color|combined)_(\d{4})\.png$", RegexOptions.Compiled);

    public BlenderFileWatcherSource(string folder)
    {
        _folder = folder;
        _fsColor = new FileSystemWatcher(folder, "*color_*.png") { EnableRaisingEvents = true};
        _fsColor.Created += _fsColor_Created;
        _fsColor.Renamed += _fsColor_Renamed;
    }

    private void _fsColor_Renamed(object? sender, RenamedEventArgs e)
    {
        throw new NotImplementedException();
    }

    private void _fsColor_Created(object? sender, FileSystemEventArgs e)
    {
        throw new NotImplementedException();
    }

    private void TryEnqueue(string colorPath)
    {
        var m = _rx.Match(Path.GetFileName(colorPath));
        if (!m.Success) return;

        int idx = int.Parse(m.Groups[1].Value);
        string? normal = Path.Combine(_folder, $"normal_{idx:0000}.png");
        if (!File.Exists(normal)) normal = null;

        _ch.Writer.TryWrite(new FrameDescriptor(colorPath, normal, idx, 0, 0));
    }

    public void Dispose()
    {
        _fsColor.Dispose();
    }

    public async IAsyncEnumerable<FrameDescriptor> GetFramesAsync([EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested &&
               await _ch.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
        {
            while(_ch.Reader.TryRead(out var frame))
            {
                yield return frame;
            }
        }
    }
}
