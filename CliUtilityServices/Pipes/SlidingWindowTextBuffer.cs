using System.Text;

namespace CliUtilityServices.Pipes;

/// <summary>
/// Maintains a bounded sliding window of decoded text lines.
/// </summary>
internal sealed class SlidingWindowTextBuffer
{
    private const int MaximumInitialBuilderCapacity = 64 * 1024;
    private const string TruncationMarker =
        "[... Outputs truncated for memory defense ...]";

    private readonly int _maxLines;
    private readonly int _maxRetainedCharacters;
    private readonly Queue<string> _lines = new();
    private readonly object _syncRoot = new();

    private int _retainedCharacters;
    private bool _wasTruncated;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="SlidingWindowTextBuffer"/> class.
    /// </summary>
    /// <param name="maxLines">
    /// The maximum number of retained lines.
    /// </param>
    /// <param name="maxRetainedCharacters">
    /// The maximum number of retained payload characters.
    /// </param>
    public SlidingWindowTextBuffer(
        int maxLines,
        int maxRetainedCharacters)
    {
        if (maxLines <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxLines),
                maxLines,
                "Maximum retained lines must be greater than zero.");
        }

        if (maxRetainedCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxRetainedCharacters),
                maxRetainedCharacters,
                "Maximum retained characters must be greater than zero.");
        }

        _maxLines = maxLines;
        _maxRetainedCharacters = maxRetainedCharacters;
    }

    /// <summary>
    /// Adds a completed line to the sliding window.
    /// </summary>
    /// <param name="line">
    /// The completed line.
    /// </param>
    /// <param name="wasLineTruncated">
    /// Indicates whether characters were discarded before the line
    /// reached this buffer.
    /// </param>
    public void AddLine(
        string line,
        bool wasLineTruncated = false)
    {
        ArgumentNullException.ThrowIfNull(line);

        lock (_syncRoot)
        {
            if (wasLineTruncated)
            {
                _wasTruncated = true;
            }

            string boundedLine =
                BoundLine(line);

            while (_lines.Count >= _maxLines)
            {
                RemoveOldestLine();
                _wasTruncated = true;
            }

            while (
                _lines.Count > 0
                && _retainedCharacters
                    > _maxRetainedCharacters
                        - boundedLine.Length)
            {
                RemoveOldestLine();
                _wasTruncated = true;
            }

            _lines.Enqueue(boundedLine);

            _retainedCharacters =
                checked(
                    _retainedCharacters
                    + boundedLine.Length);
        }
    }

    /// <summary>
    /// Marks the captured output as truncated.
    /// </summary>
    public void MarkTruncated()
    {
        lock (_syncRoot)
        {
            _wasTruncated = true;
        }
    }

    /// <summary>
    /// Builds an immutable snapshot of the retained output.
    /// </summary>
    /// <returns>
    /// The retained output including a truncation marker when data
    /// has been discarded.
    /// </returns>
    public string GetSnapshot()
    {
        lock (_syncRoot)
        {
            int capacity =
                CalculateInitialCapacity();

            var builder =
                new StringBuilder(capacity);

            if (_wasTruncated)
            {
                builder.AppendLine(
                    TruncationMarker);
            }

            foreach (string line in _lines)
            {
                builder.AppendLine(line);
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Restricts a line to the configured character budget.
    /// </summary>
    /// <param name="line">
    /// The line to restrict.
    /// </param>
    /// <returns>
    /// The original line when it fits, or its newest suffix when it
    /// exceeds the configured limit.
    /// </returns>
    private string BoundLine(
        string line)
    {
        if (line.Length <= _maxRetainedCharacters)
        {
            return line;
        }

        _wasTruncated = true;

        ReadOnlySpan<char> retained =
            line.AsSpan(
                line.Length
                - _maxRetainedCharacters);

        return retained.ToString();
    }

    /// <summary>
    /// Removes the oldest retained line while preserving the internal
    /// character-accounting invariant.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the retained-character accounting is inconsistent
    /// with the buffered line data.
    /// </exception>
    private void RemoveOldestLine()
    {
        if (_lines.Count == 0)
        {
            throw new InvalidOperationException(
                "Cannot remove a line because the sliding window is empty.");
        }

        string removed =
            _lines.Peek();

        if (_retainedCharacters < removed.Length)
        {
            throw new InvalidOperationException(
                "The retained-character count is inconsistent with the sliding window.");
        }

        _lines.Dequeue();

        _retainedCharacters =
            checked(
                _retainedCharacters
                - removed.Length);
    }


    /// <summary>
    /// Calculates an overflow-safe initial capacity for the snapshot.
    /// </summary>
    /// <returns>
    /// A safe initial capacity.
    /// </returns>
    private int CalculateInitialCapacity()
    {
        long capacity =
            _retainedCharacters;

        capacity +=
            (long)_lines.Count
            * Environment.NewLine.Length;

        if (_wasTruncated)
        {
            capacity +=
                TruncationMarker.Length
                + Environment.NewLine.Length;
        }

        return (int)Math.Min(
            capacity,
            MaximumInitialBuilderCapacity);
    }
}