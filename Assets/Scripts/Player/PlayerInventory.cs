using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// Hotbar inventory from the GDD: keys, puzzle pieces and weapons stack into numbered slots shown at the bottom of the
// screen, selected with 1-5 or the mouse wheel. The last Weapon Slots (4 and 5) hold weapons only, and weapons go nowhere else;
// the rest hold everything else. Collectibles (pearls, shells) don't take a slot: they're just counted.
// Puzzles check Has() and spend with Remove(); they don't need the item selected.
public class PlayerInventory : MonoBehaviour
{
    [System.Serializable]
    public class Slot
    {
        public ItemDefinition item;
        public int count;
        public bool IsEmpty => item == null || count <= 0;
    }

    [Header("Slots")]
    [SerializeField, Range(1, 9)] private int slotCount = 5;
    [Tooltip("How many of the slots, at the end of the bar, are for weapons only (the dagger, the trident). 0 = any slot takes anything.")]
    [SerializeField, Range(0, 4)] private int weaponSlots = 2;
    [Tooltip("Number keys 1..slotCount and the mouse wheel change the selected slot.")]
    [SerializeField] private bool selectWithInput = true;

    [Header("Events")]
    public UnityEvent onChanged = new UnityEvent();
    public UnityEvent<int> onSelectionChanged = new UnityEvent<int>();
    public UnityEvent<ItemDefinition, int> onCollectibleChanged = new UnityEvent<ItemDefinition, int>();

    private Slot[] slots;
    private readonly Dictionary<ItemDefinition, int> collectibles = new Dictionary<ItemDefinition, int>();

    public int SlotCount => slotCount;
    public int WeaponSlots => Mathf.Clamp(weaponSlots, 0, slotCount - 1);

    // A weapon slot (one of the last Weapon Slots).
    public bool IsWeaponSlot(int index) => index >= slotCount - WeaponSlots;

    // Whether this slot takes this item: weapons only in weapon slots, everything else only in the others.
    public bool Fits(int index, ItemDefinition item) =>
        WeaponSlots == 0 || item == null || IsWeaponSlot(index) == (item.Kind == ItemDefinition.Category.Weapon);
    // While something else owns the number keys and the wheel (a pickup being inspected), 1-5 and scrolling do nothing.
    public bool InputBlocked { get; set; }

    public int TotalCollectibles
    {
        get
        {
            int total = 0;
            foreach (KeyValuePair<ItemDefinition, int> pair in collectibles)
                total += pair.Value;
            return total;
        }
    }
    public int SelectedIndex { get; private set; }
    public IEnumerable<KeyValuePair<ItemDefinition, int>> Collectibles => collectibles;

    // Checkpoint Save: exactly these slots, these collectibles and this selection.
    public void RestoreState(IList<KeyValuePair<ItemDefinition, int>> saved, IDictionary<ItemDefinition, int> savedCollectibles, int selected)
    {
        EnsureSlots();
        for (int i = 0; i < slots.Length; i++)
        {
            bool has = saved != null && i < saved.Count && saved[i].Key != null && saved[i].Value > 0;
            slots[i].item = has ? saved[i].Key : null;
            slots[i].count = has ? saved[i].Value : 0;
        }
        collectibles.Clear();
        if (savedCollectibles != null)
            foreach (KeyValuePair<ItemDefinition, int> pair in savedCollectibles)
            {
                collectibles[pair.Key] = pair.Value;
                onCollectibleChanged.Invoke(pair.Key, pair.Value);
            }
        onChanged.Invoke();
        Select(Mathf.Clamp(selected, 0, slots.Length - 1));
    }
    public ItemDefinition SelectedItem => GetSlot(SelectedIndex).item;

    public Slot GetSlot(int index)
    {
        EnsureSlots();
        return slots[index];
    }

    private void Awake()
    {
        EnsureSlots();
    }

    // Other components may ask before our Awake has run (script order isn't guaranteed).
    private void EnsureSlots()
    {
        if (slots != null)
            return;

        slots = new Slot[slotCount];
        for (int i = 0; i < slots.Length; i++)
            slots[i] = new Slot();
    }

    private void Update()
    {
        if (InputBlocked || PauseMenu.IsOpen)
            return;
        if (!selectWithInput)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            for (int i = 0; i < slotCount && i < 9; i++)
            {
                if (keyboard[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                {
                    if (!GetSlot(i).IsEmpty)   // an empty slot (the trident's, before you have it) has nothing to hold
                        Select(i);
                    break;
                }
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll > 0.01f)
                SelectNextFilled(-1);
            else if (scroll < -0.01f)
                SelectNextFilled(1);
        }
    }

    // The wheel: on to the next slot that holds something, that way round, skipping the empty ones. Nothing held
    // anywhere else = it stays put.
    private void SelectNextFilled(int step)
    {
        for (int k = 1; k < slotCount; k++)
        {
            int index = ((SelectedIndex + step * k) % slotCount + slotCount) % slotCount;
            if (!GetSlot(index).IsEmpty)
            {
                Select(index);
                return;
            }
        }
    }

    public void Select(int index)
    {
        index = Mathf.Clamp(index, 0, slotCount - 1);
        if (index == SelectedIndex)
            return;

        SelectedIndex = index;
        onSelectionChanged.Invoke(index);
    }

    private static bool IsCollectible(ItemDefinition item) => item != null && item.Kind == ItemDefinition.Category.Collectible;

    public int Count(ItemDefinition item)
    {
        if (IsCollectible(item))
            return collectibles.TryGetValue(item, out int collected) ? collected : 0;

        EnsureSlots();
        int total = 0;
        foreach (Slot slot in slots)
        {
            if (slot.item == item)
                total += slot.count;
        }
        return total;
    }

    public bool Has(ItemDefinition item, int amount = 1) => item != null && Count(item) >= amount;

    // e.g. "any weapon": the dagger or the trident unlocks slashing.
    public bool HasCategory(ItemDefinition.Category category)
    {
        if (category == ItemDefinition.Category.Collectible)
            return TotalCollectibles > 0;

        EnsureSlots();
        foreach (Slot slot in slots)
        {
            if (!slot.IsEmpty && slot.item.Kind == category)
                return true;
        }
        return false;
    }

    // Returns how many were actually stored; the rest didn't fit.
    // How many of `amount` Add() would take right now, without taking them (a pickup checks this before it animates).
    public int SpaceFor(ItemDefinition item, int amount = 1)
    {
        if (item == null || amount <= 0)
            return 0;
        if (IsCollectible(item))
            return amount;

        EnsureSlots();
        int space = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            if (!Fits(i, item))
                continue;
            if (slot.item == item)
                space += Mathf.Max(0, item.MaxStack - slot.count);
            else if (slot.IsEmpty)
                space += item.MaxStack;
        }
        return Mathf.Min(amount, space);
    }

    public int Add(ItemDefinition item, int amount = 1)
    {
        if (item == null || amount <= 0)
            return 0;

        if (IsCollectible(item))
        {
            collectibles[item] = Count(item) + amount;
            onCollectibleChanged.Invoke(item, collectibles[item]);
            onChanged.Invoke();
            return amount;
        }

        EnsureSlots();
        int remaining = amount;
        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            if (remaining == 0)
                break;
            if (!Fits(i, item) || slot.item != item || slot.count >= item.MaxStack)
                continue;

            int add = Mathf.Min(remaining, item.MaxStack - slot.count);
            slot.count += add;
            remaining -= add;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            if (remaining == 0)
                break;
            if (!slot.IsEmpty || !Fits(i, item))
                continue;

            int add = Mathf.Min(remaining, item.MaxStack);
            slot.item = item;
            slot.count = add;
            remaining -= add;
        }

        if (remaining != amount)
            onChanged.Invoke();
        return amount - remaining;
    }

    public bool Remove(ItemDefinition item, int amount = 1)
    {
        if (!Has(item, amount))
            return false;

        if (IsCollectible(item))
        {
            collectibles[item] -= amount;
            onCollectibleChanged.Invoke(item, collectibles[item]);
            onChanged.Invoke();
            return true;
        }

        int remaining = amount;
        for (int i = slots.Length - 1; i >= 0 && remaining > 0; i--)
        {
            Slot slot = slots[i];
            if (slot.item != item)
                continue;

            int take = Mathf.Min(remaining, slot.count);
            slot.count -= take;
            remaining -= take;
            if (slot.count == 0)
                slot.item = null;
        }

        onChanged.Invoke();
        return true;
    }
}
