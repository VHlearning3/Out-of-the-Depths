using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Keeps work done by hand inside a built level (SHIP, the Arena) through a rebuild. A rebuild throws the old level away
// and builds it again, so without this a teammate's pressure plates, boxes, or a script added to one of the builder's
// doors would go with it. Before the old level is removed, Lift() takes out:
//  - whole objects that are not the builder's: prefab instances of the hand-made prefabs (Box, PressurePlate), boxes
//    tagged PushableObject, plain objects (not prefab instances) carrying a hand-made script, and anything whose name
//    starts with KEEP (name a group KEEP_Whatever and everything in it is kept);
//  - scripts added by hand to the builder's objects (the hand-made script types below, e.g. a Secret Door Manager put on
//    Door_Closet), saved with their settings.
// After the build, PutBack() puts the objects back where they were (under the group with the same name) and the
// scripts back on the object with the same path, and points every link they had to the builder's objects (a door) at
// the new ones, found by the same path.
internal static class HandMadeKeeper
{
    // Prefabs that are only ever placed by hand.
    private static readonly string[] HandMadePrefabs = { "Box", "PressurePlate" };
    // Scripts that are only ever added by hand (matched by class name).
    private static readonly string[] HandMadeScripts = { "PressurePlates", "SecretDoorManager", "MashE", "BoxPush" };

    internal class Link
    {
        public UnityEngine.Object owner;     // the component holding the reference (alive: a kept object's, or re-added)
        public string property;
        public string targetPath;            // the builder's object it pointed at, by path under the level root
        public Type targetType;              // null = a GameObject
        public UnityEngine.Object direct;    // a kept object's part, which lives through the rebuild: linked as is
    }

    internal class KeptObject
    {
        public GameObject go;
        public string parentPath;
    }

    internal class KeptScript
    {
        public string hostPath;
        public Type type;
        public string json;
        public List<Link> links = new List<Link>();
    }

    internal class Kept
    {
        internal readonly List<KeptObject> objects = new List<KeptObject>();
        internal readonly List<KeptScript> scripts = new List<KeptScript>();
        internal readonly List<Link> links = new List<Link>();
        internal int Count => objects.Count + scripts.Count;
    }

    // Takes the hand-made work out of `root` (the old level, about to be removed). Null root = nothing to keep.
    internal static Kept Lift(Transform root)
    {
        var kept = new Kept();
        if (root == null)
            return kept;

        // Whole objects, topmost only.
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root || !IsHandMadeObject(t.gameObject) || Inside(t, kept))
                continue;
            kept.objects.Add(new KeptObject { go = t.gameObject, parentPath = PathOf(t.parent, root) });
        }

        // Scripts added by hand to the builder's objects.
        foreach (MonoBehaviour script in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (script == null || Array.IndexOf(HandMadeScripts, script.GetType().Name) < 0 || Inside(script.transform, kept))
                continue;
            var s = new KeptScript { hostPath = PathOf(script.transform, root), type = script.GetType(), json = EditorJsonUtility.ToJson(script) };
            s.links = LinksOutOf(script, root, kept, true);
            kept.scripts.Add(s);
        }

        // Links from the kept objects to the builder's objects.
        foreach (KeptObject o in kept.objects)
            foreach (Component c in o.go.GetComponentsInChildren<Component>(true))
                if (c != null && !(c is Transform))
                    kept.links.AddRange(LinksOutOf(c, root, kept, false));

        // Out of the old level, so they survive it being removed.
        foreach (KeptObject o in kept.objects)
            o.go.transform.SetParent(null, true);
        if (kept.Count > 0)
            Debug.Log($"Kept {kept.objects.Count} hand-made object(s) and {kept.scripts.Count} hand-added script(s) through the rebuild.");
        return kept;
    }

    // Puts it all back into `root` (the new level).
    internal static void PutBack(Kept kept, Transform root)
    {
        if (kept == null || root == null)
            return;
        foreach (KeptObject o in kept.objects)
        {
            if (o.go == null)
                continue;
            Transform parent = string.IsNullOrEmpty(o.parentPath) ? root : root.Find(o.parentPath);
            o.go.transform.SetParent(parent != null ? parent : root, true);
        }
        foreach (KeptScript s in kept.scripts)
        {
            Transform host = string.IsNullOrEmpty(s.hostPath) ? root : root.Find(s.hostPath);
            if (host == null)
            {
                Debug.LogWarning($"Rebuild: {s.type.Name} was on {s.hostPath}, which the new level does not have any more; it was not put back.");
                continue;
            }
            Component script = host.GetComponent(s.type);
            if (script == null)
                script = host.gameObject.AddComponent(s.type);
            EditorJsonUtility.FromJsonOverwrite(s.json, script);
            foreach (Link link in s.links)
                link.owner = script;
            Relink(s.links, root);
            EditorUtility.SetDirty(script);
        }
        Relink(kept.links, root);
    }

    private static bool IsHandMadeObject(GameObject go)
    {
        if (go.name.StartsWith("KEEP", StringComparison.OrdinalIgnoreCase))
            return true;
        if (go.CompareTag("PushableObject"))
            return true;
        // A plain object (not one of the builder's prefabs) carrying a hand-made script: made by hand as a whole.
        if (!PrefabUtility.IsPartOfPrefabInstance(go))
            foreach (MonoBehaviour script in go.GetComponents<MonoBehaviour>())
                if (script != null && Array.IndexOf(HandMadeScripts, script.GetType().Name) >= 0)
                    return true;
        if (!PrefabUtility.IsAnyPrefabInstanceRoot(go))
            return false;
        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(go);
        return source != null && Array.IndexOf(HandMadePrefabs, source.name) >= 0;
    }

    private static bool Inside(Transform t, Kept kept)
    {
        foreach (KeptObject o in kept.objects)
            if (o.go != null && t.IsChildOf(o.go.transform))
                return true;
        return false;
    }

    // Every reference `owner` holds to something in the level: the builder's objects (found again by path), and with
    // `toKept` also the kept objects (a script re-added after the build, e.g. a Secret Door Manager's pressure plates).
    private static List<Link> LinksOutOf(Component owner, Transform root, Kept kept, bool toKept)
    {
        var links = new List<Link>();
        var so = new SerializedObject(owner);
        SerializedProperty p = so.GetIterator();
        while (p.Next(true))
        {
            if (p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue == null)
                continue;
            UnityEngine.Object value = p.objectReferenceValue;
            Transform target = value is Component c ? c.transform : value is GameObject g ? g.transform : null;
            if (target == null || !target.IsChildOf(root))
                continue;
            if (Inside(target, kept))
            {
                if (toKept)
                    links.Add(new Link { owner = owner, property = p.propertyPath, direct = value });
                continue;
            }
            links.Add(new Link { owner = owner, property = p.propertyPath, targetPath = PathOf(target, root), targetType = value is Component ? value.GetType() : null });
        }
        return links;
    }

    private static void Relink(List<Link> links, Transform root)
    {
        foreach (Link link in links)
        {
            if (link.owner == null)
                continue;
            UnityEngine.Object value = link.direct;   // (null again if the kept object was deleted meanwhile)
            if ((object)link.direct == null)
            {
                Transform target = string.IsNullOrEmpty(link.targetPath) ? root : root.Find(link.targetPath);
                value = target == null ? null : link.targetType == null ? (UnityEngine.Object)target.gameObject : target.GetComponent(link.targetType);
            }
            if (value == null)
            {
                Debug.LogWarning($"Rebuild: {link.owner.name} pointed at {link.targetPath}, which the new level does not have any more.");
                continue;
            }
            var so = new SerializedObject(link.owner);
            SerializedProperty p = so.FindProperty(link.property);
            if (p == null)
                continue;
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // The path from `root` down to `t` ("Room_7_BoxRoom/Door_Closet"), for Transform.Find; empty for the root itself.
    private static string PathOf(Transform t, Transform root)
    {
        var names = new List<string>();
        for (; t != null && t != root; t = t.parent)
            names.Add(t.name);
        names.Reverse();
        return string.Join("/", names);
    }
}
