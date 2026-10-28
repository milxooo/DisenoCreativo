using System.Collections.Generic;
using UnityEngine;
using RobaColores.Game.Core;

namespace RobaColores.Map
{
    // Datos de una casilla del camino: color, indice (distancia en pasos desde INICIO) y conexiones hacia adelante.
    // No tiene logica de juego: MapBoard las convierte en el grafo logico que usa el nucleo.
    public class PathTile : MonoBehaviour
    {
        [SerializeField] PathTileColor color;
        [SerializeField] int index;
        [SerializeField] int tileId = -1;
        [SerializeField] string nodeName;
        [SerializeField] bool isStart;
        [SerializeField] bool isFinish;
        [SerializeField] bool triggersMinigame = true;
        [SerializeField] Difficulty difficulty;
        [SerializeField] List<PathTile> next = new List<PathTile>();

        public PathTileColor Color => color;
        public int Index => index;
        public int TileId => tileId;
        public string NodeName => nodeName;
        public bool IsStart => isStart;
        public bool IsFinish => isFinish;
        public bool TriggersMinigame => triggersMinigame;
        public Difficulty Difficulty => difficulty;
        public IReadOnlyList<PathTile> Next => next;

        public void Set(PathTileColor newColor, int newIndex)
        {
            color = newColor;
            index = newIndex;
        }

        public void ConfigureBoard(int id, string node, bool start, bool finish, Difficulty newDifficulty)
        {
            tileId = id;
            nodeName = node;
            isStart = start;
            isFinish = finish;
            triggersMinigame = !start && !finish;
            difficulty = newDifficulty;
        }

        public void AddNext(PathTile tile)
        {
            if (tile != null && !next.Contains(tile)) next.Add(tile);
        }
    }
}
