using UnityEngine;

namespace RobaColores.Game.Presentation
{
    // Imagenes del menu principal. Provisionales: recortes del boceto. Se sustituyen por el arte final sin tocar codigo.
    [CreateAssetMenu(menuName = "Roba Colores/Arte del menu", fileName = "MenuArt")]
    public class MenuArt : ScriptableObject
    {
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
