using R3;

namespace SHCDESE.EventAPI;

/// <summary>
/// A generic wrapper for a game function hook that uses R3 observables.
/// Encapsulates the Subject, the public Observable stream, and the internal Raise method.
/// </summary>
/// <typeparam name="T">The type of the EventArgs for this specific hook.</typeparam>
public class R3EventHook<T> where T : EventHookBase
{
    /// <summary>
    /// The private, underlying subject that receives and broadcasts events.
    /// </summary>
    private readonly Subject<T> _subject = new();

    /// <summary>
    /// The public, read-only observable stream. 
    /// Mod authors subscribe to this stream to receive hook notifications.
    /// </summary>
    public Observable<T> Observable => _subject;

    /// <summary>
    /// For internal use by the script extender's hook implementations.
    /// Pushes a new event notification to all subscribers of the Stream.
    /// </summary>
    /// <param name="eventArgs">The event data to broadcast.</param>
    internal void Raise(T eventArgs)
    {
        // Add a null check for robustness, although in this pattern it should never be null.
        if (eventArgs != null)
        {
            _subject.OnNext(eventArgs);
        }
    }
}
