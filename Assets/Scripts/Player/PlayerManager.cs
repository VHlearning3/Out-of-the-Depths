using System;
using UnityEngine;

// Keeps the Player's Inspector tidy. The Player carries twenty-odd scripts (swimming, health, hunger, the slash, the
// menus...), which all stay right where they are, on this object, since they find each other there. While this
// component is on and switched on, the others are hidden from the Inspector and shown by this one instead, in folding
// groups (Movement, Survival, Combat, Interaction & items, Menus & UI, Look, Other), each script with its own on/off
// box and all its settings. Untick it (or remove it) and every script shows the normal way again. Only changes how the
// Inspector looks: nothing about how the game runs.
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Out of the Depths/Player Manager")]
public class PlayerManager : MonoBehaviour
{
    // Which group a script goes in, by class name. Anything not listed goes in Other, so nothing is ever out of sight.
    public static readonly (string group, string[] types)[] Groups =
    {
        ("Movement", new[] { "CharacterController", "SwimController", "SwimAudio", "BoxPush", "SpritePushAnimator", "PlayerTrail", "ComfortSettings" }),
        ("Survival", new[] { "HealthSystem", "HungerSystem", "DamageManager", "DeathManager", "PlayerPanic", "CheckpointSave" }),
        ("Combat", new[] { "SlashAttack", "SlashTrail", "HeldWeapon" }),
        ("Interaction & items", new[] { "PlayerInteractor", "PlayerInventory", "MashE" }),
        ("Menus & UI", new[] { "PauseMenu", "SettingsPage", "KeybindingsPage", "MapPage", "CreditsPage", "AdminPanel" }),
        ("Look", new[] { "UnderwaterLighting", "Volume" }),
    };

    public const string Other = "Other";

    public static string GroupOf(Component c)
    {
        string name = c.GetType().Name;
        foreach (var (group, types) in Groups)
            if (Array.IndexOf(types, name) >= 0)
                return group;
        return Other;
    }

#if UNITY_EDITOR
    private void OnEnable() => Tidy(true);
    private void OnDisable() => Tidy(false);

    // Hides (or shows again) every other component on this object in the Inspector; the manager draws them instead.
    // The Inspector is rebuilt (once, after this frame) when anything changed.
    public void Tidy(bool hide)
    {
        bool changed = false;
        foreach (Component c in GetComponents<Component>())
        {
            if (c == null || c == this || c is Transform)
                continue;
            HideFlags flags = hide ? c.hideFlags | HideFlags.HideInInspector : c.hideFlags & ~HideFlags.HideInInspector;
            if (flags != c.hideFlags)
            {
                c.hideFlags = flags;
                changed = true;
            }
        }
        if (changed)
            UnityEditor.EditorApplication.delayCall += () => UnityEditor.ActiveEditorTracker.sharedTracker.ForceRebuild();
    }
#endif
}
