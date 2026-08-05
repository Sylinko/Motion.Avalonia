using System.Collections;
using Avalonia;
using Avalonia.Collections;
using Avalonia.LogicalTree;

namespace Motion.Avalonia.Utilities;

internal sealed class LogicalChildrenList<T>(StyledElement owner) : AvaloniaList<T> where T : ILogical
{
    public override void Add(T item)
    {
        base.Add(item);
        owner.LogicalChildren.Add(item);
    }

    public override void Clear()
    {
        foreach (var item in this) owner.LogicalChildren.Remove(item);
        base.Clear();
    }

    public override void Insert(int index, T item)
    {
        base.Insert(index, item);
        owner.LogicalChildren.Add(item);
    }

    public override void InsertRange(int index, IEnumerable<T> items)
    {
        if (items is IList list)
        {
            base.InsertRange(index, items);
            foreach (T item in list) owner.LogicalChildren.Add(item);
        }
        else
        {
            var itemsList = items.ToList();
            base.InsertRange(index, itemsList);
            foreach (var item in itemsList) owner.LogicalChildren.Add(item);
        }
    }

    public override bool Remove(T item)
    {
        return base.Remove(item) && owner.LogicalChildren.Remove(item);
    }

    public override void RemoveRange(int index, int count)
    {
        var removedItems = this.Skip(index).Take(count).ToArray();
        base.RemoveRange(index, count);
        foreach (var item in removedItems)
        {
            owner.LogicalChildren.Remove(item);
        }
    }

    public override void RemoveAt(int index)
    {
        var item = this[index];
        base.RemoveAt(index);
        owner.LogicalChildren.Remove(item);
    }
}