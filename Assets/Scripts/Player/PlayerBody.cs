using UnityEngine;

// The one way to ask "is this collider the player?" for triggers. Only the player's Character Controller counts, so child
// colliders on the player (hands, dagger) can't fire a false enter/exit.
public static class PlayerBody
{
    public static bool Is(Collider other)
    {
        return other is CharacterController && other.GetComponentInParent<DeathManager>() != null;
    }
}
