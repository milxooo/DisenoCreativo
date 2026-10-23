using UnityEngine;

namespace RobaColores.Map
{
    // Solo datos: color e índice en la secuencia INICIO -> META. Sin lógica de juego.
    public class PathTile : MonoBehaviour
    {
        [SerializeField] PathTileColor color;
        [SerializeField] int index;

        public PathTileColor Color => color;
        public int Index => index;

        public void Set(PathTileColor newColor, int newIndex)
        {
            color = newColor;
            index = newIndex;
        }
    }
}
