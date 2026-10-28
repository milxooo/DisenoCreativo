# Roba Colores - mecánicas del juego

Juego de tablero estilo Mario Party sobre el Mapa A: se tira un dado, se avanza por casillas de color, cada casilla lanza un minijuego de Pixel Art para todos, el ganador roba una fila de píxeles a cada oponente y los jugadores sin recursos quedan descalificados.

## Cómo probarlo
1. Abre `Assets/Game/Scenes/Menu.unity` y pulsa Play.
2. JUGAR → elige jugadores (2 a 4) y personajes → COMENZAR (carga `Partida.unity`).
3. En el tablero: **Espacio** o el botón tiran el dado de movimiento. En una bifurcación hay que elegir camino (botones y marcadores dorados). Al caer en una casilla de color empieza el minijuego.
4. Minijuego: clic izquierdo pinta, clic derecho (o la X de la paleta) borra. TERMINAR lo acaba para todos.

Menús de prueba (Unity): **Roba Colores > Ejecutar pruebas del núcleo**, **Simular partidas con bots**, **Fase 2 - Generar contenido del juego**, **Fase 2 - Crear escenas Menu y Partida**.

## Arquitectura
| Capa | Carpeta | Contenido |
|---|---|---|
| Núcleo (C# puro, sin Unity) | `Core/` (`RobaColores.Game.Core`) | `GameSession` (turnos/fases), dados, orden inicial, `BoardGraph`, `MinigameRound`, `PixelGrid`, evaluador, `Inventory`, robo de filas, descalificación, victoria |
| Datos | `Unity/Data/` | ScriptableObjects: `GameContent`, `GameSettingsAsset`, `PaletteAsset`, `FigureAsset`, `CharacterAsset`; `SessionSetup` (menú → partida) |
| Presentación | `Unity/Runtime/` | `GameController` (partida), `MinigameView`, `MenuController`, `UiKit` (uGUI por código), `GameText` |
| Mapa | `Assets/Map/Scripts/` | `PathTile` (conexiones hacia adelante), `MapBoard` (construye el `BoardGraph`) |
| Pruebas | `Tests/EditMode/`, `Editor/` | 42 pruebas NUnit, simulación con bots, generadores de contenido y escenas |

El núcleo no depende de Unity, de escenas ni de la red: se maneja con métodos y eventos (`GameSession.Event`), así que sirve igual para un solo dispositivo o para multijugador en red.

## Reglas implementadas
- **Orden inicial:** dado de 6 caras (1-6). Mayor primero; los empates se repiten entre los empatados (también los empates en posiciones bajas, para tener un orden estricto).
- **Dado de movimiento (configurable en `GameSettings.asset`):** 1, 2, 3, 4 casillas, perder turno, penalización de tiempo. Se avanza exactamente esa cantidad; en cada bifurcación se elige camino. Llegar a la META gana al instante.
- **Turno perdido:** se marca al sacar la cara y se consume al empezar el siguiente turno de ese jugador (no se acumula).
- **Penalización de tiempo (PROVISIONAL):** al empezar el siguiente minijuego del jugador su cuadrícula queda bloqueada **10 s** (el temporizador es global, no hay tiempos por jugador). El valor requiere confirmación.
- **Categoría por color:** naranja frutas y verduras, rojo partes del cuerpo, amarillo accesorios de ropa, verde animales, azul profesiones, morado deportes, negro astronomía. Es un diccionario de datos (`GameConfig.CategoryByColor`), no depende del material. Todas las casillas de color (menos INICIO) activan minijuego; es configurable por casilla (`PathTile.triggersMinigame`).
- **Dificultad:** propiedad aparte de la casilla; el generador del mapa la sube con el avance (primer tercio fácil, segundo intermedio, último difícil). Si no hay figuras de esa dificultad se usa otra de la misma categoría.
- **Minijuego:** 90 s globales; cualquier participante puede terminar; fin único para todos; se bloquean todas las cuadrículas y se evalúa. Quien activa el minijuego no tiene ventaja.
- **Puntuación:** `(correctos - errores - 0.5 x faltantes) / píxeles de la referencia x 100`, mínimo 0. Pesos en `GameSettings.asset`. Empate = mismo puntaje máximo, sin desempate por velocidad. Si nadie supera 0 no hay ganador ni robo.
- **Inventario (fuente única):** 200 unidades de cada uno de los 8 colores de la paleta. Pintar gasta 1 unidad del color; cambiar un píxel devuelve la unidad anterior y gasta la nueva; borrar la devuelve. Nunca hay cantidades negativas.
- **Dibujar consume (decisión tomada):** al terminar, lo pintado queda gastado. El ganador elige una fila de la cuadrícula final de cada oponente y recupera los colores de esa fila (las celdas vacías no dan nada). Con empate, cada ganador recibe `floor(unidades / ganadores)` por color; no se crean recursos.
- **Descalificación:** un jugador queda fuera cuando con sus unidades no puede construir **ninguna** figura del catálogo. No vuelve aunque reciba recursos después y no participa en más minijuegos.
- **Fin de partida:** llegar a la META, o quedar un solo jugador habilitado. Si todos quedan descalificados a la vez: partida sin ganador.

## Datos de prueba (no son un catálogo completo)
- 14 figuras de prueba: una fácil (8x8) y una intermedia por categoría. Faltan figuras difíciles y más variedad. Se añaden creando un `FigureAsset` (filas de texto con letras de la paleta; `.` = vacío) y arrastrándolo a `GameContent.figures`.
- 4 personajes provisionales (peones voxel de colores) en `Data/Personajes`. Reemplázalos con arte propio cambiando `CharacterAsset.pawnPrefab`.
- Paleta de dibujo de 8 colores (rojo, naranja, amarillo, verde, azul, morado, negro, blanco): decisión mía, se cambia en `Data/Palette.asset`.

## Verificación hecha
- 42 pruebas EditMode del núcleo: todas correctas.
- Simulación de 60 partidas con bots sobre el tablero real y el catálogo real: 59 terminaron por llegar a la meta, 1 por eliminación, 0 atascadas, 0 violaciones de inventario.
- Tablero real (`MapBoard`): 93 casillas, 4 bifurcaciones, 6 rutas de 32 pasos, sin casillas inalcanzables ni sin salida.
- Partida completa en modo Play con piloto automático (opción `autopilot` de `GameController`, solo para pruebas): más de 60 s con turnos, bifurcaciones, minijuegos y robos, sin errores en consola.
- **No verificado:** clics reales de ratón y dibujo manual de píxeles, la pantalla de fin de partida, la de elección de camino y los modos de 3 y 4 jugadores en la interfaz (solo cubiertos por pruebas del núcleo y bots).

## Decisiones y pendientes que requieren tu revisión
1. Duración de la penalización de tiempo (10 s provisional) y su modelo (bloqueo al inicio del siguiente minijuego).
2. Pesos de la fórmula de puntuación (propuesta 1 / 1 / 0.5).
3. Paleta de dibujo de 8 colores y dificultad por progreso del mapa.
4. Equilibrio: con 200 unidades por color y figuras de 25-40 píxeles de un mismo color, un jugador puede quedarse sin un color en unos 8 minijuegos. En las simulaciones casi nadie llega a descalificarse antes de la meta; ajusta `initialUnitsPerColor` o el tamaño de las figuras según lo que busques.
5. **Multijugador sin decidir:** el núcleo no asume dispositivo. Para un solo dispositivo falta decidir cómo se turnan o comparten ratón y teclado en el minijuego (hoy todos dibujan con el mismo ratón). Para red habría que definir quién tiene autoridad sobre los dados y el temporizador y enviar solo órdenes y eventos. No hay red implementada.
6. Menú: solo se reproduce la estructura del boceto (título, JUGAR/OPCIONES/PERSONALIZAR/SALIDA, pergamino); no están el pirata, el kraken ni el papel quemado. OPCIONES es de solo lectura y PERSONALIZAR muestra los personajes. No hay mockups de la selección de personajes ni del minijuego.
7. Se modificó `ProjectSettings/EditorBuildSettings.asset` (Build Settings: Menu y Partida) para poder cargar escenas por nombre.
