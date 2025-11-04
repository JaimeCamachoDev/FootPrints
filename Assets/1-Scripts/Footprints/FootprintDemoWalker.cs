using UnityEngine;

namespace Footprints
{
    /// <summary>
    /// Simple demo walker that follows a circular path and stamps footsteps into a FootprintPainterRT.
    /// Designed purely for the Footprints RT sample scene.
    /// </summary>
    public class FootprintDemoWalker : MonoBehaviour
    {
        [Header("Stamping")]
        [SerializeField] private FootprintPainterRT painter;
        [SerializeField, Min(0.1f)] private float stepSpacing = 0.5f;
        [SerializeField, Min(0f)] private float footSeparation = 0.35f;
        [SerializeField] private Vector2 stampScale = new Vector2(0.35f, 0.6f);

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 1.6f;
        [SerializeField, Min(0.5f)] private float pathRadius = 5f;
        [SerializeField] private Vector3 pathOffset = Vector3.zero;
        [SerializeField] private bool faceDirection = true;
        [SerializeField] private bool clearMaskOnStart = true;

        private float _travelDistance;
        private float _stepAccumulator;
        private int _stepIndex;

        private void Start()
        {
            if (clearMaskOnStart)
            {
                painter?.ClearMask();
            }
        }

        private void Update()
        {
            if (painter == null)
            {
                return;
            }

            float delta = moveSpeed * Time.deltaTime;
            _travelDistance += delta;
            _stepAccumulator += delta;

            Vector3 position = EvaluatePosition(_travelDistance);
            Vector3 forward = EvaluateForward(_travelDistance);

            transform.position = position;
            if (faceDirection && forward.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }

            while (_stepAccumulator >= stepSpacing)
            {
                _stepAccumulator -= stepSpacing;
                float distanceAtStep = _travelDistance - _stepAccumulator;
                EmitFootstep(distanceAtStep);
            }
        }

        private void EmitFootstep(float distanceAlongPath)
        {
            Vector3 forward = EvaluateForward(distanceAlongPath).normalized;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            Vector3 right = new Vector3(forward.z, 0f, -forward.x).normalized;
            float side = (_stepIndex & 1) == 0 ? 1f : -1f;
            Vector3 position = EvaluatePosition(distanceAlongPath) + right * (footSeparation * 0.5f * side);
            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

            painter.Stamp(position, yaw, stampScale);
            _stepIndex++;
        }

        private Vector3 EvaluatePosition(float distance)
        {
            float radius = Mathf.Max(0.5f, pathRadius);
            float angle = distance / radius;
            return pathOffset + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        private Vector3 EvaluateForward(float distance)
        {
            float radius = Mathf.Max(0.5f, pathRadius);
            float angle = distance / radius;
            return new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        }
    }
}
