using UnityEngine;

namespace Unity.Test
{
    [RequireComponent(typeof(Rigidbody))]
    public class RotatingPlatform : MonoBehaviour
    {
        [Tooltip("Angular velocity in degrees per second around the Y axis.")] [SerializeField]
        private float rotationSpeed = 30f;

        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true; // Платформа управляется скриптом, а не физикой
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate; // Для плавности
        }

        private void FixedUpdate()
        {
            // Используем MoveRotation, чтобы физический движок корректно рассчитал угловую скорость
            // и передал её при запросе GetPointVelocity в момент столкновения.
            var deltaRot = Quaternion.Euler(0f, rotationSpeed * Time.fixedDeltaTime, 0f);
            _rb.MoveRotation(_rb.rotation * deltaRot);
        }
    }
}