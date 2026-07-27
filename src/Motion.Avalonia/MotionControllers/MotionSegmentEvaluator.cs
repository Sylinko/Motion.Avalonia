using System.Numerics;
using Avalonia.Animation.Easings;

namespace Motion.Avalonia;

/// <summary>
/// Evaluates poses and stable ordering for controllers built from ordered state nodes.
/// </summary>
/// <remarks>
/// Callers provide nodes in ascending coordinate order. Equal coordinates retain declaration
/// order, and evaluation selects the last node at an exact duplicate coordinate. Easing belongs
/// to the destination node of each non-zero interval.
/// </remarks>
internal static class MotionSegmentEvaluator
{
    /// <summary>
    /// Evaluates a state pose or an in-between blend at the supplied coordinate.
    /// </summary>
    public static MotionPose Evaluate<TItem, TCoordinate>(
        IReadOnlyList<TItem> items,
        TCoordinate position,
        Func<TItem, TCoordinate> getPosition,
        Func<TItem, object?> getState,
        Func<TItem, Easing?> getEasing,
        Func<TCoordinate, TCoordinate, bool>? positionsEqual = null)
        where TCoordinate : INumber<TCoordinate>
    {
        if (items.Count == 0)
        {
            return MotionPose.Empty;
        }

        var first = items[0];
        if (position < getPosition(first))
        {
            return MotionPose.State(getState(first));
        }

        var sourceIndex = FindSourceIndex(items, position, getPosition);
        var source = items[sourceIndex];
        var sourcePosition = getPosition(source);
        if (sourceIndex == items.Count - 1 ||
            positionsEqual?.Invoke(position, sourcePosition) == true ||
            positionsEqual is null && position == sourcePosition)
        {
            return MotionPose.State(getState(source));
        }

        var target = items[sourceIndex + 1];
        var targetPosition = getPosition(target);
        var localProgress =
            double.CreateChecked(position - sourcePosition) /
            double.CreateChecked(targetPosition - sourcePosition);

        return MotionPose.CreateBlend(
            MotionPose.State(getState(source)),
            MotionPose.State(getState(target)),
            localProgress,
            getEasing(target));
    }

    /// <summary>
    /// Inserts an item by ascending coordinate while preserving declaration order for equal values.
    /// </summary>
    public static void InsertOrdered<TItem, TCoordinate>(
        List<TItem> items,
        TItem item,
        Func<TItem, TCoordinate> getPosition)
        where TCoordinate : INumber<TCoordinate>
    {
        var position = getPosition(item);
        var index = items.FindIndex(existing => getPosition(existing) > position);
        if (index < 0)
        {
            items.Add(item);
        }
        else
        {
            items.Insert(index, item);
        }
    }

    private static int FindSourceIndex<TItem, TCoordinate>(
        IReadOnlyList<TItem> items,
        TCoordinate position,
        Func<TItem, TCoordinate> getPosition)
        where TCoordinate : INumber<TCoordinate>
    {
        var lower = 0;
        var upper = items.Count - 1;

        while (lower <= upper)
        {
            var middle = lower + ((upper - lower) / 2);
            if (getPosition(items[middle]) <= position)
            {
                lower = middle + 1;
            }
            else
            {
                upper = middle - 1;
            }
        }

        return Math.Max(upper, 0);
    }
}