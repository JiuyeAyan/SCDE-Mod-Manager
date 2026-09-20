using System;
using UnityEngine;

namespace SHCDESE.UI;

/// <summary>
/// Re-renders the logo effect every Unity frame. 
/// VideoPlayer frame callbacks only fire when a decoded frame changes, but the analog shader has its own time-based animation.
/// </summary>
public sealed class AnalogLogoRenderDriver : MonoBehaviour
{
    internal Action? RenderFrame { get; set; }
    private Action? _nextFrameAction;
    private int _notBeforeFrame = -1;

    internal void InvokeNextFrame(Action action)
    {
        if (action == null)
            return;

        _nextFrameAction += action;
        _notBeforeFrame = Math.Max(_notBeforeFrame, Time.frameCount + 1);
        enabled = true;
    }

    private void LateUpdate()
    {
        RenderFrame?.Invoke();

        Action? action = _nextFrameAction;
        if (action == null || Time.frameCount < _notBeforeFrame)
            return;

        _nextFrameAction = null;
        _notBeforeFrame = -1;
        action.Invoke();
    }
}
