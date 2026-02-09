using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.AI.Core.Streams;

/// <summary>
/// A thread-safe stream that supports concurrent writing from a producer and reading from a consumer.
/// This stream is seekable within the buffered data for format detection purposes, and releases
/// consumed data to minimize memory usage.
/// </summary>
public sealed class ProducerConsumerStream : Stream
{
    private readonly ConcurrentQueue<byte[]> _chunks = new();
    private readonly SemaphoreSlim _dataAvailable = new(0);
    private readonly SemaphoreSlim _headerReady = new(0);
    private readonly object _lock = new();
    private readonly ILogger _logger;

    private byte[] _currentChunk;
    private int _currentChunkOffset;
    private long _position;
    private long _length;
    private bool _isCompleted;
    private bool _disposed;

    // Buffer for seeking - keeps the first N bytes for format detection
    private readonly MemoryStream _headerBuffer;
    private readonly int _headerBufferSize;
    private bool _headerBufferComplete;

    /// <summary>
    /// Creates a new ProducerConsumerStream.
    /// </summary>
    /// <param name="headerBufferSize">The number of bytes to buffer at the start for seeking/format detection. Default is 12 bytes.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public ProducerConsumerStream(int headerBufferSize = 12, ILogger logger = null)
    {
        _headerBufferSize = headerBufferSize;
        _headerBuffer = new MemoryStream(headerBufferSize);
        _logger = logger ?? NullLogger.Instance;
        _logger.LogDebug("[ProducerConsumerStream] Created with headerBufferSize={HeaderBufferSize}", headerBufferSize);
    }

    public override bool CanRead => true;
    public override bool CanSeek => true; // We support seeking within the header buffer
    public override bool CanWrite => true;

    public override long Length
    {
        get
        {
            // Wait for header to be ready before returning length
            // This ensures format detection can proceed
            _logger.LogDebug("[ProducerConsumerStream] Length getter called, waiting for header...");
            WaitForHeader();
            _logger.LogDebug("[ProducerConsumerStream] Length={Length}", _length);
            return _length;
        }
    }

    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    /// <summary>
    /// Waits until the header buffer is filled or the stream is completed.
    /// </summary>
    private void WaitForHeader()
    {
        if (_headerBufferComplete || _isCompleted)
        {
            _logger.LogDebug("[ProducerConsumerStream] WaitForHeader: already complete (headerComplete={HeaderComplete}, isCompleted={IsCompleted})", _headerBufferComplete, _isCompleted);
            return;
        }

        _logger.LogDebug("[ProducerConsumerStream] WaitForHeader: waiting for header data (timeout=30s)...");

        try
        {
            // Use a timeout to help diagnose blocking issues
            if (!_headerReady.Wait(TimeSpan.FromSeconds(30)))
            {
                _logger.LogError("[ProducerConsumerStream] WaitForHeader: TIMEOUT after 30 seconds");
                throw new TimeoutException("Timed out waiting for audio header data. No audio data was received within 30 seconds.");
            }
            _logger.LogDebug("[ProducerConsumerStream] WaitForHeader: header is ready!");
            // Re-release so other waiters can also proceed
            _headerReady.Release();
        }
        catch (ObjectDisposedException)
        {
            _logger.LogDebug("[ProducerConsumerStream] WaitForHeader: stream was disposed while waiting");
            // Stream was disposed while waiting
        }
    }

    /// <summary>
    /// Waits until the header buffer is filled or the stream is completed (async version).
    /// </summary>
    private async Task WaitForHeaderAsync(CancellationToken cancellationToken = default)
    {
        if (_headerBufferComplete || _isCompleted)
        {
            _logger.LogDebug("[ProducerConsumerStream] WaitForHeaderAsync: already complete (headerComplete={HeaderComplete}, isCompleted={IsCompleted})", _headerBufferComplete, _isCompleted);
            return;
        }

        _logger.LogDebug("[ProducerConsumerStream] WaitForHeaderAsync: waiting for header data (timeout=30s)...");

        try
        {
            // Use a timeout combined with cancellation
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await _headerReady.WaitAsync(linkedCts.Token);
            _logger.LogDebug("[ProducerConsumerStream] WaitForHeaderAsync: header is ready!");
            // Re-release so other waiters can also proceed
            _headerReady.Release();
        }
        catch (ObjectDisposedException)
        {
            _logger.LogDebug("[ProducerConsumerStream] WaitForHeaderAsync: stream was disposed while waiting");
            // Stream was disposed while waiting
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("[ProducerConsumerStream] WaitForHeaderAsync: external cancellation requested");
            // External cancellation requested - rethrow
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("[ProducerConsumerStream] WaitForHeaderAsync: TIMEOUT after 30 seconds");
            // Timeout occurred
            throw new TimeoutException("Timed out waiting for audio header data. No audio data was received within 30 seconds.");
        }
    }

    /// <summary>
    /// Writes data to the stream. This method is thread-safe and can be called from a producer thread.
    /// </summary>
    public override void Write(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_isCompleted)
        {
            throw new InvalidOperationException("Cannot write to a completed stream.");
        }

        if (count == 0)
        {
            _logger.LogDebug("[ProducerConsumerStream] Write: count=0, skipping");
            return;
        }

        _logger.LogDebug("[ProducerConsumerStream] Write: writing {Count} bytes (headerComplete={HeaderComplete}, currentLength={Length})", count, _headerBufferComplete, _length);

        // Copy the data to avoid issues with buffer reuse
        var chunk = new byte[count];
        Buffer.BlockCopy(buffer, offset, chunk, 0, count);

        var headerJustCompleted = false;
        var bytesForChunkQueue = count;
        var chunkQueueOffset = 0;

        lock (_lock)
        {
            // Write to header buffer if not complete
            if (!_headerBufferComplete)
            {
                var bytesToHeader = Math.Min(count, _headerBufferSize - (int)_headerBuffer.Length);
                if (bytesToHeader > 0)
                {
                    _headerBuffer.Write(chunk, 0, bytesToHeader);
                    _logger.LogDebug("[ProducerConsumerStream] Write: wrote {BytesToHeader} bytes to header buffer (total header bytes={HeaderLength})", bytesToHeader, _headerBuffer.Length);

                    // Only queue the portion that doesn't go into the header buffer
                    chunkQueueOffset = bytesToHeader;
                    bytesForChunkQueue = count - bytesToHeader;

                    if (_headerBuffer.Length >= _headerBufferSize)
                    {
                        _headerBufferComplete = true;
                        headerJustCompleted = true;
                        _logger.LogDebug("[ProducerConsumerStream] Write: header buffer is now complete!");
                    }
                }
            }

            // Only enqueue data that's beyond the header buffer
            if (bytesForChunkQueue > 0)
            {
                if (chunkQueueOffset > 0)
                {
                    // Create a new array with only the non-header portion
                    var remainingChunk = new byte[bytesForChunkQueue];
                    Buffer.BlockCopy(chunk, chunkQueueOffset, remainingChunk, 0, bytesForChunkQueue);
                    _chunks.Enqueue(remainingChunk);
                }
                else
                {
                    _chunks.Enqueue(chunk);
                }
                _logger.LogDebug("[ProducerConsumerStream] Write: enqueued {BytesForChunkQueue} bytes to chunk queue (queue count={QueueCount})", bytesForChunkQueue, _chunks.Count);
            }

            _length += count;
        }

        // Signal that header is ready (outside lock to avoid potential deadlock)
        if (headerJustCompleted)
        {
            _logger.LogDebug("[ProducerConsumerStream] Write: releasing _headerReady semaphore");
            _headerReady.Release();
        }

        if (bytesForChunkQueue > 0)
        {
            _dataAvailable.Release();
        }
    }

    /// <summary>
    /// Writes data to the stream asynchronously.
    /// </summary>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Write(buffer, offset, count);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes data to the stream asynchronously.
    /// </summary>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Writes data to the stream.
    /// </summary>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            return;
        }

        Write(buffer.ToArray(), 0, buffer.Length);
    }

    /// <summary>
    /// Signals that no more data will be written to the stream.
    /// </summary>
    public void Complete()
    {
        _logger.LogDebug("[ProducerConsumerStream] Complete: marking stream as completed (length={Length})", _length);

        lock (_lock)
        {
            _isCompleted = true;
            _headerBufferComplete = true;
        }

        // Release semaphores to unblock any waiting operations
        try { _headerReady.Release(); } catch (SemaphoreFullException) { }
        try { _dataAvailable.Release(); } catch (SemaphoreFullException) { }

        _logger.LogDebug("[ProducerConsumerStream] Complete: done");
    }

    /// <summary>
    /// Signals that no more data will be written to the stream (async version for compatibility).
    /// </summary>
    public Task CompleteAsync()
    {
        Complete();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads data from the stream. This method blocks until data is available or the stream is completed.
    /// </summary>
    public override int Read(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (count == 0)
        {
            return 0;
        }

        _logger.LogDebug("[ProducerConsumerStream] Read: requesting {Count} bytes at position {Position}", count, _position);

        // Wait for header to be ready before reading
        WaitForHeader();

        // If we're reading from within the header buffer range, read from there
        if (_position < _headerBuffer.Length)
        {
            lock (_lock)
            {
                _headerBuffer.Position = _position;
                var bytesFromHeader = _headerBuffer.Read(buffer, offset, count);
                _position += bytesFromHeader;
                _logger.LogDebug("[ProducerConsumerStream] Read: read {BytesFromHeader} bytes from header buffer, new position={Position}", bytesFromHeader, _position);
                return bytesFromHeader;
            }
        }

        var totalRead = 0;

        while (totalRead < count)
        {
            // If we have a current chunk with remaining data, read from it
            if (_currentChunk != null && _currentChunkOffset < _currentChunk.Length)
            {
                var bytesToCopy = Math.Min(count - totalRead, _currentChunk.Length - _currentChunkOffset);
                Buffer.BlockCopy(_currentChunk, _currentChunkOffset, buffer, offset + totalRead, bytesToCopy);
                _currentChunkOffset += bytesToCopy;
                totalRead += bytesToCopy;
                _position += bytesToCopy;

                // If we've consumed the entire chunk, release it
                if (_currentChunkOffset >= _currentChunk.Length)
                {
                    _currentChunk = null;
                    _currentChunkOffset = 0;
                }

                continue;
            }

            // Try to get the next chunk
            if (_chunks.TryDequeue(out var nextChunk))
            {
                _currentChunk = nextChunk;
                _currentChunkOffset = 0;
                continue;
            }

            // No chunk available, check if we're done
            lock (_lock)
            {
                if (_isCompleted && _chunks.IsEmpty)
                {
                    _logger.LogDebug("[ProducerConsumerStream] Read: stream completed, returning {TotalRead} bytes", totalRead);
                    break;
                }
            }

            // Wait for more data
            try
            {
                _dataAvailable.Wait();
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }

        _logger.LogDebug("[ProducerConsumerStream] Read: returning {TotalRead} bytes, new position={Position}", totalRead, _position);
        return totalRead;
    }

    /// <summary>
    /// Reads data from the stream asynchronously.
    /// </summary>
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (count == 0)
        {
            return 0;
        }

        _logger.LogDebug("[ProducerConsumerStream] ReadAsync: requesting {Count} bytes at position {Position}", count, _position);

        // Wait for header to be ready before reading
        await WaitForHeaderAsync(cancellationToken);

        // If we're reading from within the header buffer range, read from there
        if (_position < _headerBuffer.Length)
        {
            lock (_lock)
            {
                _headerBuffer.Position = _position;
                var bytesFromHeader = _headerBuffer.Read(buffer, offset, count);
                _position += bytesFromHeader;
                _logger.LogDebug("[ProducerConsumerStream] ReadAsync: read {BytesFromHeader} bytes from header buffer, new position={Position}", bytesFromHeader, _position);
                return bytesFromHeader;
            }
        }

        var totalRead = 0;

        while (totalRead < count)
        {
            // If we have a current chunk with remaining data, read from it
            if (_currentChunk != null && _currentChunkOffset < _currentChunk.Length)
            {
                var bytesToCopy = Math.Min(count - totalRead, _currentChunk.Length - _currentChunkOffset);
                Buffer.BlockCopy(_currentChunk, _currentChunkOffset, buffer, offset + totalRead, bytesToCopy);
                _currentChunkOffset += bytesToCopy;
                totalRead += bytesToCopy;
                _position += bytesToCopy;

                // If we've consumed the entire chunk, release it
                if (_currentChunkOffset >= _currentChunk.Length)
                {
                    _currentChunk = null;
                    _currentChunkOffset = 0;
                }

                continue;
            }

            // Try to get the next chunk
            if (_chunks.TryDequeue(out var nextChunk))
            {
                _currentChunk = nextChunk;
                _currentChunkOffset = 0;
                continue;
            }

            // No chunk available, check if we're done
            lock (_lock)
            {
                if (_isCompleted && _chunks.IsEmpty)
                {
                    _logger.LogDebug("[ProducerConsumerStream] ReadAsync: stream completed, returning {TotalRead} bytes", totalRead);
                    break;
                }
            }

            // Wait for more data
            try
            {
                await _dataAvailable.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }

        _logger.LogDebug("[ProducerConsumerStream] ReadAsync: returning {TotalRead} bytes, new position={Position}", totalRead, _position);
        return totalRead;
    }

    /// <summary>
    /// Reads data from the stream asynchronously using Memory buffer.
    /// </summary>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        // Delegate to the array-based implementation
        var array = new byte[buffer.Length];
        var bytesRead = await ReadAsync(array.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
        if (bytesRead > 0)
        {
            array.AsSpan(0, bytesRead).CopyTo(buffer.Span);
        }
        return bytesRead;
    }

    /// <summary>
    /// Seeks within the header buffer only. Seeking beyond the header buffer is not supported.
    /// </summary>
    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogDebug("[ProducerConsumerStream] Seek: offset={Offset}, origin={Origin}", offset, origin);

        // Wait for header to be ready before seeking
        WaitForHeader();

        var newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => throw new NotSupportedException("SeekOrigin.End is not supported."),
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        // Only allow seeking within the header buffer for format detection
        if (newPosition < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Cannot seek to a negative position.");
        }

        if (newPosition > _headerBuffer.Length)
        {
            throw new NotSupportedException($"Cannot seek beyond the header buffer ({_headerBufferSize} bytes). Requested position: {newPosition}");
        }

        _position = newPosition;
        _logger.LogDebug("[ProducerConsumerStream] Seek: new position={Position}", _position);
        return _position;
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Flush()
    {
        // No-op - data is immediately available after Write
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogDebug("[ProducerConsumerStream] Dispose: disposing stream (length={Length})", _length);

        if (disposing)
        {
            _disposed = true;
            _isCompleted = true;
            _headerBuffer.Dispose();
            _dataAvailable.Dispose();
            _headerReady.Dispose();
        }

        base.Dispose(disposing);
    }
}
