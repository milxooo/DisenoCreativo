using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RobaColores.Game.Presentation
{
    // Celda de una cuadricula: clic izquierdo pinta, clic derecho borra; arrastrar con el boton pulsado repite la accion.
    public class CellHandler : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler
    {
        public Action<bool> onAction;   // true = pintar, false = borrar

        public void OnPointerDown(PointerEventData e)
        {
            if (onAction == null) return;
            if (e.button == PointerEventData.InputButton.Left) onAction(true);
            else if (e.button == PointerEventData.InputButton.Right) onAction(false);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            var mouse = Mouse.current;
            if (onAction == null || mouse == null) return;
            if (mouse.leftButton.isPressed) onAction(true);
            else if (mouse.rightButton.isPressed) onAction(false);
        }
    }
}
