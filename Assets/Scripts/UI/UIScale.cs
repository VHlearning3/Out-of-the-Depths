using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// How big the in-game HUD is (the Settings page's HUD size, remembered in PlayerPrefs): the HUD canvas (the object
// named HUD: health and hunger bars, hotbar, prompt, counters, inspect hint) is scaled by it, and the HUD drawn in
// code (the dash ring, the fish health bar and damage numbers, tips) multiplies its size by Hud. Applied as every
// scene loads and whenever it changes.
public static class UIScale
{
    public const string PrefsKey = "settings.hudScale";
    public const float Default = 1.35f;
    public const float Min = 0.75f;
    public const float Max = 2f;

    public static float Hud { get; private set; } = Default;
    public static event Action Changed;

    // Each HUD canvas's own reference resolution, before the HUD size was applied (by instance, for this session).
    private static readonly System.Collections.Generic.Dictionary<int, Vector2> designed = new System.Collections.Generic.Dictionary<int, Vector2>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Load()
    {
        Hud = Mathf.Clamp(PlayerPrefs.GetFloat(PrefsKey, Default), Min, Max);
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

    public static void Set(float value)
    {
        value = Mathf.Clamp(value, Min, Max);
        if (Mathf.Approximately(value, Hud))
            return;
        Hud = value;
        PlayerPrefs.SetFloat(PrefsKey, value);
        Apply();
        Changed?.Invoke();
    }

    public static void Reset()
    {
        PlayerPrefs.DeleteKey(PrefsKey);
        Hud = Default;
        Apply();
        Changed?.Invoke();
    }

    // The HUD canvas: its reference resolution shrinks as the scale grows, so everything on it draws bigger.
    public static void Apply()
    {
        foreach (CanvasScaler scaler in UnityEngine.Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize || !IsHud(scaler.transform))
                continue;
            int id = scaler.GetInstanceID();
            if (!designed.TryGetValue(id, out Vector2 resolution))
            {
                resolution = scaler.referenceResolution;
                designed[id] = resolution;
            }
            scaler.referenceResolution = resolution / Hud;
        }
    }

    private static bool IsHud(Transform t)
    {
        for (; t != null; t = t.parent)
            if (t.name == "HUD")
                return true;
        return false;
    }
}
