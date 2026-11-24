using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Xunit;

namespace UIFramework.Mvvm.Tests;

public sealed class CollectionSnapshotTests
{
    private sealed class Row(string key)
    {
        public string Key { get; } = key;
        public string Layout { get; init; } = "basic";
        public int Value { get; set; }
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void InvalidKeyIsRejectedBeforeNotificationsOrCallbacks(bool inRows, bool duplicate)
    {
        string badKey = duplicate ? "A" : null!;
        var rows = new ObservableCollection<Row> { new("A"), new(inRows ? badKey : "B") };
        string[] snapshot = inRows ? ["A", "C"] : ["A", badKey];
        int calls = 0;
        rows.CollectionChanged += (_, _) => calls++;
        var error = Assert.Throws<ArgumentException>(() => rows.ApplySnapshot(snapshot, row => row.Key, key => key,
            key => { calls++; return new Row(key); }, (_, _) => calls++));
        Assert.Equal(inRows ? "rows" : "snapshot", error.ParamName);
        Assert.Equal(0, calls);
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void MatchingOrderOnlyRefreshesValuesAndChangedLayoutReplacesTheRow()
    {
        var rows = new ObservableCollection<Row> { new("sword") };
        var original = rows[0];
        var events = new List<NotifyCollectionChangedAction>();
        rows.CollectionChanged += (_, args) => events.Add(args.Action);
        rows.ApplySnapshot(new[] { (Key: "sword", Price: 80) }, row => row.Key, item => item.Key,
            item => throw new InvalidOperationException("Unexpected creation"), (row, item) => row.Value = item.Price);
        Assert.Same(original, rows[0]);
        Assert.Equal(80, original.Value);
        Assert.Empty(events);

        rows.ApplySnapshot(new[] { (Key: "sword", Layout: "bundle") }, row => (row.Key, row.Layout), item => item,
            item => new Row(item.Key) { Layout = item.Layout }, (_, _) => throw new InvalidOperationException("Unexpected update"));
        Assert.NotSame(original, Assert.Single(rows));
        Assert.Equal("bundle", rows[0].Layout);
        Assert.Equal(new[] { NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Add }, events);
    }

    [Fact]
    public void FactoryKeyMismatchDoesNotInsertTheInvalidRow()
    {
        var rows = new ObservableCollection<Row>();
        Assert.Throws<InvalidOperationException>(() => rows.ApplySnapshot(new[] { "ok", "bad" }, row => row.Key, key => key,
            key => new Row(key == "bad" ? "unexpected" : key), (_, _) => { }));
        Assert.Equal("ok", Assert.Single(rows).Key);
    }

    [Fact]
    public void CallbackErrorKeepsItsIdentityAndAlreadyNotifiedChanges()
    {
        var rows = new ObservableCollection<Row> { new("old"), new("A") };
        var failure = new IOException("Price service");
        var error = Assert.Throws<IOException>(() => rows.ApplySnapshot(new[] { "B", "A" }, row => row.Key, key => key,
            key => new Row(key), (_, _) => throw failure));
        Assert.Same(failure, error);
        Assert.Equal(new[] { "B", "A" }, rows.Select(row => row.Key));
    }
}
