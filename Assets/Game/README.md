
# Roba Colores - mecánicas del juego

Juego de tablero estilo Mario Party sobre el Mapa A: se tira un dado, se avanza por casillas de color, cada casilla lanza un minijuego de Pixel Art para todos, el ganador roba una fila de píxeles a cada oponente y los jugadores sin recursos quedan descalificados.

## Cómo probarlo
1. Abre `Assets/Game/Scenes/Menu.unity` y pulsa Play.
2. JUGAR → se eligen de **dos a seis** jugadores, cada uno elige y confirma su personaje → COMENZAR (lanzamiento de dados y carga de `Partida.unity`). Detalle en "Menú principal y selección".
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
| Pruebas | `Tests/EditMode/`, `Editor/` | 58 pruebas NUnit, simulación con bots, generadores de contenido y escenas |

El núcleo no depende de Unity, de escenas ni de la red: se maneja con métodos y eventos (`GameSession.Event`), así que sirve igual para un solo dispositivo o para multijugador en red.

## Menú principal y selección de personajes
**Navegación:** Menú → JUGAR → selección de personajes → COMENZAR → `Partida`. OPCIONES permite activar/desactivar la repetición de personajes y ajustar la duración del minijuego y la penalización de tiempo. PERSONALIZAR muestra los personajes. SALIDA cierra (en el editor detiene el Play).

**Diseño:** sigue el boceto (`Art/Menu/Mockup_Menu.png`): pergamino con bordes quemados, título con kraken y pirata a la izquierda, botones con icono a la derecha y SALIDA abajo. Todo se coloca en un escenario de 1393 x 1080 (proporción del boceto) que se escala para caber en cualquier pantalla; los lados sobrantes se rellenan con madera. Comprobado a 16:9, 21:9, 4:3 y vertical.

**Animaciones** (sin librerías; `UiAnim`, tiempo sin escala): título que aparece deslizándose, botones que entran escalonados, respiración y balanceo sutil del arte, hover/pulsado/deshabilitado en los botones (`ButtonFeedback`), fundido entre pantallas y lanzamiento de dados al comenzar. Ninguna bloquea clics. No hay sonidos porque no hay recursos de audio.

**Dados de presentación** (`DiceView` y `DiceShowcase`): dado de orden (1 a 6, con puntos) y dado de movimiento (1, 2, 3, 4, pierde turno, penalización), cada uno con su texto. Giran y se asientan en una cara al azar; se repiten con clic o cada 7 s. Son solo decoración: usan su propio azar y no tocan el estado ni el orden de la partida. Las caras son provisionales (dibujadas por código): para usar arte propio, rellena `MenuArt.orderDiceFaces` / `moveDiceFaces` con 6 sprites cada uno.

**De dos a seis jugadores:** el rango se configura en `GameSettings.minPlayers = 2` y `maxPlayers = 6` y se ajusta con los botones − / + de la selección. Hay seis retratos de personaje y un panel por jugador en la vista genérica. Cada uno muestra jugador, retrato, flechas, fichas de color para elegir directo, estado (PENDIENTE / SELECCIONADO / CONFIRMADO) y CONFIRMAR / CAMBIAR. Pulsar el retrato de un personaje ya confirmado permite volver a cambiarlo. COMENZAR se habilita solo cuando todos están confirmados y, si no, dice quién falta. La validación vive en el núcleo (`CharacterSelection`, con pruebas).

**Repetición de personajes (PROVISIONAL):** configurable con `GameSettings.allowRepeatedCharacters` (hoy `false`: cada jugador un personaje distinto, y por eso hay seis personajes). Con `true`, varios jugadores pueden elegir el mismo.

**Datos hacia la partida:** al confirmar, `SessionSetup.Commit` guarda los jugadores elegidos (número, personaje, nombre) y la partida los usa para crear los peones en la casilla inicial, el HUD, el orden de turnos y las cuadrículas del minijuego. Con cinco o seis participantes, el minijuego usa una rejilla 3 x 2 para mantener legibles las cuadrículas. Un indicador `starting` evita pulsar COMENZAR dos veces.

**Cómo incorporar los personajes definitivos:** en `Data/Personajes/` cada `CharacterAsset` tiene id, nombre, descripción, color, `portrait` (sprite), `pawnPrefab` (modelo 3D) y materiales extra. Rellena esos campos (o crea un asset nuevo y añádelo a `GameContent.characters`): no hay que tocar la pantalla ni la lógica. Los retratos actuales (32x32) y los peones voxel son provisionales. Para sustituir el arte del menú: `Art/Menu/MenuArt.asset`.

## Reglas implementadas
- **Orden inicial:** dado de 6 caras (1-6). Mayor primero; los empates se repiten entre los empatados (también los empates en posiciones bajas, para tener un orden estricto).
- **Dado de movimiento (configurable en `GameSettings.asset`):** 1, 2, 3, 4 casillas, perder turno, penalización de tiempo. Se avanza exactamente esa cantidad; en cada bifurcación se elige camino. Llegar a la META gana al instante.
- **Turno perdido:** se marca al sacar la cara y se consume al empezar el siguiente turno de ese jugador (no se acumula).
- **Penalización de tiempo (PROVISIONAL):** al empezar el siguiente minijuego del jugador su cuadrícula queda bloqueada **10 s por defecto** (el temporizador es global, no hay tiempos por jugador). Se ajusta en OPCIONES de 0 a 30 s en pasos de 5.
- **Categoría por color:** naranja frutas y verduras, rojo partes del cuerpo, amarillo accesorios de ropa, verde animales, azul profesiones, morado deportes, negro astronomía. Es un diccionario de datos (`GameConfig.CategoryByColor`), no depende del material. Todas las casillas de color (menos INICIO) activan minijuego; es configurable por casilla (`PathTile.triggersMinigame`).
- **Dificultad:** propiedad aparte de la casilla; el generador del mapa la sube con el avance (primer tercio fácil, segundo intermedio, último difícil). Si no hay figuras de esa dificultad se usa otra de la misma categoría.
- **Minijuego:** 90 s globales por defecto (ajustable en OPCIONES de 30 a 180 s); cualquier participante puede terminar; fin único para todos; se bloquean todas las cuadrículas y se evalúa. Quien activa el minijuego no tiene ventaja.
- **Puntuación:** `(correctos - errores - 0.5 x faltantes) / píxeles de la referencia x 100`, mínimo 0. Pesos en `GameSettings.asset`. Empate = mismo puntaje máximo, sin desempate por velocidad. Si nadie supera 0 no hay ganador ni robo.
- **Inventario (fuente única):** 200 unidades de cada uno de los 8 colores de la paleta. Pintar gasta 1 unidad del color; cambiar un píxel devuelve la unidad anterior y gasta la nueva; borrar la devuelve. Nunca hay cantidades negativas.
- **Dibujar consume (decisión tomada):** al terminar, lo pintado queda gastado. El ganador elige una fila de la cuadrícula final de cada oponente y recupera los colores de esa fila (las celdas vacías no dan nada). Con empate, cada ganador recibe `floor(unidades / ganadores)` por color; no se crean recursos.
- **Descalificación:** un jugador queda fuera cuando con sus unidades no puede construir **ninguna** figura del catálogo. No vuelve aunque reciba recursos después y no participa en más minijuegos.
- **Fin de partida:** llegar a la META, o quedar un solo jugador habilitado. Si todos quedan descalificados a la vez: partida sin ganador.

## Datos de prueba (no son un catálogo completo)
- 14 figuras de prueba: una fácil (8x8) y una intermedia por categoría. Faltan figuras difíciles y más variedad. Se añaden creando un `FigureAsset` (filas de texto con letras de la paleta; `.` = vacío) y arrastrándolo a `GameContent.figures`.
- 6 personajes provisionales (peones voxel y retratos pixelados de colores) en `Data/Personajes`. Reemplázalos con arte propio en cada `CharacterAsset`.
- Paleta de dibujo de 8 colores (rojo, naranja, amarillo, verde, azul, morado, negro, blanco): decisión mía, se cambia en `Data/Palette.asset`.

## Verificación hecha
- Pruebas EditMode del núcleo (incluye selección de personajes y partidas de dos a seis): todas correctas.
- Simulación de 80 partidas con bots sobre el tablero real y el catálogo real (con 2 a 6 jugadores): 67 por llegada a meta, 11 por eliminación, 2 sin ganador, 0 atascadas, 0 violaciones de inventario.
- Menú en modo Play: pantalla principal, selección vacía (dos paneles PENDIENTE, COMENZAR deshabilitado) y selección con seis confirmados (COMENZAR habilitado), todo capturado y revisado; flujo Menú → lanzamiento → `Partida` con seis peones y seis jugadores en el HUD, sin errores en consola.
- Composición del menú renderizada a 2560x1080, 1024x768 y 720x1280 sin cortes ni solapes.
- Tablero real (`MapBoard`): 93 casillas, 4 bifurcaciones, 6 rutas de 32 pasos, sin casillas inalcanzables ni sin salida.
- Partida completa en modo Play con piloto automático (opción `autopilot` de `GameController`, solo para pruebas): más de 60 s con turnos, bifurcaciones, minijuegos y robos, sin errores en consola.
- **No verificado:** clics reales de ratón (hover, pulsado, flechas y fichas de selección, CONFIRMAR / CAMBIAR) y dibujo manual de píxeles; solo se probaron estados y flujo con ayudas de prueba (`debugAutoSelect`, `debugAutoStart`) y con la lógica del núcleo. Tampoco la pantalla de fin de partida, la de elección de camino ni el minijuego con seis paneles en pantalla real. El orden de turnos con seis jugadores solo se vio con el modal rediseñado en código (no capturado tras el cambio).
- El arte del menú son recortes del boceto de 605x469 px: al ampliarse se ve blando. Hay que sustituirlo por arte final.

## Decisiones y pendientes que requieren tu revisión
1. Duración por defecto de la penalización de tiempo (10 s, ajustable) y su modelo (bloqueo al inicio del siguiente minijuego).
2. Pesos de la fórmula de puntuación (propuesta 1 / 1 / 0.5).
3. Paleta de dibujo de 8 colores y dificultad por progreso del mapa.
4. Equilibrio: con 200 unidades por color y figuras de 25-40 píxeles de un mismo color, un jugador puede quedarse sin un color en unos 8 minijuegos. En las simulaciones casi nadie llega a descalificarse antes de la meta; ajusta `initialUnitsPerColor` o el tamaño de las figuras según lo que busques.
5. **Multijugador sin decidir:** el núcleo no asume dispositivo. Para un solo dispositivo falta decidir cómo se turnan o comparten ratón y teclado en el minijuego (hoy todos dibujan con el mismo ratón). Para red habría que definir quién tiene autoridad sobre los dados y el temporizador y enviar solo órdenes y eventos. No hay red implementada.
6. Menú: se reproduce la estructura del boceto (título, JUGAR/OPCIONES/PERSONALIZAR/SALIDA, pergamino); OPCIONES modifica `GameSettings` en caliente y PERSONALIZAR muestra los personajes. La selección usa el arte de `Seleccion_Jugadores.jpg` como fondo y el minijuego se alinea con el boceto de Pixel Art.
7. Se modificó `ProjectSettings/EditorBuildSettings.asset` (Build Settings: Menu y Partida) para poder cargar escenas por nombre.
