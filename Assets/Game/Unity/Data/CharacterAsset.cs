using UnityEngine;

namespace RobaColores.Game.Presentation
{
    [CreateAssetMenu(menuName = "Roba Colores/Personaje", fileName = "Personaje")]
    public class CharacterAsset : ScriptableObject
    {
        public string displayName;
        public Color mainColor = Color.white;
        public GameObject pawnPrefab;    // figura que se mueve por el tablero
    }
}
