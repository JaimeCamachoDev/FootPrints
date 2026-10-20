using UnityEngine;

namespace Footprints
{
    /// <summary>
    /// Simple demo walker that can either follow an internal circular path or rely on external motion
    /// while stamping footsteps into a <see cref="FootprintPainterRT"/>.
    /// Designed for the Footprints RT sample scene but flexible enough for spline-driven motion.
    /// </summary>
    public class FootprintDemoWalker : MonoBehaviour
    {
        private enum MovementMode
        {
            ProceduralCircle,
            ExternalTransform
        }

        [Header("Stamping")]
        [SerializeField] private FootprintPainterRT painter;
        [SerializeField, Min(0.1f)] private float stepSpacing = 0.5f;
        [SerializeField, Min(0f)] private float footSeparation = 0.35f;
        [SerializeField] private Vector2 stampScale = new Vector2(0.35f, 0.6f);
        [SerializeField] private bool projectToGround = true;
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField, Min(0.01f)] private float projectionRayHeight = 1f;
        [SerializeField, Min(0.01f)] private float projectionRayDepth = 3f;
        [SerializeField] private float projectionSurfaceOffset = 0.005f;

        [Header("Movement")]
        [SerializeField] private MovementMode movementMode = MovementMode.ProceduralCircle;
        [SerializeField, Min(0.1f)] private float moveSpeed = 1.6f;
        [SerializeField, Min(0.5f)] private float pathRadius = 5f;
        [SerializeField] private Vector3 pathOffset = Vector3.zero;
        [SerializeField] private bool faceDirection = true;
        [SerializeField] private bool clearMaskOnStart = true;

        private float _travelDistance;
        private float _distanceSinceLastStep;
        private int _stepIndex;
        private Vector3 _previousPosition;
        private Vector3 _lastForward;

        private void Start()
        {
            if (clearMaskOnStart)
            {
                painter?.ClearMask();
            }

            _previousPosition = transform.position;
            _lastForward = new Vector3(transform.forward.x, 0f, transform.forward.z);
            if (!TryNormalize(ref _lastForward))
            {
                _lastForward = Vector3.forward;
            }
        }

        private void Update()
        {
            if (painter == null)
            {
                return;
            }

            Vector3 currentPosition = transform.position;
            Vector3 forward = transform.forward;
            bool hasForward = TryNormalize(ref forward);

            if (movementMode == MovementMode.ProceduralCircle)
            {
                float delta = moveSpeed * Time.deltaTime;
                _travelDistance += delta;

                currentPosition = EvaluatePosition(_travelDistance);
                Vector3 pathForward = EvaluateForward(_travelDistance);
                bool hasPathForward = TryNormalize(ref pathForward);

                transform.position = currentPosition;
                if (faceDirection && hasPathForward)
                {
                    transform.rotation = Quaternion.LookRotation(pathForward, Vector3.up);
                    forward = pathForward;
                    hasForward = true;
                }
            }

            Vector3 horizontalDelta = new Vector3(currentPosition.x - _previousPosition.x, 0f, currentPosition.z - _previousPosition.z);
            float frameDistance = horizontalDelta.magnitude;
            if (frameDistance > 0.0001f)
            {
                forward = horizontalDelta / frameDistance;
                hasForward = true;

                if (movementMode == MovementMode.ExternalTransform && faceDirection)
                {
                    transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
                }
            }
            else if (!hasForward)
            {
                forward = _lastForward;
                hasForward = TryNormalize(ref forward);
            }

            if (hasForward)
            {
                _lastForward = forward;
            }

            if (movementMode != MovementMode.ProceduralCircle)
            {
                _travelDistance += frameDistance;
            }
            _distanceSinceLastStep += frameDistance;

            while (_distanceSinceLastStep >= stepSpacing && hasForward)
            {
                _distanceSinceLastStep -= stepSpacing;
                Vector3 stepCenter = currentPosition - forward * _distanceSinceLastStep;
                EmitFootstep(stepCenter, forward);
            }

            _previousPosition = currentPosition;
        }

        private void EmitFootstep(Vector3 center, Vector3 forward)
        {
            forward.y = 0f;
            if (!TryNormalize(ref forward))
            {
                forward = Vector3.forward;
            }

            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            if (!TryNormalize(ref right))
            {
                right = Vector3.right;
            }

            float side = (_stepIndex & 1) == 0 ? 1f : -1f;
            Vector3 position = center + right * (footSeparation * 0.5f * side);

            Vector3 projectedForward = forward;
            if (projectToGround)
            {
                TryProjectToGround(position, forward, out position, out projectedForward);
            }

            float yaw = Mathf.Atan2(projectedForward.x, projectedForward.z) * Mathf.Rad2Deg;

            painter.Stamp(position, yaw, stampScale);
            _stepIndex++;
        }

        private bool TryProjectToGround(Vector3 position, Vector3 forward, out Vector3 projectedPosition, out Vector3 projectedForward)
        {
            float castDistance = projectionRayHeight + projectionRayDepth;
            Vector3 origin = position + Vector3.up * projectionRayHeight;

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, castDistance, groundLayers, QueryTriggerInteraction.Ignore))
            {
                projectedPosition = hit.point + hit.normal * projectionSurfaceOffset;
                projectedForward = Vector3.ProjectOnPlane(forward, hit.normal);
                if (!TryNormalize(ref projectedForward))
                {
                    projectedForward = forward;
                }

                return true;
            }

            projectedPosition = position;
            projectedForward = forward;
            return false;
        }

        private static bool TryNormalize(ref Vector3 vector)
        {
            float magnitudeSquared = vector.sqrMagnitude;
            if (magnitudeSquared < 0.0001f)
            {
                vector = Vector3.zero;
                return false;
            }

            vector /= Mathf.Sqrt(magnitudeSquared);
            return true;
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
