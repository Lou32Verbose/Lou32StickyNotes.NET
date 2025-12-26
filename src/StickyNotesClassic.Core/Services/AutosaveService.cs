using Microsoft.Extensions.Logging;
using StickyNotesClassic.Core.Models;
using StickyNotesClassic.Core.Repositories;
using System.Collections.Concurrent;

namespace StickyNotesClassic.Core.Services;

/// <summary>
/// Autosave service with debounced content saves and throttled bounds saves.
/// Implements a background queue to ensure sequential writes.
/// </summary>
public class AutosaveService : IDisposable
{
    private const int ContentDebounceMs = 250;
    private const int BoundsThrottleMs = 200;

    private readonly INotesRepository _repository;
    private readonly ILogger<AutosaveService> _logger;
    private readonly ConcurrentDictionary<string, Timer> _contentTimers = new();
    private readonly ConcurrentDictionary<string, Timer> _boundsTimers = new();
    private readonly ConcurrentQueue<Func<Task>> _saveQueue = new();
    private readonly SemaphoreSlim _queueSemaphore = new(0);
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Task _processingTask;

    public AutosaveService(INotesRepository repository, ILogger<AutosaveService> logger)
    {
        _repository = repository;
        _logger = logger;
        _processingTask = Task.Run(ProcessSaveQueueAsync);
        _logger.LogInformation("AutosaveService initialized");
    }

    /// <summary>
    /// Enqueues a content change with 250ms debounce.
    /// </summary>
    public void EnqueueContentChanged(string noteId, string contentRtf, string contentText)
    {
        // Cancel existing content timer if any
        if (_contentTimers.TryRemove(noteId, out var existingTimer))
        {
            existingTimer.Dispose();
        }

        // Create new debounced timer
        var timer = new Timer(_ =>
        {
            EnqueueSave(async () =>
            {
                var note = await _repository.GetNoteByIdAsync(noteId);
                if (note != null)
                {
                    note.ContentRtf = contentRtf;
                    note.ContentText = contentText;
                    await _repository.UpsertNoteAsync(note);
                }
            });

            // Clean up timer
            if (_contentTimers.TryRemove(noteId, out var t))
            {
                t.Dispose();
            }
        }, null, ContentDebounceMs, Timeout.Infinite);

        _contentTimers[noteId] = timer;
    }

    /// <summary>
    /// Enqueues a bounds change with 200ms throttle.
    /// </summary>
    public void EnqueueBoundsChanged(string noteId, double x, double y, double width, double height)
    {
        // Cancel existing bounds timer if any
        if (_boundsTimers.TryRemove(noteId, out var existingTimer))
        {
            existingTimer.Dispose();
        }

        // Create new throttled timer
        var timer = new Timer(_ =>
        {
            EnqueueSave(async () =>
            {
                var note = await _repository.GetNoteByIdAsync(noteId);
                if (note != null)
                {
                    note.X = x;
                    note.Y = y;
                    note.Width = width;
                    note.Height = height;
                    await _repository.UpsertNoteAsync(note);
                }
            });

            // Clean up timer
            if (_boundsTimers.TryRemove(noteId, out var t))
            {
                t.Dispose();
            }
        }, null, BoundsThrottleMs, Timeout.Infinite);

        _boundsTimers[noteId] = timer;
    }

    /// <summary>
    /// Enqueues an immediate save (for color/topmost changes).
    /// </summary>
    public void EnqueueImmediateSave(Note note)
    {
        EnqueueSave(async () => await _repository.UpsertNoteAsync(note));
    }

    /// <summary>
    /// Flushes all pending saves synchronously (called on app shutdown).
    /// </summary>
    public async Task FlushAllAsync()
    {
        // Trigger all pending timers immediately
        foreach (var timer in _contentTimers.Values)
        {
            timer.Change(0, Timeout.Infinite);
        }
        foreach (var timer in _boundsTimers.Values)
        {
            timer.Change(0, Timeout.Infinite);
        }

        // Give timers a moment to enqueue
        await Task.Delay(100);

        // Process remaining queue items
        while (_saveQueue.TryDequeue(out var saveAction))
        {
            await saveAction();
        }
    }

    private void EnqueueSave(Func<Task> saveAction)
    {
        _saveQueue.Enqueue(saveAction);
        _queueSemaphore.Release();
    }

    private async Task ProcessSaveQueueAsync()
    {
        try
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                await _queueSemaphore.WaitAsync(_cancellationTokenSource.Token);

                if (_saveQueue.TryDequeue(out var saveAction))
                {
                    try
                    {
                        await saveAction();
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue processing queue
                        _logger.LogError(ex, "Autosave operation failed");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }

    public void Dispose()
    {
        // Cancel background processing
        _cancellationTokenSource.Cancel();

        // Dispose all timers
        foreach (var timer in _contentTimers.Values)
        {
            timer.Dispose();
        }
        foreach (var timer in _boundsTimers.Values)
        {
            timer.Dispose();
        }

        _contentTimers.Clear();
        _boundsTimers.Clear();

        _queueSemaphore.Dispose();
        _cancellationTokenSource.Dispose();

        // Wait for processing task to complete
        try
        {
            _processingTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Ignore timeout
        }
    }
}
