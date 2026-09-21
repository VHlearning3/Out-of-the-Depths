using System.Collections.Generic;
using UnityEngine;

// The controls a pause-menu page draws with. Buttons and toggles work exactly like the GUILayout ones but tell the
// menu when the mouse is over them (the hover tick) and ease their highlight in and out instead of snapping; the
// rest are the menu's own: section headings, settings rows with a switch (its knob slides), slider, stepper or button
// on the right, key caps for bindings, and the sidebar entries. The menu fills in the styles and textures when it
// builds its skin and calls Tick every frame; drawn outside the menu they fall back to the default skin.
public static class MenuGUI
{
    public const float RowHeight = 44f;

    public static GUIStyle SectionStyle;
    public static GUIStyle RowLabelStyle;
    public static GUIStyle ValueStyle;
    public static GUIStyle NoteStyle;
    public static GUIStyle SwitchStyle;        // only its size and margins are used; the switch is drawn by hand
    public static GUIStyle KeyStyle;
    public static GUIStyle KeyListeningStyle;
    public static GUIStyle SmallButtonStyle;
    public static GUIStyle DangerStyle;
    public static GUIStyle HighlightStyle;     // rounded white, drawn over a hovered control with its eased amount
    public static GUIStyle OnStyle;            // rounded accent tint, drawn over the open sidebar entry
    public static Texture2D SwitchTrackOff;
    public static Texture2D SwitchTrackOn;
    public static Texture2D SwitchGlow;
    public static Texture2D SwitchKnob;
    [Tooltip("How quickly highlights and knobs ease, per second.")]
    public static float Smoothing = 16f;

    // ---- motion ----------------------------------------------------------------------------------------------------

    private class Motion
    {
        public float hover;    // 0..1, eased toward hovered
        public bool hovered;
        public float on;       // 0..1, eased toward isOn
        public bool isOn;
        public int seen;       // last frame this control was drawn
    }

    private static readonly Dictionary<int, Motion> motions = new Dictionary<int, Motion>();
    private static readonly List<int> stale = new List<int>();

    // Called by the menu once a frame (unscaled time): everything eases toward where it is going.
    public static void Tick(float dt)
    {
        float k = 1f - Mathf.Exp(-Smoothing * Mathf.Max(0f, dt));
        stale.Clear();
        foreach (KeyValuePair<int, Motion> pair in motions)
        {
            Motion m = pair.Value;
            m.hover = Mathf.Lerp(m.hover, m.hovered ? 1f : 0f, k);
            m.on = Mathf.Lerp(m.on, m.isOn ? 1f : 0f, k);
            if (Time.frameCount - m.seen > 120)
                stale.Add(pair.Key);
        }
        foreach (int key in stale)
            motions.Remove(key);
    }

    // Everything stops mid-ease when the menu closes; next time it opens nothing jumps.
    public static void Reset()
    {
        motions.Clear();
    }

    private static Motion MotionFor(Rect rect)
    {
        int key = Key(rect);
        if (!motions.TryGetValue(key, out Motion m))
        {
            m = new Motion();
            motions[key] = m;
        }
        m.seen = Time.frameCount;
        return m;
    }

    private static int Key(Rect r)
    {
        int key = Mathf.RoundToInt(r.x) * 73856093 ^ Mathf.RoundToInt(r.y) * 19349663 ^ Mathf.RoundToInt(r.width) * 83492791 ^ Mathf.RoundToInt(r.height) * 49979687;
        return key == 0 ? 1 : key;
    }

    private static void Overlay(Rect rect, GUIStyle style, float alpha)
    {
        if (style == null || alpha <= 0.005f)
            return;
        Color previous = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, previous.a * alpha);
        GUI.Box(rect, GUIContent.none, style);
        GUI.color = previous;
    }

    // ---- the GUILayout ones, with the hover note and an eased highlight -------------------------------------------

    public static bool Button(string text, params GUILayoutOption[] options)
    {
        return Note(GUILayout.Button(text, options));
    }

    public static bool Button(string text, GUIStyle style, params GUILayoutOption[] options)
    {
        return Note(GUILayout.Button(text, style ?? GUI.skin.button, options));
    }

    public static bool Button(GUIContent content, GUIStyle style, params GUILayoutOption[] options)
    {
        return Note(GUILayout.Button(content, style ?? GUI.skin.button, options));
    }

    public static bool Toggle(bool value, string text, params GUILayoutOption[] options)
    {
        bool result = GUILayout.Toggle(value, text, options);
        Note(false);
        return result;
    }

    public static bool Toggle(bool value, string text, GUIStyle style, params GUILayoutOption[] options)
    {
        bool result = GUILayout.Toggle(value, text, style ?? GUI.skin.toggle, options);
        Note(false);
        return result;
    }

    // A sidebar entry: its open tint and its hover both ease in and out.
    public static bool NavEntry(bool on, string text, GUIStyle style)
    {
        bool result = GUILayout.Toggle(on, text, style ?? GUI.skin.toggle);
        if (Event.current.type == EventType.Repaint)
        {
            Rect rect = GUILayoutUtility.GetLastRect();
            Motion m = MotionFor(rect);
            m.isOn = on;
            m.hovered = rect.Contains(Event.current.mousePosition);
            if (m.hovered)
                PauseMenu.NoteHover(rect);
            Overlay(rect, OnStyle, m.on);
            Overlay(rect, HighlightStyle, m.hover * (1f - 0.6f * m.on));
        }
        return result;
    }

    // ---- the menu's own -------------------------------------------------------------------------------------------

    // A section title: small accent capitals with a little room around it.
    public static void Heading(string title)
    {
        GUILayout.Space(10f);
        GUILayout.Label(title.ToUpperInvariant(), SectionStyle ?? GUI.skin.label);
        GUILayout.Space(4f);
    }

    // A settings row: the label on the left, whatever you draw next on the right; EndRow closes it with a faint line.
    public static void BeginRow(string label)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, RowLabelStyle ?? GUI.skin.label);
        GUILayout.FlexibleSpace();
    }

    public static void EndRow()
    {
        GUILayout.EndHorizontal();
        Rect rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint)
        {
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.07f * previous.a);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }

    // Label on the left, an on/off switch on the right. The knob slides and the track fades between its colours.
    public static bool SwitchRow(string label, bool value)
    {
        BeginRow(label);
        Rect rect = GUILayoutUtility.GetRect(52f, 28f, SwitchStyle ?? GUIStyle.none, GUILayout.Width(52f), GUILayout.Height(28f));
        if (SwitchTrackOff == null || SwitchTrackOn == null || SwitchKnob == null)
        {
            value = GUI.Toggle(rect, value, "");   // outside the menu: the plain one
            EndRow();
            return value;
        }
        if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            value = !value;
        if (Event.current.type == EventType.Repaint)
        {
            Motion m = MotionFor(rect);
            m.isOn = value;
            m.hovered = rect.Contains(Event.current.mousePosition);
            if (m.hovered)
                PauseMenu.NoteHover(rect);

            Color previous = GUI.color;
            GUI.DrawTexture(rect, SwitchTrackOff);
            GUI.color = new Color(1f, 1f, 1f, previous.a * m.on);
            GUI.DrawTexture(rect, SwitchTrackOn);
            if (SwitchGlow != null)
            {
                GUI.color = new Color(1f, 1f, 1f, previous.a * m.hover * 0.12f);
                GUI.DrawTexture(rect, SwitchGlow);
            }
            float knobX = Mathf.Lerp(rect.x + 4f, rect.xMax - 24f, m.on);
            Color knob = Color.Lerp(new Color(0.75f, 0.8f, 0.86f), Color.white, m.on);
            GUI.color = new Color(knob.r, knob.g, knob.b, previous.a);
            GUI.DrawTexture(new Rect(knobX, rect.y + 4f, 20f, 20f), SwitchKnob);
            GUI.color = previous;
        }
        EndRow();
        return value;
    }

    // Label on the left, a slider and its value on the right.
    public static float SliderRow(string label, float value, float min, float max, string shown)
    {
        BeginRow(label);
        value = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(240f));
        GUILayout.Label(shown, ValueStyle ?? GUI.skin.label, GUILayout.Width(64f));
        EndRow();
        return value;
    }

    // Label on the left, a value with < and > either side on the right. Returns the new index, wrapping round.
    public static int StepperRow(string label, int index, string[] options)
    {
        BeginRow(label);
        if (options == null || options.Length == 0)
        {
            GUILayout.Label("-", ValueStyle ?? GUI.skin.label, GUILayout.Width(190f));
            EndRow();
            return index;
        }
        index = Mathf.Clamp(index, 0, options.Length - 1);
        if (Button("<", SmallButtonStyle, GUILayout.Width(36f)))
            index = (index - 1 + options.Length) % options.Length;
        GUILayout.Label(options[index], ValueStyle ?? GUI.skin.label, GUILayout.Width(190f));
        if (Button(">", SmallButtonStyle, GUILayout.Width(36f)))
            index = (index + 1) % options.Length;
        EndRow();
        return index;
    }

    // Label on the left, a button on the right.
    public static bool ButtonRow(string label, string button)
    {
        BeginRow(label);
        bool pressed = Button(button, SmallButtonStyle);
        EndRow();
        return pressed;
    }

    // A key cap: the key bound to something; click it to change. Listening = waiting for the new key.
    public static bool KeyCap(string text, bool listening)
    {
        GUIStyle style = listening ? (KeyListeningStyle ?? KeyStyle) : KeyStyle;
        return Button(text, style ?? GUI.skin.button);
    }

    // On the repaint pass, the last control drawn: is the mouse over it (tell the menu), and ease its highlight.
    private static bool Note(bool pressed)
    {
        if (Event.current.type == EventType.Repaint)
        {
            Rect rect = GUILayoutUtility.GetLastRect();
            Motion m = MotionFor(rect);
            m.hovered = rect.Contains(Event.current.mousePosition);
            if (m.hovered)
                PauseMenu.NoteHover(rect);
            Overlay(rect, HighlightStyle, m.hover);
        }
        return pressed;
    }
}
