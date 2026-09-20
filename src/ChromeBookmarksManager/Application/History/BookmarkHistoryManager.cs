using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.History;

public sealed class BookmarkHistoryManager
{
    public const int DefaultCapacity = 200;

    private readonly List<IBookmarkHistoryEntry> _entries = new();
    private int _position;
    private int? _cleanPosition = 0;

    public BookmarkHistoryManager(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                capacity,
                "History capacity must be greater than zero.");
        }

        Capacity = capacity;
    }

    public int Capacity { get; }

    public int Count => _entries.Count;

    public int Position => _position;

    public bool CanUndo => _position > 0;

    public bool CanRedo => _position < _entries.Count;

    public bool IsCleanStateReachable => _cleanPosition.HasValue;

    public bool IsAtCleanState =>
        _cleanPosition is int cleanPosition &&
        cleanPosition == _position;

    public string? UndoDescription =>
        CanUndo
            ? _entries[_position - 1].Description
            : null;

    public string? RedoDescription =>
        CanRedo
            ? _entries[_position].Description
            : null;

    public void Record(IBookmarkHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (_position < _entries.Count)
        {
            if (_cleanPosition is int cleanPosition &&
                cleanPosition > _position)
            {
                _cleanPosition = null;
            }

            _entries.RemoveRange(
                _position,
                _entries.Count - _position);
        }

        _entries.Add(entry);
        _position++;

        TrimToCapacity();
    }

    public BookmarkHistoryResult Undo(BookmarkDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (!CanUndo)
        {
            return BookmarkHistoryResult.NoChange;
        }

        var entry = _entries[_position - 1];

        entry.Undo(document);
        _position--;

        return new BookmarkHistoryResult(
            true,
            entry.Description,
            entry.Impact);
    }

    public BookmarkHistoryResult Redo(BookmarkDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (!CanRedo)
        {
            return BookmarkHistoryResult.NoChange;
        }

        var entry = _entries[_position];

        entry.Redo(document);
        _position++;

        return new BookmarkHistoryResult(
            true,
            entry.Description,
            entry.Impact);
    }

    public void MarkClean()
    {
        _cleanPosition = _position;
    }

    public void Clear()
    {
        _entries.Clear();
        _position = 0;
        _cleanPosition = 0;
    }

    private void TrimToCapacity()
    {
        var trimCount = _entries.Count - Capacity;
        if (trimCount <= 0)
        {
            return;
        }

        _entries.RemoveRange(0, trimCount);
        _position -= trimCount;

        if (_cleanPosition is not int cleanPosition)
        {
            return;
        }

        if (cleanPosition < trimCount)
        {
            _cleanPosition = null;
            return;
        }

        _cleanPosition = cleanPosition - trimCount;
    }
}
