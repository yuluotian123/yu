using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

/// <summary>Notifies owning graph documents when an indexed key changes.</summary>
public abstract class GraphStructuralData
{
    internal event Action StructureChanged;

    protected void SetStructuralValue<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        StructureChanged?.Invoke();
    }
}

internal sealed class GraphDataCollection<T> : Collection<T> where T : GraphStructuralData
{
    internal event Action Changed;

    private void NotifyChanged() => Changed?.Invoke();

    internal void StopObservingItems()
    {
        foreach (T item in this)
        {
            if (item != null)
                item.StructureChanged -= NotifyChanged;
        }
    }

    protected override void InsertItem(int index, T item)
    {
        base.InsertItem(index, item);
        if (item != null)
            item.StructureChanged += NotifyChanged;
        NotifyChanged();
    }

    protected override void RemoveItem(int index)
    {
        T previous = this[index];
        base.RemoveItem(index);
        if (previous != null)
            previous.StructureChanged -= NotifyChanged;
        NotifyChanged();
    }

    protected override void SetItem(int index, T item)
    {
        T previous = this[index];
        base.SetItem(index, item);
        if (previous != null)
            previous.StructureChanged -= NotifyChanged;
        if (item != null)
            item.StructureChanged += NotifyChanged;
        NotifyChanged();
    }

    protected override void ClearItems()
    {
        T[] previous = this.ToArray();
        base.ClearItems();
        foreach (T item in previous)
        {
            if (item != null)
                item.StructureChanged -= NotifyChanged;
        }
        NotifyChanged();
    }
}
