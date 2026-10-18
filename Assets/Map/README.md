# Mapa pirata (Roba Colores)

Alcance: solo el entorno/tablero. Sin personajes, UI ni lógica de juego. Por ahora solo el **Mapa A** (horizontal).

## Estructura
- `Scenes/` escenas de cada mapa (una por mapa).
- `Prefabs/Blocks`, `Prefabs/PathTiles`, `Prefabs/Props` piezas reutilizables.
- `Textures/` texturas pixel art. Se importan solas con Point, sin compresión, sin mipmaps, 16 PPU (`Editor/MapTextureImporter.cs`).
- `ScriptableObjects/MapPalette.asset` paleta de colores (entorno + 7 casillas).
- `Scripts/PathTile.cs` datos mínimos de casilla: color (`PathTileColor`) e índice INICIO → META.
- `Prefabs/IsoPreviewCamera.prefab` cámara ortográfica X=30°, Y=45° solo para previsualizar.

## Paleta
| Entorno | Hex | Casillas | Hex |
|---|---|---|---|
| Contorno | 1A1423 | Rojo | D8363A |
| Arena | E3CE8C | Naranja | F08A24 |
| Madera | 8B5A2B | Amarillo | F2D335 |
| Agua | 3FC1C9 | Verde | 3FAE4A |
| Roca | 6B6478 | Azul | 3C78D8 |
| Pasto | 5E9E3C | Morado | 8E4FBF |
| Pergamino | EBDDB5 | Negro | 2B2B33 |

## Fuente
Pendiente: candidata Pirata One (licencia OFL). Se documentará aquí al añadirla.

## Editar el mapa
Pendiente (Fases 2-3).
