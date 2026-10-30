using UnityEngine;

namespace RobaColores.Game.Presentation
{
    // Imagenes y fondos del menu principal.
    [CreateAssetMenu(menuName = "Roba Colores/Arte del menu", fileName = "MenuArt")]
    public class MenuArt : ScriptableObject
    {
        [Tooltip("Diseno completo de la pantalla inicial; los botones se superponen como zonas clicables.")]
        public Texture2D mainMenuBackground;
        [Tooltip("Diseno completo de seleccion de jugadores; se superponen controles interactivos.")]
        public Texture2D playerSelectionBackground;
        [Tooltip("Titulo, kraken y pirata (linea oscura sobre fondo transparente).")]
        public Sprite titleArt;
        public Sprite swordIcon;
        public Sprite compassIcon;
        public Sprite chestIcon;

        [Tooltip("Opcional: 6 sprites para las caras del dado de orden (1 a 6). Vacio = caras provisionales dibujadas.")]
        public Sprite[] orderDiceFaces;
        [Tooltip("Opcional: 6 sprites para el dado de movimiento (1, 2, 3, 4, pierde turno, penalizacion).")]
        public Sprite[] moveDiceFaces;
    }
}
