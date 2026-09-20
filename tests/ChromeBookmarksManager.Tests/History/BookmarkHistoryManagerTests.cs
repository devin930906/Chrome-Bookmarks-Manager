using System.Text.Json;
using ChromeBookmarksManager.Application.History;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Tests.History;

public sealed class BookmarkHistoryManagerTests
{
    [Fact]
    public void NewHistory_IsCleanAndHasNoUndoOrRedo()
    {
        var history = new BookmarkHistoryManager();

        Assert.Equal(200, history.Capacity);
        Assert.Equal(0, history.Count);
        Assert.Equal(0, history.Position);
        Assert.True(history.IsAtCleanState);
        Assert.True(history.IsCleanStateReachable);
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Null(history.UndoDescription);
        Assert.Null(history.RedoDescription);
    }

    [Fact]
    public void Record_Undo_Redo_TraversesInDeterministicOrder()
    {
        var document = Document();
        var trace = new List<string>();
        var history = new BookmarkHistoryManager();

        history.Record(new FakeEntry(
            "first",
            BookmarkHistoryImpact.SearchRelevant,
            () => trace.Add("undo first"),
            () => trace.Add("redo first")));
        history.Record(new FakeEntry(
            "second",
            BookmarkHistoryImpact.StructureOnly,
            () => trace.Add("undo second"),
            () => trace.Add("redo second")));

        Assert.Equal(2, history.Count);
        Assert.Equal(2, history.Position);
        Assert.False(history.IsAtCleanState);
        Assert.Equal("second", history.UndoDescription);
        Assert.Null(history.RedoDescription);

        var undoSecond = history.Undo(document);
        var undoFirst = history.Undo(document);

        Assert.Equal(
            new[] { "undo second", "undo first" },
            trace);
        Assert.True(undoSecond.Changed);
        Assert.Equal("second", undoSecond.Description);
        Assert.Equal(
            BookmarkHistoryImpact.StructureOnly,
            undoSecond.Impact);
        Assert.Equal(0, history.Position);
        Assert.True(history.IsAtCleanState);
        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);
        Assert.Equal("first", history.RedoDescription);

        var redoFirst = history.Redo(document);
        var redoSecond = history.Redo(document);

        Assert.Equal(
            new[]
            {
                "undo second",
                "undo first",
                "redo first",
                "redo second"
            },
            trace);
        Assert.True(redoFirst.Changed);
        Assert.True(redoSecond.Changed);
        Assert.Equal(2, history.Position);
        Assert.False(history.IsAtCleanState);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void RecordAfterUndo_TruncatesRedoBranch()
    {
        var document = Document();
        var history = new BookmarkHistoryManager();

        history.Record(Fake("one"));
        history.Record(Fake("two"));
        history.Record(Fake("three"));

        history.Undo(document);
        history.Undo(document);

        Assert.Equal(1, history.Position);
        Assert.True(history.CanRedo);
        Assert.Equal("two", history.RedoDescription);

        history.Record(Fake("replacement"));

        Assert.Equal(2, history.Count);
        Assert.Equal(2, history.Position);
        Assert.False(history.CanRedo);
        Assert.Null(history.RedoDescription);
        Assert.Equal("replacement", history.UndoDescription);
    }

    [Fact]
    public void RecordAfterUndo_PastMarkedCleanPoint_MakesCleanPointUnreachable()
    {
        var document = Document();
        var history = new BookmarkHistoryManager();

        history.Record(Fake("one"));
        history.Record(Fake("two"));
        history.MarkClean();

        history.Record(Fake("three"));
        history.Undo(document);
        history.Undo(document);

        Assert.True(history.CanRedo);
        Assert.False(history.IsAtCleanState);

        history.Record(Fake("replacement"));

        Assert.False(history.IsCleanStateReachable);
        Assert.False(history.IsAtCleanState);
    }

    [Fact]
    public void MarkClean_TracksCurrentCursorExactly()
    {
        var document = Document();
        var history = new BookmarkHistoryManager();

        history.Record(Fake("one"));
        history.Record(Fake("two"));

        history.MarkClean();

        Assert.True(history.IsAtCleanState);

        history.Undo(document);
        Assert.False(history.IsAtCleanState);

        history.Redo(document);
        Assert.True(history.IsAtCleanState);
    }

    [Fact]
    public void Clear_RemovesHistoryAndRestoresCleanBaseline()
    {
        var history = new BookmarkHistoryManager();

        history.Record(Fake("one"));
        history.Record(Fake("two"));

        history.Clear();

        Assert.Equal(0, history.Count);
        Assert.Equal(0, history.Position);
        Assert.True(history.IsAtCleanState);
        Assert.True(history.IsCleanStateReachable);
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void UndoFailure_DoesNotMoveCursor()
    {
        var document = Document();
        var history = new BookmarkHistoryManager();
        history.Record(new FakeEntry(
            "broken undo",
            BookmarkHistoryImpact.SearchRelevant,
            () => throw new InvalidOperationException("undo failed"),
            () => { }));

        var exception = Assert.Throws<InvalidOperationException>(
            () => history.Undo(document));

        Assert.Equal("undo failed", exception.Message);
        Assert.Equal(1, history.Position);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.False(history.IsAtCleanState);
    }

    [Fact]
    public void RedoFailure_DoesNotMoveCursor()
    {
        var document = Document();
        var history = new BookmarkHistoryManager();
        history.Record(new FakeEntry(
            "broken redo",
            BookmarkHistoryImpact.SearchRelevant,
            () => { },
            () => throw new InvalidOperationException("redo failed")));

        history.Undo(document);

        var exception = Assert.Throws<InvalidOperationException>(
            () => history.Redo(document));

        Assert.Equal("redo failed", exception.Message);
        Assert.Equal(0, history.Position);
        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);
        Assert.True(history.IsAtCleanState);
    }

    [Fact]
    public void Capacity_TrimsOldestEntriesAndMakesOriginalBaselineUnreachable()
    {
        var document = Document();
        var history = new BookmarkHistoryManager(capacity: 2);

        history.Record(Fake("one"));
        history.Record(Fake("two"));
        history.Record(Fake("three"));

        Assert.Equal(2, history.Count);
        Assert.Equal(2, history.Position);
        Assert.False(history.IsCleanStateReachable);
        Assert.False(history.IsAtCleanState);
        Assert.Equal("three", history.UndoDescription);

        history.Undo(document);
        history.Undo(document);

        Assert.Equal(0, history.Position);
        Assert.False(history.CanUndo);
        Assert.False(history.IsAtCleanState);
        Assert.False(history.IsCleanStateReachable);
    }

    [Fact]
    public void Capacity_AdjustsReachableCleanPositionWhenOlderEntriesAreTrimmed()
    {
        var document = Document();
        var history = new BookmarkHistoryManager(capacity: 2);

        history.Record(Fake("one"));
        history.MarkClean();
        history.Record(Fake("two"));
        history.Record(Fake("three"));

        Assert.True(history.IsCleanStateReachable);
        Assert.False(history.IsAtCleanState);

        history.Undo(document);
        history.Undo(document);

        Assert.True(history.IsAtCleanState);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositiveCapacity(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BookmarkHistoryManager(capacity));
    }

    [Fact]
    public void Record_RejectsNullEntry()
    {
        var history = new BookmarkHistoryManager();

        Assert.Throws<ArgumentNullException>(
            () => history.Record(null!));
    }

    [Fact]
    public void UndoRedoWithoutAvailableEntry_ReturnsNoChange()
    {
        var document = Document();
        var history = new BookmarkHistoryManager();

        var undo = history.Undo(document);
        var redo = history.Redo(document);

        Assert.False(undo.Changed);
        Assert.False(redo.Changed);
        Assert.Null(undo.Description);
        Assert.Null(redo.Description);
        Assert.Equal(BookmarkHistoryImpact.None, undo.Impact);
        Assert.Equal(BookmarkHistoryImpact.None, redo.Impact);
    }

    private static FakeEntry Fake(string description) =>
        new(
            description,
            BookmarkHistoryImpact.StructureOnly,
            () => { },
            () => { });

    private static BookmarkDocument Document()
    {
        var bar = Folder("1", "Bookmarks bar");
        var other = Folder("2", "Other bookmarks");
        var synced = Folder("3", "Mobile bookmarks");

        return new BookmarkDocument(
            1,
            null,
            null,
            new BookmarkRoots(
                bar,
                other,
                synced,
                EmptyProperties),
            EmptyProperties);
    }

    private static BookmarkFolder Folder(string id, string name) =>
        new(
            id,
            GuidFor(int.Parse(id)),
            name,
            null,
            null,
            null,
            null,
            EmptyProperties,
            Array.Empty<BookmarkNode>());

    private static Guid GuidFor(int value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, value);
        bytes[7] = 0x40;
        bytes[8] = 0x80;
        return new Guid(bytes);
    }

    private sealed class FakeEntry(
        string description,
        BookmarkHistoryImpact impact,
        Action undo,
        Action redo) : IBookmarkHistoryEntry
    {
        public string Description { get; } = description;

        public BookmarkHistoryImpact Impact { get; } = impact;

        public void Undo(BookmarkDocument document) => undo();

        public void Redo(BookmarkDocument document) => redo();
    }

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
