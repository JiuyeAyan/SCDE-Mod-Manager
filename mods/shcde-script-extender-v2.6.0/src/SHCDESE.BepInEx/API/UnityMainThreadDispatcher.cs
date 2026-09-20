using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace SHCDESE.API;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static readonly ConcurrentQueue<Action> _executionQueue = new ConcurrentQueue<Action>();
    private static volatile UnityMainThreadDispatcher _instance;
    private static volatile int _mainThreadId = -1;
    private static readonly object _lock = new object();

    public static UnityMainThreadDispatcher Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

            // Only try to create/find on the main thread
            // Check if we're on a thread that could be the Unity main thread
            int currentThreadId = Thread.CurrentThread.ManagedThreadId;

            // If _mainThreadId is initialized and we're NOT on it, return null
            if (_mainThreadId != -1 && currentThreadId != _mainThreadId)
            {
                return null;
            }

            // Otherwise, try to initialize (thread-safe)
            lock (_lock)
            {
                if (_instance != null)
                    return _instance;

                _instance = FindObjectOfType<UnityMainThreadDispatcher>();
                if (_instance == null)
                {
                    GameObject singleton = new GameObject("SHCDESE_MainThreadDispatcher");
                    _instance = singleton.AddComponent<UnityMainThreadDispatcher>();
                    DontDestroyOnLoad(singleton);
                }
            }

            return _instance;
        }
    }

    void Awake()
    {
        lock (_lock)
        {
            // Ensure there's only one instance
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            Debug.Log($"[UnityMainThreadDispatcher] Unity Thread Id: {_mainThreadId}");
            DontDestroyOnLoad(gameObject);
        }
    }

    public void Update()
    {
        // Process all queued actions
        while (_executionQueue.TryDequeue(out Action action))
        {
            if (action == null)
                continue;

            try
            {
                action.Invoke();
            }
            catch (Exception ex)
            {
                // Use Debug.LogError to avoid circular dependency with LogHelper
                Debug.LogError($"[UnityMainThreadDispatcher] Error during dequeued action: {ex}");
            }
        }
    }

    /// <summary>
    /// Safe entry point for background threads. 
    /// Checks for existence without triggering Unity Native API calls.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Dispatch(Action action)
    {
        if (action == null)
            return;

        UnityMainThreadDispatcher instance = _instance;
        if (instance == null)
        {
            // Fallback: execute immediately if dispatcher isn't ready
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityMainThreadDispatcher] Failed to dispatch (no instance): {ex}");
            }
            return;
        }

        instance.Enqueue(action);
    }

    /// <summary>
    /// Returns if the caller is running on the main thread.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsCallingFromMainThread()
    {
        return _mainThreadId != -1 && Thread.CurrentThread.ManagedThreadId == _mainThreadId;
    }

    /// <summary>
    /// Enqueues an action to be executed on the main Unity thread.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    public void Enqueue(Action action)
    {
        if (action == null)
            return;

        // If on main thread, execute immediately
        if (_mainThreadId != -1 && Thread.CurrentThread.ManagedThreadId == _mainThreadId)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityMainThreadDispatcher] Error executing immediate action: {ex}");
            }
            return;
        }

        // Otherwise, queue it
        _executionQueue.Enqueue(action);
    }

    /// <summary>
    /// Always defers execution to the next Update(), even if called from the main thread.
    /// </summary>
    public void EnqueueDeferred(Action action)
    {
        if (action == null) return;
        _executionQueue.Enqueue(action);
    }

    /// <summary>
    /// Thread-safe enqueue that never touches any Unity native object.
    /// </summary>
    public static void EnqueueStatic(Action action)
    {
        if (action == null) return;
        _executionQueue.Enqueue(action);
    }

    /// <summary>
    /// Enqueues a function to be executed on the main thread and blocks until it returns a result.
    /// Returns default on error.
    /// IMPORTANT: UNDER NO CIRCUMSTANCE MUST YOU CALL THIS FROM WITHIN THE NATIVE ENGINE.
    /// NEVER CALL THIS FUNCTION IN A SCENARIO WHERE THE MAIN UNITY THREAD IS OR MIGHT BECOME WAITING FOR THIS TO FINISH.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("Dont fucking use this, and if you run into issues, you're on your own.")]
    public T EnqueueAndWait<T>(Func<T> func)
    {
        if (func == null)
            return default;

        try
        {
            // If already on Unity's main thread, execute immediately
            if (_mainThreadId != -1 && Thread.CurrentThread.ManagedThreadId == _mainThreadId)
            {
                return func();
            }

            T result = default;
            using (ManualResetEvent evt = new ManualResetEvent(false))
            {
                _executionQueue.Enqueue(() =>
                {
                    try
                    {
                        result = func();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[UnityMainThreadDispatcher] Error in EnqueueAndWait: {ex}");
                    }
                    finally
                    {
                        evt.Set();
                    }
                });

                // Block until executed on main thread
                evt.WaitOne();
            }

            return result;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UnityMainThreadDispatcher] Fatal error in EnqueueAndWait: {ex}");
        }

        return default;
    }

    /// <summary>
    /// A generic helper to safely execute a function on the main Unity thread and return its result to a Lua callback.
    /// This encapsulates all the boilerplate for null-checking, dispatching, error handling, and invoking the callback.
    /// </summary>
    /// <typeparam name="T">The type of the value that the work function will return.</typeparam>
    /// <param name="workFunction">The function to execute on the main thread. It must return a value of type T.</param>
    /// <param name="onCompleteCallback">The Lua function to call with the result.</param>
    public static void DispatchGetAsync<T>(Func<T> workFunction, NLua.LuaFunction onCompleteCallback)
    {
        if (onCompleteCallback == null || workFunction == null)
            return;

        UnityMainThreadDispatcher instance = Instance;
        if (instance == null)
        {
            Debug.LogError("[UnityMainThreadDispatcher] Cannot dispatch async - dispatcher not initialized");
            return;
        }

        instance.Enqueue(() =>
        {
            T result = default;
            try
            {
                result = workFunction();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityMainThreadDispatcher] Error in DispatchGetAsync: {ex}");
            }
            finally
            {
                try
                {
                    onCompleteCallback.Call(result);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UnityMainThreadDispatcher] Error calling Lua callback: {ex}");
                }
            }
        });
    }
}