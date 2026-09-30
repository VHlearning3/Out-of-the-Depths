using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Plays the hand-drawn slash frames (Slash Sprites) over the view when the dagger slashes: the sweep sheet for a
// left-right slash, running across the screen from the left edge to the right one, the chop sheet for an up-down one,
// coming down in one column; the arm always rests on the bottom edge. Drawn on its own screen canvas just under the HUD
// (the hotbar and bars stay on top). While the dagger is the weapon in hand the 3D hand and dagger are hidden the whole
// time (the drawings are the dagger now); with any other weapon (the trident) they show and the slash goes to the hand
// animator that was there before, unchanged. The slash ribbon (Slash Trail) follows the drawn blade's tip through the
// frames (BladePath). Slash Attack sets this up by itself when Resources/SlashSprites exists.
public class SpriteSlashAnimator : MonoBehaviour, IDirectionalHandAnimator
{
    private SlashSprites sprites;
    private IHandAnimator fallback;
    private HeldWeapon held;
    private PlayerInventory inventory;
    private Canvas canvas;
    private Image image;
    private Image ghost;
    private Coroutine playing;
    private readonly List<Renderer> hidden = new List<Renderer>();
    private readonly List<Renderer> scratch = new List<Renderer>();

    // The last drawn slash, for the ribbon: where the blade's tip is in each frame on screen, and when it started.
    private Vector2[] tips = new Vector2[0];
    private float startedAt;

    // True when the last slash was the drawn one (false when it went to the old hand animator).
    public bool DrewLastSlash { get; private set; }

    public void Setup(SlashSprites set, IHandAnimator previous, HeldWeapon weapon, PlayerInventory owner)
    {
        sprites = set;
        fallback = previous;
        held = weapon;
        inventory = owner;
    }

    public void PlaySlash() => PlaySlash(SlashAttack.Direction.LeftRight);

    public void PlaySlash(SlashAttack.Direction direction)
    {
        bool chop = direction == SlashAttack.Direction.UpDown;
        Sprite[] frames = sprites == null ? null : chop ? sprites.down : sprites.side;
        if (frames == null || frames.Length == 0 || !HoldingDrawnWeapon())
        {
            DrewLastSlash = false;
            if (fallback is IDirectionalHandAnimator directional)
                directional.PlaySlash(direction);
            else
                fallback?.PlaySlash();
            return;
        }
        if (playing != null)
            StopCoroutine(playing);
        DrewLastSlash = true;
        playing = StartCoroutine(Play(frames, chop));
    }

    // The drawn blade tip's path so far in the last drawn slash, in screen pixels (origin bottom left), smoothed
    // between the frames and moving on with time, for the slash ribbon. `along` is how far through the slash each point
    // is (0..1). False when there is no drawn slash to follow.
    public bool BladePath(List<Vector2> points, List<float> along)
    {
        points.Clear();
        along.Clear();
        if (!DrewLastSlash || tips.Length < 2)
            return false;
        float fps = Mathf.Max(1f, sprites.framesPerSecond);
        float last = tips.Length - 1;
        float reached = Mathf.Clamp((Time.time - startedAt) * fps, 0f, last);
        const float step = 0.2f;   // five points between two frames
        for (float f = 0f; ; f += step)
        {
            float at = Mathf.Min(f, reached);
            points.Add(TipAt(at));
            along.Add(at / last);
            if (at >= reached)
                break;
        }
        return points.Count >= 2;
    }

    // The tip at frame position `f` (fractional between frames), on a smooth curve through the frames' tips.
    private Vector2 TipAt(float f)
    {
        int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, tips.Length - 2);
        float t = f - i;
        Vector2 p0 = tips[Mathf.Max(i - 1, 0)], p1 = tips[i], p2 = tips[i + 1], p3 = tips[Mathf.Min(i + 2, tips.Length - 1)];
        return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * (t * t) + (3f * p1 - p0 - 3f * p2 + p3) * (t * t * t));
    }

    // The drawings are of the dagger: the weapon in hand has to be that item.
    private bool HoldingDrawnWeapon()
    {
        if (sprites == null)
            return false;
        ItemDefinition item = held != null ? held.Current : null;
        if (item == null && inventory != null && inventory.SelectedItem != null && inventory.SelectedItem.Kind == ItemDefinition.Category.Weapon)
            item = inventory.SelectedItem;
        return item != null && (string.IsNullOrEmpty(sprites.weaponItem) || item.name == sprites.weaponItem);
    }

    // The 3D hand and dagger stay out of sight while the dagger is in hand (checked every frame: the held model is
    // rebuilt when the weapon changes), and come back for any other weapon.
    private void LateUpdate()
    {
        if (HoldingDrawnWeapon())
            HideWeapon();
        else
            ShowWeapon();
    }

    private IEnumerator Play(Sprite[] frames, bool chop)
    {
        EnsureCanvas();
        HideWeapon();
        float lowest = float.MaxValue;   // the bottom of the drawing on the sheet: set on the screen's bottom edge
        foreach (Sprite frame in frames)
            if (frame != null)
                lowest = Mathf.Min(lowest, frame.textureRect.y);

        // Where the blade's tip is in every frame, for the ribbon.
        var list = new List<Vector2>();
        Vector2[] uv = chop ? sprites.downTips : sprites.sideTips;
        for (int i = 0; i < frames.Length; i++)
            if (frames[i] != null)
            {
                Rect r = FrameRect(frames, i, chop, lowest);
                Vector2 at = uv != null && i < uv.Length ? uv[i] : new Vector2(0.5f, 1f);
                list.Add(r.position + Vector2.Scale(r.size, at));
            }
        tips = list.ToArray();
        startedAt = Time.time;

        // When each frame ends: the whole slash takes frames / Frames Per Second, the first and last held longer.
        int n = frames.Length;
        float total = n / Mathf.Max(1f, sprites.framesPerSecond);
        var ends = new float[n];
        float weights = 0f;
        for (int i = 0; i < n; i++)
            weights += Weight(i, n);
        float sum = 0f;
        for (int i = 0; i < n; i++)
        {
            sum += Weight(i, n);
            ends[i] = total * sum / weights;
        }

        bool chained = image.enabled;   // a slash straight after the last one: no rising in again
        float slideIn = chained ? 0f : Mathf.Max(0f, sprites.slideIn);
        float slideOut = Mathf.Max(0.0001f, sprites.slideOut);
        int shown = -1, before = -1;
        float changedAt = 0f;
        image.enabled = true;
        for (float t = 0f; t < total + slideOut; t += Time.deltaTime)
        {
            int i = 0;
            while (i < n - 1 && t >= ends[i])
                i++;
            if (i != shown)
            {
                before = shown;
                shown = i;
                changedAt = t;
            }

            // Rising in at the start, sinking out and fading after the last frame.
            float drop = 0f, alpha = 1f;
            if (t < slideIn)
                drop = 1f - Mathf.SmoothStep(0f, 1f, t / slideIn);
            if (t > total)
            {
                float u = (t - total) / slideOut;
                drop = u * u;
                alpha = 1f - u;
            }
            float lower = drop * Screen.height * 0.35f;
            Show(image, frames, shown, chop, lowest, lower, alpha);

            // The frame before, fading out over this one's first moment.
            float fade = before >= 0 ? 1f - (t - changedAt) / Mathf.Max(0.0001f, ends[shown] - (shown > 0 ? ends[shown - 1] : 0f)) : 0f;
            if (fade > 0f && sprites.afterimage > 0f)
                Show(ghost, frames, before, chop, lowest, lower, alpha * fade * sprites.afterimage);
            else
                ghost.enabled = false;
            yield return null;
        }
        image.enabled = false;
        ghost.enabled = false;
        playing = null;
    }

    private float Weight(int i, int n) => i == 0 ? sprites.holdFirst : i == n - 1 ? sprites.holdLast : 1f;

    private void Show(Image target, Sprite[] frames, int index, bool chop, float lowest, float lower, float alpha)
    {
        if (index < 0 || frames[index] == null)
        {
            target.enabled = false;
            return;
        }
        Rect r = FrameRect(frames, index, chop, lowest);
        RectTransform rt = target.rectTransform;
        rt.sizeDelta = r.size;
        rt.anchoredPosition = r.position - new Vector2(0f, lower);
        target.sprite = frames[index];
        Color c = sprites.tint;
        c.a *= Mathf.Clamp01(alpha);
        target.color = c;
        target.enabled = true;
    }

    // Frame i on screen (pixels, origin bottom left), sized by its sheet's height setting, the lowest drawing on the
    // sheet on the bottom edge (the arms are cut flat there). The chop: every frame's middle at Down Centre X. The sweep:
    // the first frame's left side on the left edge (its blade is cut there), the last frame's right side on the right
    // edge (its hand is cut there), the frames between evenly on the way by their middles, so the hand swings across
    // the whole view.
    private Rect FrameRect(Sprite[] frames, int index, bool chop, float lowest)
    {
        Sprite frame = frames[index];
        Rect r = frame.textureRect;
        float scale = Screen.height * (chop ? sprites.downHeight : sprites.sideHeight) / frame.texture.height;
        float width = r.width * scale;
        float middle;
        if (chop)
            middle = Screen.width * sprites.downCentreX;
        else
        {
            float margin = Screen.width * sprites.sideMargin;
            Sprite first = frames[0] != null ? frames[0] : frame;
            Sprite last = frames[frames.Length - 1] != null ? frames[frames.Length - 1] : frame;
            float from = margin + first.textureRect.width * scale * 0.5f;
            float to = Screen.width - margin - last.textureRect.width * scale * 0.5f;
            middle = Mathf.Lerp(from, to, frames.Length > 1 ? index / (frames.Length - 1f) : 0.5f);
        }
        return new Rect(middle - width * 0.5f, (r.y - lowest) * scale, width, r.height * scale);
    }

    private void EnsureCanvas()
    {
        if (canvas != null)
            return;
        var go = new GameObject("SlashSprites");
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -20;   // under the HUD
        ghost = MakeImage("Afterimage", go.transform);   // first: drawn behind
        image = MakeImage("Frame", go.transform);
    }

    private static Image MakeImage(string name, Transform parent)
    {
        var holder = new GameObject(name, typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        Image made = holder.AddComponent<Image>();
        made.raycastTarget = false;
        made.enabled = false;
        RectTransform rt = made.rectTransform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        return made;
    }

    // Every shown renderer under the Hands (the hand, the held dagger) switched off, remembered to switch back on.
    private void HideWeapon()
    {
        GetComponentsInChildren(scratch);
        foreach (Renderer r in scratch)
            if (r.enabled)
            {
                r.enabled = false;
                hidden.Add(r);
            }
    }

    private void ShowWeapon()
    {
        if (hidden.Count == 0)
            return;
        foreach (Renderer r in hidden)
            if (r != null)
                r.enabled = true;
        hidden.Clear();
    }

    private void OnDisable()
    {
        if (playing != null)
            StopCoroutine(playing);
        playing = null;
        if (image != null)
            image.enabled = false;
        if (ghost != null)
            ghost.enabled = false;
        ShowWeapon();
    }
}
