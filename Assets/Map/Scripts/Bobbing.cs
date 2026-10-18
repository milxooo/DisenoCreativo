using UnityEngine;

namespace RobaColores.Map
{
    // Ambientacion: balanceo vertical suave (y giro opcional) de props del agua. Sin logica de juego.
    public class Bobbing : MonoBehaviour
    {
        [SerializeField] float amplitude = 0.08f;
        [SerializeField] float speed = 0.5f;
        [SerializeField] float spinDegreesPerSecond;

        Vector3 basePosition;

        public void Configure(float newAmplitude, float newSpeed, float newSpin)
        {
            amplitude = newAmplitude;
            speed = newSpeed;
            spinDegreesPerSecond = newSpin;
        }

        void Start()
        {
            basePosition = transform.localPosition;
        }

        void Update()
        {
            transform.localPosition = basePosition + Vector3.up * (Mathf.Sin(Time.time * speed * Mathf.PI * 2f) * amplitude);
            if (spinDegreesPerSecond != 0f) transform.Rotate(0f, spinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
        }
    }
}
