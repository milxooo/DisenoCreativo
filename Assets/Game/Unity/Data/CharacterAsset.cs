using UnityEngine;

namespace RobaColores.Game.Presentation
{
    // Un personaje jugable. Toda la interfaz y la partida leen estos datos: para incorporar un personaje definitivo
    // basta con crear un asset y anadirlo a GameContent.characters (o rellenar los campos de uno provisional).
    [CreateAssetMenu(menuName = "Roba Colores/Personaje", fileName = "Personaje")]
    public class CharacterAsset : ScriptableObject
    {
        [Tooltip("Identificador unico y estable (no cambiarlo una vez en uso).")]
        public string id;
        public string displayName;
        [TextArea(1, 3)] public string description;
        [Tooltip("Color de identificacion en interfaz (paneles, nombre del jugador).")]
        public Color mainColor = Color.white;
        [Tooltip("Imagen para el menu de seleccion. Si esta vacia se muestra un retrato provisional de color.")]
        public Sprite portrait;
        [Tooltip("Modelo 3D (peon) que se mueve por el tablero.")]
        public GameObject pawnPrefab;
        [Tooltip("Materiales o recursos visuales adicionales que necesite el modelo.")]
        public Material[] extraMaterials;
    }
}
