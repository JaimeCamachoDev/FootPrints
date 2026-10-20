using System;
using System.Collections.Generic;
using UnityEngine;

namespace Footprints
{
    /// <summary>
    /// Draws footprint decals as instanced quads. Ideal for prototypes or mid-sized crowds.
    /// </summary>
    [DisallowMultipleComponent]
    public class FootprintInstancedPool : MonoBehaviour
    {
        private const int MaxBatchSize = 1023;

        [Serializable]
        public struct FootprintInstance
        {
            public Vector3 position;
            public float yaw;
            public Vector2 scale;
            public Vector4 uvRect;
            public Color color;
            public float lifetime;
            public float age;

            public bool IsAlive(float sqCullDistance, Vector3 referencePosition)
            {
                if (lifetime > 0f && age >= lifetime)
                {
                    return false;
                }

                if (sqCullDistance > 0f && (position - referencePosition).sqrMagnitude > sqCullDistance)
                {
                    return false;
                }

                return true;
            }

            public float GetAlpha()
            {
                if (lifetime <= 0f)
                {
                    return color.a;
                }

                float remaining = Mathf.Clamp01(1f - age / lifetime);
                return color.a * remaining;
            }
        }

        [Header("Rendering")]
        [SerializeField] private Mesh quadMesh;
        [SerializeField] private Material decalMaterial;
        [SerializeField, Range(1, 10000)] private int maxInstances = 2048;
        [SerializeField] private float globalAlpha = 1f;

        [Header("Culling")]
        [SerializeField] private Transform cullOrigin;
        [SerializeField] private float cullDistance = 15f;

        private readonly List<FootprintInstance> _instances = new();
        private Matrix4x4[] _matrices = new Matrix4x4[MaxBatchSize];
        private readonly List<Vector4> _uvRects = new(MaxBatchSize);
        private readonly List<Vector4> _colors = new(MaxBatchSize);

        private MaterialPropertyBlock _propertyBlock;

        /// <summary>
        /// Adds a new footprint to the pool, replacing the oldest if the capacity is full.
        /// </summary>
        public void AddFootprint(Vector3 position, float yawDegrees, Vector2 scale, Vector4 uvRect, Color color, float lifetime = 4f)
        {
            if (quadMesh == null || decalMaterial == null)
            {
                Debug.LogWarning("FootprintInstancedPool is missing mesh or material.");
                return;
            }

            if (_instances.Count == maxInstances)
            {
                _instances.RemoveAt(0);
            }

            _instances.Add(new FootprintInstance
            {
                position = position,
                yaw = yawDegrees,
                scale = scale,
                uvRect = uvRect,
                color = color,
                lifetime = lifetime,
                age = 0f
            });
        }

        /// <summary>
        /// Clears all active footprints.
        /// </summary>
        public void Clear()
        {
            _instances.Clear();
        }

        private void Awake()
        {
            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            if (decalMaterial != null && !decalMaterial.enableInstancing)
            {
                decalMaterial.enableInstancing = true;
            }
        }

        private void OnValidate()
        {
            maxInstances = Mathf.Clamp(maxInstances, 1, 10000);

            if (_instances.Count > maxInstances)
            {
                int removeCount = _instances.Count - maxInstances;
                _instances.RemoveRange(0, removeCount);
            }

            if (decalMaterial != null && !decalMaterial.enableInstancing)
            {
                decalMaterial.enableInstancing = true;
            }
        }

        private void LateUpdate()
        {
            float dt = Application.isPlaying ? Time.deltaTime : 0f;
            float sqCull = cullDistance > 0f ? cullDistance * cullDistance : -1f;
            Vector3 origin = cullOrigin != null ? cullOrigin.position : transform.position;

            for (int i = _instances.Count - 1; i >= 0; i--)
            {
                var instance = _instances[i];
                instance.age += dt;
                if (!instance.IsAlive(sqCull, origin))
                {
                    _instances.RemoveAt(i);
                    continue;
                }

                _instances[i] = instance;
            }

            RenderInstances();
        }

        private void RenderInstances()
        {
            if (_instances.Count == 0 || quadMesh == null || decalMaterial == null)
            {
                return;
            }

            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            int drawn = 0;
            while (drawn < _instances.Count)
            {
                int batchCount = Mathf.Min(MaxBatchSize, _instances.Count - drawn);

                _uvRects.Clear();
                _colors.Clear();

                for (int i = 0; i < batchCount; i++)
                {
                    var instance = _instances[drawn + i];
                    Quaternion rotation = Quaternion.Euler(0f, instance.yaw, 0f);
                    Vector3 scale = new Vector3(Mathf.Max(0.001f, instance.scale.x), 1f, Mathf.Max(0.001f, instance.scale.y));

                    _matrices[i] = Matrix4x4.TRS(instance.position, rotation, scale);
                    _uvRects.Add(instance.uvRect);

                    Color col = instance.color;
                    col.a = instance.GetAlpha();
                    _colors.Add(col);
                }

                _propertyBlock.SetVectorArray("_UvRect", _uvRects);
                _propertyBlock.SetVectorArray("_InstanceColor", _colors);
                _propertyBlock.SetFloat("_GlobalAlpha", Mathf.Clamp01(globalAlpha));

                Graphics.DrawMeshInstanced(
                    quadMesh,
                    0,
                    decalMaterial,
                    _matrices,
                    batchCount,
                    _propertyBlock,
                    UnityEngine.Rendering.ShadowCastingMode.Off,
                    false,
                    gameObject.layer,
                    null,
                    UnityEngine.Rendering.LightProbeUsage.Off,
                    null);

                drawn += batchCount;
            }
        }
    }
}
