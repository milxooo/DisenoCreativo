using System;
using System.Collections.Generic;
using NUnit.Framework;
using RobaColores.Game.Core;

namespace RobaColores.Game.Tests
{
    public class CharacterSelectionTests
    {
        static CharacterSelection Four(bool repeats = false) { return new CharacterSelection(4, 6, repeats); }

        [Test] public void HasExactlyFourSlotsAllPendingAtStart()
        {
            var s = Four();
            Assert.AreEqual(4, s.SlotCount);
            for (int i = 0; i < 4; i++) { Assert.AreEqual(SlotState.Pending, s.Slot(i).State); Assert.AreEqual(i + 1, s.Slot(i).Number); }
            Assert.IsFalse(s.CanStart);
            Assert.AreEqual(4, s.Missing().Count);
        }

        [Test] public void SelectThenConfirmFollowsTheStates()
        {
            var s = Four();
            Assert.IsFalse(s.Confirm(0), "no se puede confirmar sin elegir");
            Assert.IsTrue(s.Select(0, 2));
            Assert.AreEqual(SlotState.Selected, s.Slot(0).State);
            Assert.IsTrue(s.Confirm(0));
            Assert.AreEqual(SlotState.Confirmed, s.Slot(0).State);
            Assert.IsFalse(s.Select(0, 3), "un hueco confirmado no cambia hasta desconfirmar");
            s.Unconfirm(0);
            Assert.AreEqual(SlotState.Selected, s.Slot(0).State);
            Assert.IsTrue(s.Select(0, 3));
            Assert.AreEqual(3, s.Slot(0).CharacterId);
        }

        [Test] public void StartStaysBlockedUntilAllSelectedPlayersAreConfirmed()
        {
            var s = Four();
            for (int i = 0; i < 3; i++) { s.Select(i, i); s.Confirm(i); Assert.IsFalse(s.CanStart); }
            Assert.AreEqual(1, s.Missing().Count);
            Assert.Throws<InvalidOperationException>(() => s.ConfirmedCharacters());
            s.Select(3, 5); Assert.IsFalse(s.CanStart, "seleccionado no basta, hay que confirmar");
            s.Confirm(3);
            Assert.IsTrue(s.CanStart);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 5 }, s.ConfirmedCharacters());
        }

        [Test] public void WithoutRepeatsTakenCharactersAreUnavailableToOthers()
        {
            var s = Four();
            s.Select(0, 1);
            Assert.IsFalse(s.IsAvailable(1, 1));
            Assert.IsFalse(s.Select(1, 1));
            Assert.AreEqual(0, s.OwnerOf(1, 1));
            Assert.IsTrue(s.IsAvailable(0, 1), "el propio hueco si puede conservarlo");
        }

        [Test] public void RepeatsAreAllowedWhenConfigured()
        {
            var s = Four(true);
            Assert.IsTrue(s.Select(0, 1)); Assert.IsTrue(s.Select(1, 1));
            s.Confirm(0); s.Confirm(1);
            Assert.AreEqual(0, s.Validate().FindAll(p => p.Contains("mismo personaje")).Count);
        }

        [Test] public void TurningRepeatsOffReportsExistingDuplicates()
        {
            var s = Four(true);
            for (int i = 0; i < 4; i++) { s.Select(i, i == 3 ? 0 : i); s.Confirm(i); }
            Assert.IsTrue(s.CanStart);
            s.AllowRepeats = false;
            Assert.IsFalse(s.CanStart);
            Assert.IsTrue(s.Validate().Exists(p => p.Contains("mismo personaje")));
        }

        [Test] public void CycleSkipsTakenCharactersAndWraps()
        {
            var s = Four();
            s.Select(1, 0); s.Select(2, 1);              // J2 tiene 0, J3 tiene 1
            Assert.IsTrue(s.Cycle(0, 1));                // desde Pendiente: primer disponible = 2
            Assert.AreEqual(2, s.Slot(0).CharacterId);
            Assert.IsTrue(s.Cycle(0, -1));               // atras: salta 1 y 0 (ocupados) y da la vuelta hasta 5
            Assert.AreEqual(5, s.Slot(0).CharacterId);
            Assert.IsTrue(s.Cycle(0, 1));                // adelante desde 5: 0 y 1 ocupados -> 2
            Assert.AreEqual(2, s.Slot(0).CharacterId);
        }

        [Test] public void ConfirmedSlotsCannotCycleAndClearResetsToPending()
        {
            var s = Four();
            s.Cycle(0, 1); s.Confirm(0);
            Assert.IsFalse(s.Cycle(0, 1));
            s.Unconfirm(0); s.Clear(0);
            Assert.AreEqual(SlotState.Pending, s.Slot(0).State);
            Assert.AreEqual(-1, s.Slot(0).CharacterId);
        }

        [Test] public void InvalidCharacterIndexesAreRejected()
        {
            var s = Four();
            Assert.IsFalse(s.Select(0, -1)); Assert.IsFalse(s.Select(0, 6));
            Assert.IsFalse(s.IsAvailable(0, 99));
        }

        [Test] public void SixSlotsAreAcceptedAndSevenAreRejected()
        {
            var s = new CharacterSelection(6, 6, false);
            Assert.AreEqual(6, s.SlotCount);
            Assert.Throws<ArgumentException>(() => new CharacterSelection(7, 7, false));
        }

        [Test] public void PlayerCountCanChangeFromTwoToSixAndCannotLeaveThatRange()
        {
            var s = new CharacterSelection(2, 6, false);
            s.SetSlotCount(6);
            Assert.AreEqual(6, s.SlotCount);
            s.SetSlotCount(2);
            Assert.AreEqual(2, s.SlotCount);
            Assert.Throws<ArgumentOutOfRangeException>(() => s.SetSlotCount(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.SetSlotCount(7));
        }
    }

    public class FourPlayerSessionTests
    {
        [Test] public void SessionSupportsSixPlayersWithSixCharacters()
        {
            var s = Fixtures.Session(new QueueRandom(1, 2, 3, 4, 5, 6), 6);
            Assert.AreEqual(6, s.Players.Count);
            Fixtures.StartAndRollOrder(s);
            Assert.AreEqual(6, s.TurnOrder.Count);
            CollectionAssert.AreEqual(new[] { 5, 4, 3, 2, 1, 0 }, new List<int>(s.TurnOrder), "orden por tirada descendente");
            foreach (var p in s.Players) Assert.AreEqual(s.Board.StartId, p.TileId);
        }

        [Test] public void SeventhPlayerIsRejected()
        {
            var s = Fixtures.Session(new QueueRandom(1), 6);
            Assert.Throws<InvalidOperationException>(() => s.AddPlayer("J7", 6));
        }

        [Test] public void RepeatedCharacterIsRejectedUnlessConfigured()
        {
            var s = new GameSession(new GameConfig(), Fixtures.SmallBoard(), Fixtures.Catalog(), new QueueRandom(1));
            s.AddPlayer("A", 3);
            Assert.Throws<InvalidOperationException>(() => s.AddPlayer("B", 3));
            var cfg = new GameConfig { AllowRepeatedCharacters = true };
            var s2 = new GameSession(cfg, Fixtures.SmallBoard(), Fixtures.Catalog(), new QueueRandom(1));
            s2.AddPlayer("A", 3);
            Assert.DoesNotThrow(() => s2.AddPlayer("B", 3));
        }
    }
}
