using UnityEngine;

public interface ISpectatable
{
    Transform SpectateTarget { get; }
    string DisplayName { get; }
    bool CanBeSpectated { get; }
}
