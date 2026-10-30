using System;
using System.Collections.Generic;

namespace RobaColores.Game.Core
{
    public enum SlotState { Pending, Selected, Confirmed }

    public sealed class PlayerSlot
    {
        public int Number;                 // 1..N, para mostrar "Jugador N"
        public int CharacterId = -1;       // indice del personaje en el catalogo, -1 = ninguno
        public SlotState State = SlotState.Pending;
    }

    // Estado de la seleccion de personajes antes de empezar: un hueco por jugador, sin depender de Unity.
    // La repeticion de personajes es una regla configurable (AllowRepeats), no una restriccion fija.
    public sealed class CharacterSelection
    {
        readonly List<PlayerSlot> slots;
        readonly int characterCount;

        public bool AllowRepeats { get; set; }
        public int SlotCount { get { return slots.Count; } }
        public int CharacterCount { get { return characterCount; } }

        public CharacterSelection(int slotCount, int characterCount, bool allowRepeats)
        {
            if (slotCount < 2 || slotCount > 6 || characterCount <= 0)
                throw new ArgumentException("La selección requiere de dos a seis jugadores y al menos un personaje.");
            this.characterCount = characterCount;
            AllowRepeats = allowRepeats;
            slots = new List<PlayerSlot>(slotCount);
            for (int i = 0; i < slotCount; i++) slots.Add(new PlayerSlot { Number = i + 1 });
        }

        public PlayerSlot Slot(int index) { return slots[index]; }

        public void SetSlotCount(int count)
        {
            if (count < 2 || count > 6) throw new ArgumentOutOfRangeException(nameof(count), "La partida admite de dos a seis jugadores.");
            while (slots.Count > count) slots.RemoveAt(slots.Count - 1);
            while (slots.Count < count) slots.Add(new PlayerSlot { Number = slots.Count + 1 });
        }

        // Indice del otro hueco que tiene ese personaje (elegido o confirmado), o -1.
        public int OwnerOf(int character, int exceptSlot)
        {
            for (int i = 0; i < slots.Count; i++)
                if (i != exceptSlot && slots[i].CharacterId == character) return i;
            return -1;
        }

        public bool IsAvailable(int slot, int character)
        {
            if (character < 0 || character >= characterCount) return false;
            return AllowRepeats || OwnerOf(character, slot) < 0;
        }

        // Elige un personaje para el hueco (queda "Seleccionado"). No se puede cambiar un hueco ya confirmado.
        public bool Select(int slot, int character)
        {
            var s = slots[slot];
            if (s.State == SlotState.Confirmed || !IsAvailable(slot, character)) return false;
            s.CharacterId = character;
            s.State = SlotState.Selected;
            return true;
        }

        // Pasa al siguiente (dir > 0) o anterior (dir < 0) personaje disponible. Desde "Pendiente" empieza por un extremo.
        public bool Cycle(int slot, int dir)
        {
            var s = slots[slot];
            if (s.State == SlotState.Confirmed) return false;
            int step = dir >= 0 ? 1 : -1;
            int start = s.CharacterId < 0 ? (step > 0 ? -1 : characterCount) : s.CharacterId;
            for (int i = 1; i <= characterCount; i++)
            {
                int candidate = ((start + step * i) % characterCount + characterCount) % characterCount;
                if (candidate == s.CharacterId && s.State != SlotState.Pending && i == characterCount) return false;
                if (IsAvailable(slot, candidate)) return Select(slot, candidate);
            }
            return false;
        }

        public bool Confirm(int slot)
        {
            var s = slots[slot];
            if (s.State != SlotState.Selected || !IsAvailable(slot, s.CharacterId)) return false;
            s.State = SlotState.Confirmed;
            return true;
        }

        public void Unconfirm(int slot)
        {
            if (slots[slot].State == SlotState.Confirmed) slots[slot].State = SlotState.Selected;
        }

        public void Clear(int slot)
        {
            slots[slot].CharacterId = -1;
            slots[slot].State = SlotState.Pending;
        }

        public bool AllConfirmed
        {
            get { foreach (var s in slots) if (s.State != SlotState.Confirmed) return false; return true; }
        }

        // Indices de los huecos que aun no estan confirmados.
        public List<int> Missing()
        {
            var res = new List<int>();
            for (int i = 0; i < slots.Count; i++) if (slots[i].State != SlotState.Confirmed) res.Add(i);
            return res;
        }

        // Lista de problemas que impiden empezar (vacia = se puede empezar).
        public List<string> Validate()
        {
            var problems = new List<string>();
            foreach (var m in Missing()) problems.Add("Falta confirmar al jugador " + slots[m].Number + ".");
            if (!AllowRepeats)
                for (int i = 0; i < slots.Count; i++)
                    for (int j = i + 1; j < slots.Count; j++)
                        if (slots[i].CharacterId >= 0 && slots[i].CharacterId == slots[j].CharacterId)
                            problems.Add("Los jugadores " + slots[i].Number + " y " + slots[j].Number + " tienen el mismo personaje.");
            return problems;
        }

        public bool CanStart { get { return Validate().Count == 0; } }

        public int[] ConfirmedCharacters()
        {
            if (!CanStart) throw new InvalidOperationException("La seleccion no es valida: " + string.Join(" ", Validate().ToArray()));
            var res = new int[slots.Count];
            for (int i = 0; i < slots.Count; i++) res[i] = slots[i].CharacterId;
            return res;
        }
    }
}
