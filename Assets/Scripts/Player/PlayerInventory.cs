using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// Hotbar inventory from the GDD: collected items stack into numbered slots shown at the bottom of the screen and are
// selected with 1-5 or the mouse wheel. Puzzles check Has() and spend with Remove(); they don't need the item selected.
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
    [Tooltip("Number keys 1..slotCount and the mouse wheel change the selected slot.")]
    [SerializeField] private bool selectWithInput = true;

    [Header("Events")]
    public UnityEvent onChanged = new UnityEvent();
    public UnityEvent<int> onSelectionChanged = new UnityEvent<int>();

    private Slot[] slots;

    public int SlotCount => slotCount;
    public int SelectedIndex { get; private set; }
    public ItemDefinition SelectedItem => slots[SelectedIndex].item;
    public Slot GetSlot(int index) => slots[index];

    private void Awake()
    {
        slots = new Slot[slotCount];
        for (int i = 0; i < slots.Length; i++)
            slots[i] = new Slot();
    }

    private void Update()
    {
        if (!selectWithInput)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            for (int i = 0; i < slotCount && i < 9; i++)
            {
                if (keyboard[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                {
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
                Select((SelectedIndex - 1 + slotCount) % slotCount);
            else if (scroll < -0.01f)
                Select((SelectedIndex + 1) % slotCount);
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

    public int Count(ItemDefinition item)
    {
        int total = 0;
        foreach (Slot slot in slots)
        {
            if (slot.item == item)
                total += slot.count;
        }
        return total;
    }

    public bool Has(ItemDefinition item, int amount = 1) => item != null && Count(item) >= amount;

    // Returns how many were actually stored; the rest didn't fit.
    public int Add(ItemDefinition item, int amount = 1)
    {
        if (item == null || amount <= 0)
            return 0;

        int remaining = amount;
        foreach (Slot slot in slots)
        {
            if (remaining == 0)
                break;
            if (slot.item != item || slot.count >= item.MaxStack)
                continue;

            int add = Mathf.Min(remaining, item.MaxStack - slot.count);
            slot.count += add;
            remaining -= add;
        }

        foreach (Slot slot in slots)
        {
            if (remaining == 0)
                break;
            if (!slot.IsEmpty)
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
