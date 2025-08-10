using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Xaml.Interactivity;
using PixiEditor.ChangeableDocument;
using PixiEditor.ChangeableDocument.Actions;
using PixiEditor.Integrations.Models;

using IAction = PixiEditor.ChangeableDocument.Actions.IAction;

namespace PixiEditor.Integrations.Blender;

public interface ILinkService
{
    string Id { get; }
    bool IsRunning { get; }
    Task StartAsync();
    Task StopAsync();
}
public interface IBlenderLinkService : ILinkService
{
    IngestMode Mode { get; set; }
    event Action<int> OnFrameImported;
}

internal class BlenderLinkService : IBlenderLinkService, IDisposable
{
    private readonly IFrameSource _source;
    private readonly Func<IngestMode, IFrameIngestor> _ingestorFor;
    private readonly DocumentChangeTracker _tracker;

    private readonly Channel<FrameDescriptor> _queue = 
        Channel.CreateUnbounded<FrameDescriptor>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = false
        });

    private CancellationTokenSource? _cts;
    private Task? _producerTask;
    private Task? _workerTask;

    public IngestMode Mode { get; set; } = IngestMode.Layer;

    public string Id => "blender";
    public bool IsRunning => _workerTask is { IsCompleted: false };
    public event Action<int>? OnFrameImported;


    public BlenderLinkService(IFrameSource source, 
                              DocumentChangeTracker tracker, 
                              Func<IngestMode, IFrameIngestor> ingestorFactory)
    {
        _source = source;
        _tracker = tracker;
        _ingestorFor = ingestorFactory;
    }


    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public Task StartAsync()
    {
        if(IsRunning) return Task.CompletedTask;
        _cts = new CancellationTokenSource();

        _producerTask = Task.Run(() => ProducerLoopAsync(_cts.Token));
        _workerTask = Task.Run(() => WorkerLoopAsync(_cts.Token));

        return Task.CompletedTask;
    }

    private async Task WorkerLoopAsync(CancellationToken ct)
    {
        try
        {
            while (await _queue.Reader.WaitToReadAsync(ct))
            {
                while (_queue.Reader.TryRead(out var frame))
                {
                    var packet = _ingestorFor(Mode).BuildPacket(_tracker.Document, frame);

                    await CommitWithRetryAsync(packet, ct);

                    OnFrameImported?.Invoke(frame.FrameIndex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in worker loop: {ex.Message}");
        }
    }

    private async Task CommitWithRetryAsync(IReadOnlyList<(ActionSource,IAction)> packet, CancellationToken ct)
    {
        const int maxRetries = 10;
        int retries = 0;

        const int backOffMsMin = 2;
        const int backOffMsMax = 25;
        int backOff = backOffMsMin;

        while(true)
        {
            ct.ThrowIfCancellationRequested();

            if (retries >= maxRetries)
            {
                Console.WriteLine("Max retries reached, giving up on committing actions.");
                return;
            }

            try
            {
                await _tracker.ProcessActions(packet.ToList());
                return;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Already currently processing"))
            {
                await Task.Delay(backOff, ct);
                if(backOff < backOffMsMax) backOff = Math.Min(backOff * 2, backOffMsMax);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error committing actions: {ex.Message}");
            }

            retries++;
        }
    }

    private async Task ProducerLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var frame in _source.GetFramesAsync(ct))
            {
                await WaitForStableFileAsync(frame.ColorPath);
                if(!string.IsNullOrEmpty(frame.NormalPath))
                    await WaitForStableFileAsync(frame.NormalPath);

                await _queue.Writer.WriteAsync(frame, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in producer loop: {ex.Message}");
        }
        finally
        {
            _queue.Writer.TryComplete();
        }
    }

    private async Task WaitForStableFileAsync(string colorPath)
    {
        long last = -1, same = 0;

        while(same < 3)
        {
            var len = new FileInfo(colorPath).Length;
            if (len == last)
            {
                same++;
            }
            else
            {
                same = 0;
                last = len;
            }
            await Task.Delay(40);
        }
    }

    public async Task StopAsync()
    {
        if(!IsRunning) return;
        _cts!?.Cancel();
        _queue.Writer.TryComplete();
        try
        {
            if (_producerTask is not null)
                await _producerTask;
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in producer task: {ex.Message}");
        }

        try
        {
            if (_workerTask is not null)
                await _workerTask;
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in worker task: {ex.Message}");
        }
        _cts?.Dispose();
        _cts = null;
    }
}
