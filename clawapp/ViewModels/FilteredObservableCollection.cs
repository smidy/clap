using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace clawapp.ViewModels;

/// <summary>
/// An observable collection that filters items from a source collection.
/// Extends ObservableCollection to ensure proper Avalonia binding integration.
/// Automatically synchronizes with the source collection and refreshes when the filter changes.
/// This collection is read-only from the outside - items can only be added/removed by the filtering mechanism.
/// </summary>
/// <typeparam name="T">The type of items in the collection.</typeparam>
public sealed class FilteredObservableCollection<T> : ObservableCollection<T>
    where T : class
{
    private readonly ObservableCollection<T> _source;
    private readonly Func<T, bool> _filter;
    private bool _isUpdating;

    /// <summary>
    /// Gets a value indicating whether this collection is read-only (from external perspective).
    /// </summary>
    public bool IsReadOnly => true;

    /// <summary>
    /// Creates a new filtered collection that wraps the specified source collection.
    /// </summary>
    /// <param name="source">The source collection to filter.</param>
    /// <param name="filter">The predicate used to filter items.</param>
    public FilteredObservableCollection(ObservableCollection<T> source, Func<T, bool> filter)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _filter = filter ?? throw new ArgumentNullException(nameof(filter));

        // Initialize with filtered items from source
        Refresh();

        // Subscribe to source collection changes to keep filtered list in sync
        _source.CollectionChanged += OnSourceCollectionChanged;
    }

    /// <summary>
    /// Refreshes the filtered collection by re-applying the filter to all source items.
    /// Call this when the filter predicate's behavior changes (e.g., user toggles a setting).
    /// </summary>
    public void Refresh()
    {
        _isUpdating = true;
        try
        {
            // Clear current items using base method
            base.Clear();

            // Re-add all items that pass the filter using base method
            foreach (var item in _source.Where(_filter))
            {
                base.Add(item);
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_isUpdating)
            return;

        _isUpdating = true;
        try
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems != null)
                    {
                        foreach (T item in e.NewItems)
                        {
                            if (_filter(item))
                            {
                                // Insert at the correct position to maintain order
                                InsertItemInOrder(item);
                            }
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems != null)
                    {
                        foreach (T item in e.OldItems)
                        {
                            base.Remove(item);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Replace:
                    if (e.OldItems != null)
                    {
                        foreach (T item in e.OldItems)
                        {
                            base.Remove(item);
                        }
                    }
                    if (e.NewItems != null)
                    {
                        foreach (T item in e.NewItems)
                        {
                            if (_filter(item))
                            {
                                InsertItemInOrder(item);
                            }
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    // Full refresh when source is cleared
                    Refresh();
                    break;

                case NotifyCollectionChangedAction.Move:
                    // Full refresh on move to maintain order
                    Refresh();
                    break;
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    /// <summary>
    /// Inserts an item in order (at the end, since we're adding incrementally).
    /// This maintains the relative order of items from the source collection.
    /// </summary>
    private void InsertItemInOrder(T item)
    {
        // Find the correct position by checking the order in the source
        int sourceIndex = _source.IndexOf(item);
        if (sourceIndex < 0)
            return;

        // Find where this item should be inserted in the filtered collection
        // by finding the last item in the filtered collection that comes before it in the source
        int insertPosition = Count;
        for (int i = Count - 1; i >= 0; i--)
        {
            if (_source.IndexOf(this[i]) < sourceIndex)
            {
                insertPosition = i + 1;
                break;
            }
        }

        base.Insert(insertPosition, item);
    }

    #region Read-Only Enforcement - External callers cannot modify this collection

    /// <summary>
    /// Throws NotSupportedException - cannot add items directly to a filtered collection.
    /// </summary>
    public new void Add(T item) => throw new NotSupportedException("Cannot add items directly to a filtered collection. Items are added/removed based on the filter predicate.");

    /// <summary>
    /// Throws NotSupportedException - cannot remove items directly from a filtered collection.
    /// </summary>
    public new bool Remove(T item) => throw new NotSupportedException("Cannot remove items directly from a filtered collection. Items are removed when they no longer match the filter predicate.");

    /// <summary>
    /// Throws NotSupportedException - cannot clear a filtered collection.
    /// </summary>
    public new void Clear() => throw new NotSupportedException("Cannot clear a filtered collection directly. Clear the source collection instead.");

    /// <summary>
    /// Throws NotSupportedException - cannot insert items directly into a filtered collection.
    /// </summary>
    public new void Insert(int index, T item) => throw new NotSupportedException("Cannot insert items directly into a filtered collection.");

    /// <summary>
    /// Throws NotSupportedException - cannot remove items directly from a filtered collection.
    /// </summary>
    public new void RemoveAt(int index) => throw new NotSupportedException("Cannot remove items directly from a filtered collection.");

    #endregion
}
