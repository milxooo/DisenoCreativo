using RobaColores.Game.Core;

namespace RobaColores.Game.Presentation
{
    // Textos del juego en un solo sitio (para localizar o cambiar sin tocar la logica).
    public static class GameText
    {
        public static string CategoryName(Category c)
        {
            switch (c)
            {
                case Category.PartesDelCuerpo: return "Partes del cuerpo";
                case Category.FrutasYVerduras: return "Frutas y verduras";
                case Category.AccesoriosDeRopa: return "Accesorios de ropa";
                case Category.Animales: return "Animales";
                case Category.Profesiones: return "Profesiones";
                case Category.Deportes: return "Deportes";
                default: return "Astronomía";
            }
        }

        public static string ColorName(TileColor c)
        {
            switch (c)
            {
                case TileColor.Rojo: return "Rojo";
                case TileColor.Naranja: return "Naranja";
                case TileColor.Amarillo: return "Amarillo";
                case TileColor.Verde: return "Verde";
                case TileColor.Azul: return "Azul";
                case TileColor.Morado: return "Morado";
                default: return "Negro";
            }
        }

        public static string FaceText(MoveFace f)
        {
            switch (f.Kind)
            {
                case MoveFaceKind.Advance: return "avanza " + f.Steps + (f.Steps == 1 ? " casilla" : " casillas");
                case MoveFaceKind.LoseTurn: return "pierde un turno";
                default: return "recibe una penalización de tiempo";
            }
        }

        public static string DifficultyName(Difficulty d)
        {
            switch (d)
            {
                case Difficulty.Facil: return "fácil";
                case Difficulty.Intermedio: return "intermedia";
                default: return "difícil";
            }
        }
    }
}
