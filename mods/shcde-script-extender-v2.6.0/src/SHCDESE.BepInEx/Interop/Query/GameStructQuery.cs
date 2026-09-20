using System;
using System.Collections.Generic;

namespace SHCDESE.Interop.Query;

public delegate ref TField FieldSelector<T, TField>(ref T item)
    where T : unmanaged
    where TField : unmanaged;

public delegate bool RefPredicate<T>(in T item) where T : unmanaged;

/// <summary>
/// Callback invoked for each matched element.
/// </summary>
/// <param name="item">The matched element, by reference.</param>
/// <param name="idOrIndex">
/// The one-based game ID when invoked via <c>GameStructQuery{T}.ForEach</c>, or the raw zero-based
/// slot index when invoked via <c>GameStructQuery{T}.ForEachIndex</c>. Which one you get is
/// determined by the method you call, so prefer <c>ForEach</c> unless you are indexing a span
/// directly.
/// </param>
public delegate void RefAction<T>(in T item, int idOrIndex) where T : unmanaged;

/// <summary>
/// A lightweight, allocation-free query over a native game struct array.
/// </summary>
/// <remarks>
/// <para>
/// <b>ID convention.</b> The game stores entities in flat, zero-indexed native arrays, but every
/// public accessor in the script extender (<c>TryGetBuildingById</c>, <c>TryGetUnitById</c>,
/// <c>TryGetTribeById</c>, ...) takes a <b>one-based game ID</b> and internally resolves it as
/// <c>_array[id - 1]</c>. The corresponding <c>IsValidId</c> checks reject <c>0</c>.
/// </para>
/// <para>
/// To keep the whole surface consistent, everything this query type hands back to a caller is a
/// <b>one-based game ID</b>. Members that deliberately expose the raw zero-based slot index are
/// named explicitly (<see cref="ToIndexList"/>, <see cref="ForEachIndex(Action{int})"/>,
/// <see cref="Enumerator.CurrentIndex"/>).
/// </para>
/// <para>
/// The rule of thumb: <c>id == index + 1</c>. Anything named <c>...Id...</c> is safe to pass
/// straight into a <c>TryGet...ById</c> method; anything named <c>...Index...</c> is only valid
/// for direct span/array indexing.
/// </para>
/// </remarks>
public unsafe readonly ref struct GameStructQuery<T> where T : unmanaged
{
    private readonly Span<T> _data;
    private readonly RefPredicate<T>? _predicate;

    internal GameStructQuery(Span<T> data)
    {
        _data = data;
        _predicate = null;
    }

    internal GameStructQuery(T* start, int length)
    {
        _data = new Span<T>(start, length);
        _predicate = null;
    }

    private GameStructQuery(T* start, int length, RefPredicate<T> predicate)
    {
        _data = new Span<T>(start, length);
        _predicate = predicate;
    }

    internal GameStructQuery(Span<T> data, RefPredicate<T> predicate)
    {
        _data = data;
        _predicate = predicate;
    }

    /// <summary>
    /// Executes the query and fills a pre-allocated list with the resulting <b>one-based game IDs</b>.
    /// </summary>
    /// <param name="results">The list to be cleared and filled with game IDs.</param>
    /// <remarks>
    /// <para>
    /// Every value written to <paramref name="results"/> is <c>slot index + 1</c> and can be passed
    /// directly to the matching <c>TryGet...ById</c> accessor without any further adjustment.
    /// </para>
    /// </remarks>
    public void ToIdList(List<int> results)
    {
        results.Clear();
        for (int i = 0; i < _data.Length; i++)
        {
            if (_predicate == null || _predicate(in _data[i]))
            {
                results.Add(i + 1);
            }
        }
    }

    /// <summary>
    /// Executes the query and fills a pre-allocated list with the raw <b>zero-based slot indices</b>
    /// of the matched elements.
    /// </summary>
    /// <param name="results">The list to be cleared and filled with slot indices.</param>
    /// <remarks>
    /// Only use this when indexing directly into a span or native array obtained from
    /// <c>Get...AsSpan()</c> / <c>Get...Array()</c>. These values are <b>not</b> game IDs and must
    /// not be passed to a <c>TryGet...ById</c> accessor or written into a native ID field.
    /// Use <see cref="ToIdList"/> for that.
    /// </remarks>
    public void ToIndexList(List<int> results)
    {
        results.Clear();
        for (int i = 0; i < _data.Length; i++)
        {
            if (_predicate == null || _predicate(in _data[i]))
            {
                results.Add(i);
            }
        }
    }

    /// <summary>
    /// Filters the sequence of units based on a predicate.
    /// This method is chainable and uses deferred execution.
    /// </summary>
    public readonly GameStructQuery<T> Where(RefPredicate<T> predicate)
    {
        if (_predicate == null)
        {
            return new GameStructQuery<T>(_data, predicate);
        }

        RefPredicate<T> capturedPredicate = _predicate;
        return new GameStructQuery<T>(_data, (in T unit) => capturedPredicate(in unit) && predicate(in unit));
    }

    public Enumerator GetEnumerator() => new Enumerator(this);

    /// <summary>
    /// Executes the query and invokes an action for each matched element, passing the
    /// <b>one-based game ID</b> (<c>slot index + 1</c>).
    /// Avoids allocating an intermediate List compared to <see cref="ToIdList"/> + foreach.
    /// </summary>
    public void ForEach(Action<int> action)
    {
        for (int i = 0; i < _data.Length; i++)
        {
            if (_predicate == null || _predicate(in _data[i]))
                action(i + 1);
        }
    }

    /// <summary>
    /// Overload that also passes the matched element by reference, avoiding a second
    /// array lookup if the caller needs both the game ID and the struct data.
    /// The <c>int</c> argument is the <b>one-based game ID</b>.
    /// </summary>
    public void ForEach(RefAction<T> action)
    {
        for (int i = 0; i < _data.Length; i++)
        {
            if (_predicate == null || _predicate(in _data[i]))
                action(in _data[i], i + 1);
        }
    }

    /// <summary>
    /// Executes the query and invokes an action for each matched element, passing the raw
    /// <b>zero-based slot index</b>. Only valid for direct span/array indexing.
    /// </summary>
    public void ForEachIndex(Action<int> action)
    {
        for (int i = 0; i < _data.Length; i++)
        {
            if (_predicate == null || _predicate(in _data[i]))
                action(i);
        }
    }

    /// <summary>
    /// Overload that also passes the matched element by reference. The <c>int</c> argument is the
    /// raw <b>zero-based slot index</b>. Only valid for direct span/array indexing.
    /// </summary>
    public void ForEachIndex(RefAction<T> action)
    {
        for (int i = 0; i < _data.Length; i++)
        {
            if (_predicate == null || _predicate(in _data[i]))
                action(in _data[i], i);
        }
    }

    public ref struct Enumerator
    {
        private readonly Span<T> _data;
        private readonly RefPredicate<T>? _predicate;
        private int _currentIndex;

        internal Enumerator(GameStructQuery<T> query)
        {
            _data = query._data;
            _predicate = query._predicate;
            _currentIndex = -1;
        }

        public readonly ref T Current => ref _data[_currentIndex];

        /// <summary>
        /// The <b>one-based game ID</b> of <see cref="Current"/>, usable with the
        /// <c>TryGet...ById</c> accessors.
        /// </summary>
        public readonly int CurrentId => _currentIndex + 1;

        /// <summary>
        /// The raw <b>zero-based slot index</b> of <see cref="Current"/> within the native array.
        /// Only valid for direct span/array indexing; not a game ID.
        /// </summary>
        public readonly int CurrentIndex => _currentIndex;

        public bool MoveNext()
        {
            for (int i = _currentIndex + 1; i < _data.Length; i++)
            {
                if (_predicate == null || _predicate(in _data[i]))
                {
                    _currentIndex = i;
                    return true;
                }
            }
            return false;
        }
    }
}
