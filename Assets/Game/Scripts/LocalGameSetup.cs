using System;

namespace RobaColores.Game
{
    [Serializable]
    public sealed class PlayerSetup
    {
        public int playerNumber;
        public int characterIndex;
        public string characterName;
        public float miniGameTimePenaltySeconds;
        public int[] pixelInventory;
    }

    public static class LocalGameSetup
    {
        static PlayerSetup[] players = Array.Empty<PlayerSetup>();

        public static PlayerSetup[] Players
        {
            get
            {
                var copy = new PlayerSetup[players.Length];
                for (var i = 0; i < players.Length; i++)
                {
                    copy[i] = new PlayerSetup
                    {
                        playerNumber = players[i].playerNumber,
                        characterIndex = players[i].characterIndex,
                        characterName = players[i].characterName,
                        miniGameTimePenaltySeconds = players[i].miniGameTimePenaltySeconds,
                        pixelInventory = (int[])players[i].pixelInventory.Clone()
                    };
                }

                return copy;
            }
        }

        public static void AddMiniGameTimePenalty(int playerIndex, float seconds)
        {
            if (playerIndex < 0 || playerIndex >= players.Length)
                throw new ArgumentOutOfRangeException(nameof(playerIndex));
            if (seconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds));

            players[playerIndex].miniGameTimePenaltySeconds += seconds;
        }

        public static float ConsumeMiniGameTimePenalty(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= players.Length)
                throw new ArgumentOutOfRangeException(nameof(playerIndex));

            var penalty = players[playerIndex].miniGameTimePenaltySeconds;
            players[playerIndex].miniGameTimePenaltySeconds = 0f;
            return penalty;
        }

        public static void SetPixelInventory(int playerIndex, int[] inventory)
        {
            if (playerIndex < 0 || playerIndex >= players.Length)
                throw new ArgumentOutOfRangeException(nameof(playerIndex));
            if (inventory == null || inventory.Length != 7)
                throw new ArgumentException("El inventario debe incluir los siete colores.", nameof(inventory));

            foreach (var count in inventory)
            {
                if (count < 0)
                    throw new ArgumentOutOfRangeException(nameof(inventory), "El inventario no puede ser negativo.");
            }

            players[playerIndex].pixelInventory = (int[])inventory.Clone();
        }

        public static void SetPlayers(int[] characterIndices, string[] characterNames)
        {
            if (characterIndices == null || characterNames == null ||
                characterIndices.Length != characterNames.Length ||
                characterIndices.Length < 2 || characterIndices.Length > 4)
            {
                throw new ArgumentException("La partida debe tener de 2 a 4 personajes.");
            }

            var updatedPlayers = new PlayerSetup[characterIndices.Length];
            for (var i = 0; i < characterIndices.Length; i++)
            {
                updatedPlayers[i] = new PlayerSetup
                {
                    playerNumber = i + 1,
                    characterIndex = characterIndices[i],
                    characterName = characterNames[i],
                    pixelInventory = new[] { 200, 200, 200, 200, 200, 200, 200 }
                };
            }

            players = updatedPlayers;
        }
    }
}
