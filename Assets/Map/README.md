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
**Pirata One** (Google Fonts, SIL Open Font License 1.1). Estilo gótico/pirata, legible a tamaño grande, y la OFL permite incluirla en videojuegos. Archivo y licencia en `Fonts/`. Falta crear el asset TextMeshPro con filtro Point al hacer los carteles (Fase 3).

## Biblioteca (Fase 2)
- `Textures/MapAtlas.png`: atlas 128x128 (8x8 celdas de 16 px) generado por código. Un solo material: `Materials/MapAtlas.mat` (URP Lit sin brillo).
- `Meshes/`: una malla por prefab con UV al atlas. Pivote abajo al centro, Batching Static.
- `Prefabs/Blocks`: Pasto, Tierra, Arena, Madera, Roca, Agua (0.9 de alto), Pergamino. Bloque = 1x1x1.
- `Prefabs/PathTiles`: `Casilla_<Color>` x7. Óvalo de 1.8x1.4 con borde oscuro y un glifo en relieve por color (aro, cruz, X, triángulo, líneas horizontales, líneas verticales, cuadrado), con componente `PathTile`.
- `Prefabs/Props`: Barril, Caja, Cofre, Palmera, BanderaPirata, Ancla, Canon, Calavera, Cartel, PosteMuelle, RosaDeLosVientos, XTesoro, Kraken, BarcoPirata, Remolino, CuevaArco, Montana, Roca.
- `Scenes/Biblioteca_Preview.unity`: todos los prefabs a la vista con la cámara isométrica.
- Se regenera con el menú **Roba Colores > Fase 2 - Generar biblioteca** (`Editor/MapLibraryGenerator.cs`). Para cambiar una pieza, edítala ahí.

## Mapa A (Fase 3)
- Escena: `Scenes/MapaA.unity`. Tablero de 64x48 bloques dentro de un marco de madera. INICIO en z bajo (muelle sur), META en z alto (muelle norte). Con la cámara isométrica el mapa se ve girado 45°.
- `Terreno/`: 12 mallas combinadas (chunks de 16x16) con el mismo atlas. `Camino/`: casillas (prefabs). `Decoracion/`: props (prefabs).
- Casillas: `Casilla_<índice>_<Color>`. El índice es la distancia en casillas desde INICIO por el camino más corto (`PathTile.Index`); en las ramas paralelas varias casillas comparten índice, y META tiene el índice más alto de su recorrido. Los cruces/bifurcaciones llevan sufijo con el nombre del nodo (S, M, N1...).
- Carteles "INICIO" y "META": `Textures/MapSigns.png`, rasterizado con Pirata One y pixelado (sin TextMeshPro). Prefabs `Cartel_Inicio` y `Cartel_Meta`.

## Pulido y rendimiento (Fase 5)
- Iluminación: una sola luz direccional (sin sombras en tiempo real, look plano) y ambiente plano frío. Sin luces extra.
- Ambientación: `Scripts/Bobbing.cs` balancea Kraken, Barco y Remolino (solo visual; esos tres quedan fuera del static batching).
- Medido en `MapaA.unity`: 185 renderers (182 con Batching Static), 2 materiales (`MapAtlas`, `MapSigns`), ~61k triángulos, 1 luz.

## Editar el mapa
El layout vive en `Editor/MapAGenerator.cs`: nodos y tramos del camino (`Nodes`, `BuildEdges`), clasificación del terreno (`Classify`) y decoración (`Decorate`). Cambia los datos y ejecuta **Roba Colores > Fase 3 - Generar Mapa A**, que regenera la escena completa. No edites a mano los chunks de `Terreno/`, se sobrescriben.
