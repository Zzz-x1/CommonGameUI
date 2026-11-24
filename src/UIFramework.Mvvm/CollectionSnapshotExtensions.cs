using System.Collections.ObjectModel;

namespace UIFramework.Mvvm;

public static class CollectionSnapshotExtensions
{
    /// <summary>Applies a complete ordered snapshot while retaining rows with the same key.</summary>
    public static void ApplySnapshot<TRow, TData, TKey>(
        this ObservableCollection<TRow> rows, IReadOnlyList<TData> snapshot,
        Func<TRow, TKey> rowKey, Func<TData, TKey> dataKey,
        Func<TData, TRow> create, Action<TRow, TData> update)
        where TRow : class where TKey : notnull
    {
        if (rows == null) throw new ArgumentNullException(nameof(rows));
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        if (rowKey == null) throw new ArgumentNullException(nameof(rowKey));
        if (dataKey == null) throw new ArgumentNullException(nameof(dataKey));
        if (create == null) throw new ArgumentNullException(nameof(create));
        if (update == null) throw new ArgumentNullException(nameof(update));

        var existingKeys = new HashSet<TKey>();
        var order = new List<TKey>(rows.Count);
        foreach (var row in rows)
        {
            var key = rowKey(row);
            AddUniqueKey(existingKeys, key, nameof(rows));
            order.Add(key);
        }

        var incomingKeys = new HashSet<TKey>();
        var incoming = new (TKey Key, TData Data)[snapshot.Count];
        for (int i = 0; i < incoming.Length; i++)
        {
            var data = snapshot[i];
            var key = dataKey(data);
            AddUniqueKey(incomingKeys, key, nameof(snapshot));
            incoming[i] = (key, data);
        }

        for (int i = order.Count - 1; i >= 0; i--)
        {
            if (incomingKeys.Contains(order[i])) continue;
            rows.RemoveAt(i);
            order.RemoveAt(i);
        }

        // Positions before i already match the snapshot. Compare keys even if rows override Equals.
        for (int i = 0; i < incoming.Length; i++)
        {
            var (key, data) = incoming[i];
            if (!existingKeys.Contains(key))
            {
                var row = create(data);
                if (row == null || !EqualityComparer<TKey>.Default.Equals(rowKey(row), key))
                    throw new InvalidOperationException($"The row factory must return a row with snapshot key '{key}'.");
                rows.Insert(i, row);
                order.Insert(i, key);
                continue;
            }

            int current = order.IndexOf(key, i);
            if (current != i)
            {
                rows.Move(current, i);
                order.RemoveAt(current);
                order.Insert(i, key);
            }
            update(rows[i], data);
        }
    }

    private static void AddUniqueKey<TKey>(HashSet<TKey> keys, TKey key, string parameter) where TKey : notnull
    {
        if (key is null) throw new ArgumentException("A collection key cannot be null.", parameter);
        if (!keys.Add(key)) throw new ArgumentException($"Duplicate collection key '{key}'.", parameter);
    }
}
