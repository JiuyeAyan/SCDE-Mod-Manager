using Noesis;

namespace SHCDESE.NoesisUtil;

/// <summary>
/// Receives the Noesis element to which a registered ViewModel was attached.
/// </summary>
/// <remarks>
/// This is useful for controls whose routed events cannot be expressed as ViewModel
/// bindings, such as <c>MediaElement.MediaEnded</c>.
/// </remarks>
public interface INoesisElementBindingAware
{
    void OnNoesisElementBound(FrameworkElement element);
}
