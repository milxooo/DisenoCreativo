using System.Collections.Generic;
using UnityEngine;
using RobaColores.Game.Core;

namespace RobaColores.Map
{
    // Une el mapa visual con el nucleo del juego: lista las casillas y construye el BoardGraph logico.
    public class MapBoard : MonoBehaviour
    {
        [SerializeField] List<PathTile> tiles = new List<PathTile>();

        public IReadOnlyList<PathTile> Tiles => tiles;

        public void SetTiles(List<PathTile> newTiles) { tiles = newTiles; }

        public PathTile TileById(int id)
        {
            foreach (var t in tiles) if (t.TileId == id) return t;
            return null;
        }

        // Posicion de pie de una casilla (para colocar personajes).
        public Vector3 PositionOf(int id)
        {
            var t = TileById(id);
            return t != null ? t.transform.position + Vector3.up * 0.25f : transform.position;
        }

        public BoardGraph BuildGraph()
        {
            var graph = new BoardGraph();
            foreach (var t in tiles)
            {
                graph.Add(new BoardTile
                {
                    Id = t.TileId,
                    Name = name + "/" + t.name,
                    Color = (TileColor)(int)t.Color,
                    Difficulty = t.Difficulty,
                    IsStart = t.IsStart,
                    IsFinish = t.IsFinish,
                    TriggersMinigame = t.TriggersMinigame,
                });
            }
            foreach (var t in tiles)
                foreach (var n in t.Next) graph.Connect(t.TileId, n.TileId);
            return graph;
        }
    }
}
