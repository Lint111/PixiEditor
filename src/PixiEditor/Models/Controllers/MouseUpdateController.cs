﻿using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace PixiEditor.Models.Controllers;

#nullable enable
public class MouseUpdateController : IDisposable
{
    private const double MouseUpdateIntervalMs = 1000 / 142.0; //142 Hz

    private Thread timerThread;
    private readonly AutoResetEvent resetEvent = new(false);
    private readonly object lockObj = new();
    private bool isAborted = false;

    private readonly FrameworkElement element;
    private readonly MouseEventHandler mouseMoveHandler;
    
    public MouseUpdateController(FrameworkElement uiElement, MouseEventHandler onMouseMove)
    {
        mouseMoveHandler = onMouseMove;
        element = uiElement;
        element.MouseMove += OnMouseMove;

        bool wasThreadCreated = !element.IsLoaded;
        element.Loaded += (_, _) =>
        {
            wasThreadCreated = true;
            CreateTimerThread();
        };

        if (!wasThreadCreated)
            CreateTimerThread();

        element.Unloaded += (_, _) =>
        {
            isAborted = true;
        };
    }

    private void CreateTimerThread()
    {
        timerThread = new Thread(TimerThread);
        timerThread.Name = "MouseUpdateController thread";
        timerThread.Start();
        isAborted = false;
    }

    private bool IsThreadShouldStop()
    {
        return isAborted || timerThread != Thread.CurrentThread || Application.Current is null;
    }
    
    private void TimerThread()
    {
        try
        {
            long lastThreadIter = Stopwatch.GetTimestamp();
            
            // abort if a new thread was created
            while (!IsThreadShouldStop())
            {
                // call waitOne periodically instead of waiting infinitely to make sure we crash or exit when resetEvent is disposed
                if (!resetEvent.WaitOne(300))
                {
                    lastThreadIter = Stopwatch.GetTimestamp();
                    continue;
                }

                lock (lockObj)
                {
                    double sleepDur = Math.Clamp(MouseUpdateIntervalMs - Stopwatch.GetElapsedTime(lastThreadIter).TotalMilliseconds, 0, MouseUpdateIntervalMs);
                    lastThreadIter += (long)(MouseUpdateIntervalMs * Stopwatch.Frequency / 1000);
                    if (sleepDur > 0)
                        Thread.Sleep((int)Math.Round(sleepDur));
                    
                    if (IsThreadShouldStop())
                        return;
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        element.MouseMove += OnMouseMove;
                    });
                    
                }
            }
        }
        catch (ObjectDisposedException)
        {
            return;
        }
        catch (Exception e)
        {
            Application.Current?.Dispatcher.BeginInvoke(() => throw new AggregateException("Input handling thread died", e), DispatcherPriority.SystemIdle);
            throw;
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        bool lockWasTaken = false;
        try
        {
            Monitor.TryEnter(lockObj, ref lockWasTaken);
            if (lockWasTaken)
            {
                resetEvent.Set();
                element.MouseMove -= OnMouseMove;
                mouseMoveHandler(sender, e);
            }
        }
        finally
        {
            if (lockWasTaken)
                Monitor.Exit(lockObj);
        }
    }

    public void Dispose()
    {
        element.MouseMove -= OnMouseMove;
        isAborted = true;
        resetEvent.Dispose();
    }
}
