using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Plays the hand-drawn push (Push Sprites) over the view while the player pushes a box (Box Push → Pushing): the frames
// in order, looping, standing on the bottom edge of the screen, sliding up into view as a push starts and down out of
// it as it ends. Drawn on its own screen canvas just under the HUD. Does nothing while Push Sprites has no frames.
// Box Push adds it at start when Resources/PushSprites exists.
public class SpritePushAnimator : MonoBehaviour
{
    private PushSprites sprites;
    private BoxPush push;
    private Canvas canvas;
    private Image image;
    private float shown;      // 0 = out of view, 1 = fully in
    private float playTime;   // seconds into the push, for the frame
    private Transform hands;
    private readonly List<Renderer> hidden = new List<Renderer>();
    private readonly List<Renderer> scratch = new List<Renderer>();

    public void Setup(PushSprites set, BoxPush pusher)
    {
        sprites = set;
        push = pusher;
    }

    private void Update()
    {
        if (sprites == null || push == null || sprites.frames == null || sprites.frames.Length == 0)
            return;   // no push drawing yet

        bool pushing = push.Pushing;
        float fade = Mathf.Max(0.0001f, sprites.fadeSeconds);
        shown = Mathf.MoveTowards(shown, pushing ? 1f : 0f, Time.deltaTime / fade);
        if (shown <= 0f)
        {
            if (image != null)
                image.enabled = false;
            ShowHands();
            playTime = 0f;
            return;
        }
        if (pushing)
            playTime += Time.deltaTime;

        EnsureCanvas();
        int n = sprites.frames.Length;
        int i = Mathf.FloorToInt(playTime * Mathf.Max(0.01f, sprites.framesPerSecond));
        i = sprites.loop ? i % n : Mathf.Min(i, n - 1);
        Sprite frame = sprites.frames[i];
        if (frame == null)
            return;

        // Every frame sized the same way: the tallest one Screen Height tall. Standing on the bottom edge, its middle at
        // Centre X, lowered out of view while it slides in or out.
        float tallest = 1f;
        foreach (Sprite f in sprites.frames)
            if (f != null)
                tallest = Mathf.Max(tallest, f.rect.height);
        float scale = Screen.height * sprites.screenHeight / tallest;
        Vector2 size = frame.rect.size * scale;
        float ease = Mathf.SmoothStep(0f, 1f, shown);
        RectTransform rt = image.rectTransform;
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(Screen.width * sprites.centreX - size.x * 0.5f, -(1f - ease) * size.y * 0.6f);
        image.sprite = frame;
        Color c = sprites.tint;
        c.a *= ease;
        image.color = c;
        image.enabled = true;

        if (sprites.hideHands)
            HideHands();
    }

    private void EnsureCanvas()
    {
        if (canvas != null)
            return;
        var go = new GameObject("PushSprites");
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -20;   // under the HUD
        var holder = new GameObject("Frame", typeof(RectTransform));
        holder.transform.SetParent(go.transform, false);
        image = holder.AddComponent<Image>();
        image.raycastTarget = false;
        image.enabled = false;
        RectTransform rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
    }

    // The 3D hands (and weapon) under the camera, out of sight while the drawing shows.
    private void HideHands()
    {
        if (hands == null)
        {
            HeldWeapon held = GetComponentInChildren<HeldWeapon>(true);
            if (held == null)
                return;
            hands = held.transform;
        }
        hands.GetComponentsInChildren(scratch);
        foreach (Renderer r in scratch)
            if (r.enabled)
            {
                r.enabled = false;
                hidden.Add(r);
            }
    }

    private void ShowHands()
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
        if (image != null)
            image.enabled = false;
        shown = 0f;
        ShowHands();
    }
}
