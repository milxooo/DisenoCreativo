# Menú y selección local

La escena `Assets/Scenes/SampleScene.unity` muestra el menú al iniciar el juego. Desde **Jugar** se configura una partida local de 2 a 4 personas, se elige un personaje distinto para cada una y se abre `Assets/Map/Scenes/MapaA.unity`.

Los personajes se cargan desde `Assets/Resources/CharacterArt/` y el menú usa la fuente Pirata One incluida en `Assets/Resources/Fonts/`. La selección queda disponible en `RobaColores.Game.LocalGameSetup.Players` al cambiar a la escena del mapa.

## Turnos y movimiento

En el mapa, cada jugador aparece con el retrato seleccionado y su ficha; el resultado más alto de una tirada del 1 al 4 define quién inicia y los empates se vuelven a tirar. El dado de turnos tiene cuatro resultados de avance (1–4), una cara que hace perder el turno y otra que agrega 15 segundos de penalización al próximo minijuego de pixel art del jugador. En una bifurcación, cada jugador escoge una ruta que acerque a la meta.

El movimiento usa las posiciones de las casillas del mapa; el tablero debe conservar las casillas `_S` (inicio) y `_M` (meta), y el espaciado entre casillas debe seguir siendo menor a 2.7 unidades.

Al terminar un movimiento, todos participan en el mismo reto local de pixel art de 25 × 25. Hay 90 segundos, la referencia queda visible y cada tablero puede entregarse antes de que termine el tiempo. La puntuación compara los píxeles correctos con la referencia y penaliza tanto los colores incorrectos como los píxeles fuera de la figura. Quien gana roba una fila con más píxeles dibujados a cada rival; en empate, esas filas se distribuyen por turnos entre quienes empataron. Las existencias empiezan en 200 píxeles por color y se conservan entre rondas. Si solo queda una persona con colores suficientes para jugar, gana la partida.

Los siete colores del tablero representan los temas del reto: rojo (partes del cuerpo), naranja (frutas y verduras), amarillo (accesorios), verde (animales), azul (profesiones), morado (deportes) y negro (astronomía). Para este prototipo, rojo y naranja son dificultad fácil; amarillo, verde y azul, media; morado y negro, difícil. Las referencias actuales se generan por código y pueden reemplazarse por ilustraciones finales después.
