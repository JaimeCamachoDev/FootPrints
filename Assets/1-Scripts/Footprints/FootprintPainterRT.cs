using System;
using System.Collections.Generic;
using UnityEngine;

namespace Footprints
{
    /// <summary>
    /// Paints footprints into a RenderTexture tile that can be sampled by the ground shader.
    /// The mask is written in GPU to allow thousands of imprints with a single draw on the floor.
    /// </summary>
    [DisallowMultipleComponent]
    public class FootprintPainterRT : MonoBehaviour
    {
        private const string StampShaderName = "Hidden/Footprints/Stamp";
        private const string FadeShaderName = "Hidden/Footprints/Fade";

        private static readonly int FootMaskId = Shader.PropertyToID("_FootMask");
        private static readonly int FootTileOriginSizeId = Shader.PropertyToID("_FootTileOriginSize");

        private static readonly int StampTextureId = Shader.PropertyToID("_StampTex");
        private static readonly int StampCenterScaleId = Shader.PropertyToID("_StampCenterScale");
        private static readonly int StampRotationStrengthId = Shader.PropertyToID("_StampRotationStrength");
        private static readonly int FadeAmountId = Shader.PropertyToID("_Fade");

        [Header("Tile")]
        [SerializeField] private Vector2 tileOrigin = Vector2.zero;
        [SerializeField, Min(0.1f)] private float tileSize = 50f;
        [SerializeField, Range(128, 4096)] private int resolution = 1024;
        [SerializeField] private FilterMode filterMode = FilterMode.Bilinear;
        [SerializeField] private bool clearOnRecenter = true;

        [Header("Tracking")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private bool snapOriginToTile = true;

        [Header("Stamp")]
        [SerializeField] private Texture2D footprintStamp;
        [SerializeField, Range(0f, 1f)] private float defaultStrength = 1f;

        [Header("Fade")]
        [SerializeField, Tooltip("Units per second removed from the mask (0 = disabled).")]
        private float fadePerSecond = 0f;
        [SerializeField] private float fadeInterval = 0.25f;

        private RenderTexture _mask;
        private RenderTextureFormat _maskFormat = RenderTextureFormat.R8;
        private Material _stampMaterial;
        private Material _fadeMaterial;
        private Texture2D _runtimeStamp;
        private float _fadeTimer;

        private readonly List<Action<RenderTexture>> _onMaskReady = new();

        /// <summary>
        /// Current world origin (bottom-left corner) of the active tile.
        /// </summary>
        public Vector2 TileOrigin => tileOrigin;

        /// <summary>
        /// Current RenderTexture that stores the footprint mask.
        /// </summary>
        public RenderTexture MaskTexture => _mask;

        /// <summary>
        /// Size in world units covered by the active tile.
        /// </summary>
        public float TileSize => tileSize;

        /// <summary>
        /// Convenience vector containing origin.xy and size.zw for shader consumption.
        /// </summary>
        public Vector4 TileOriginSizeVector => new Vector4(tileOrigin.x, tileOrigin.y, tileSize, tileSize);

        private void OnEnable()
        {
            EnsureResources();
            UpdateShaderGlobals();
        }

        private void OnDisable()
        {
            ReleaseResources();
        }

        private void LateUpdate()
        {
            if (followTarget != null)
            {
                var desiredOrigin = CalculateTargetOrigin(followTarget.position);
                if (desiredOrigin != tileOrigin)
                {
                    SetTileOrigin(desiredOrigin, clearOnRecenter);
                }
            }

            if (fadePerSecond > 0f && _mask != null)
            {
                _fadeTimer += Time.deltaTime;
                if (_fadeTimer >= fadeInterval)
                {
                    float fadeAmount = fadePerSecond * _fadeTimer;
                    _fadeTimer = 0f;
                    ApplyFade(fadeAmount);
                }
            }
        }

        private void OnValidate()
        {
            tileSize = Mathf.Max(0.1f, tileSize);
            resolution = Mathf.Clamp(resolution, 128, 4096);
            fadeInterval = Mathf.Max(0.01f, fadeInterval);

            if (isActiveAndEnabled)
            {
                EnsureResources();
                UpdateShaderGlobals();
            }
        }

        private void EnsureResources()
        {
            RenderTextureFormat desiredFormat = ChooseSupportedFormat();

            if (_mask == null || _mask.width != resolution || _mask.height != resolution || _maskFormat != desiredFormat)
            {
                ReleaseMask();
                _mask = CreateMask(desiredFormat);
                if (_mask == null)
                {
                    return;
                }
                _maskFormat = _mask.format;
                ClearMask();
            }
            else
            {
                _mask.filterMode = filterMode;
            }

            if (_stampMaterial == null)
            {
                var shader = Shader.Find(StampShaderName);
                if (shader == null)
                {
                    Debug.LogError($"FootprintPainterRT could not find shader '{StampShaderName}'.");
                }
                else
                {
                    _stampMaterial = new Material(shader);
                }
            }

            if (_fadeMaterial == null)
            {
                var shader = Shader.Find(FadeShaderName);
                if (shader == null)
                {
                    Debug.LogError($"FootprintPainterRT could not find shader '{FadeShaderName}'.");
                }
                else
                {
                    _fadeMaterial = new Material(shader);
                }
            }

            EnsureStampTexture();
        }

        private void ReleaseResources()
        {
            ReleaseMask();

            if (_stampMaterial != null)
            {
                DestroyImmediate(_stampMaterial);
                _stampMaterial = null;
            }

            if (_fadeMaterial != null)
            {
                DestroyImmediate(_fadeMaterial);
                _fadeMaterial = null;
            }

            if (_runtimeStamp != null)
            {
                DestroyImmediate(_runtimeStamp);
                _runtimeStamp = null;
            }
        }

        private void ReleaseMask()
        {
            if (_mask != null)
            {
                if (_mask.IsCreated())
                {
                    _mask.Release();
                }

                DestroyImmediate(_mask);
                _mask = null;
            }

            _maskFormat = RenderTextureFormat.R8;
        }

        private RenderTextureFormat ChooseSupportedFormat()
        {
            if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8))
            {
                return RenderTextureFormat.R8;
            }

            if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RHalf))
            {
                return RenderTextureFormat.RHalf;
            }

            if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R16))
            {
                return RenderTextureFormat.R16;
            }

            return RenderTextureFormat.ARGB32;
        }

        private RenderTexture CreateMask(RenderTextureFormat format)
        {
            var renderTexture = new RenderTexture(resolution, resolution, 0, format)
            {
                name = $"FootprintMask_{name}",
                antiAliasing = 1,
                enableRandomWrite = false,
                useMipMap = false,
                filterMode = filterMode,
                wrapMode = TextureWrapMode.Clamp
            };

            if (!renderTexture.Create())
            {
                Debug.LogWarning($"FootprintPainterRT failed to create mask using format {format}. Falling back to a supported format.");
                DestroyImmediate(renderTexture);

                if (format == RenderTextureFormat.ARGB32)
                {
                    Debug.LogError("FootprintPainterRT could not create a compatible render texture for the footprint mask.");
                    return null;
                }

                RenderTextureFormat fallback = RenderTextureFormat.ARGB32;
                return CreateMask(fallback);
            }

            return renderTexture;
        }

        /// <summary>
        /// Clears the mask texture to zero (no footprints).
        /// </summary>
        public void ClearMask()
        {
            if (_mask == null)
            {
                return;
            }

            var active = RenderTexture.active;
            RenderTexture.active = _mask;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = active;

            NotifyMaskReady();
        }

        /// <summary>
        /// Repositions the tile and optionally clears the existing mask.
        /// </summary>
        public void SetTileOrigin(Vector2 newOrigin, bool clear)
        {
            tileOrigin = newOrigin;
            if (clear)
            {
                ClearMask();
            }

            UpdateShaderGlobals();
            NotifyMaskReady();
        }

        /// <summary>
        /// Draws a footprint onto the mask at the provided world position.
        /// </summary>
        /// <param name="worldPos">World position of the centre of the stamp.</param>
        /// <param name="yawDegrees">Rotation in degrees on the Y axis.</param>
        /// <param name="scale">World size of the stamp on X/Z (meters).</param>
        /// <param name="strength">Amount added to the mask (0-1).</param>
        public void Stamp(Vector3 worldPos, float yawDegrees, Vector2 scale, float strength = -1f)
        {
            if (_mask == null || _stampMaterial == null)
            {
                EnsureResources();
                if (_mask == null || _stampMaterial == null)
                {
                    return;
                }
            }

            EnsureStampTexture();

            float strengthToApply = strength < 0f ? defaultStrength : strength;
            strengthToApply = Mathf.Clamp01(strengthToApply);

            var cos = Mathf.Cos(yawDegrees * Mathf.Deg2Rad);
            var sin = Mathf.Sin(yawDegrees * Mathf.Deg2Rad);

            _stampMaterial.SetVector(StampCenterScaleId, new Vector4(worldPos.x, worldPos.z, Mathf.Max(0.001f, scale.x), Mathf.Max(0.001f, scale.y)));
            _stampMaterial.SetVector(StampRotationStrengthId, new Vector4(cos, sin, strengthToApply, 0f));
            _stampMaterial.SetVector(FootTileOriginSizeId, new Vector4(tileOrigin.x, tileOrigin.y, tileSize, tileSize));

            var temp = RenderTexture.GetTemporary(_mask.width, _mask.height, 0, _mask.format);
            Graphics.Blit(_mask, temp);
            Graphics.Blit(temp, _mask, _stampMaterial, 0);
            RenderTexture.ReleaseTemporary(temp);

            UpdateShaderGlobals();
            NotifyMaskReady();
        }

        /// <summary>
        /// Convenience overload with uniform scale.
        /// </summary>
        public void Stamp(Vector3 worldPos, float yawDegrees, float uniformScale, float strength = -1f)
        {
            Stamp(worldPos, yawDegrees, new Vector2(uniformScale, uniformScale), strength);
        }

        /// <summary>
        /// Registers a callback fired whenever the mask is updated.
        /// </summary>
        public void RegisterMaskListener(Action<RenderTexture> callback)
        {
            if (callback == null)
            {
                return;
            }

            if (!_onMaskReady.Contains(callback))
            {
                _onMaskReady.Add(callback);
                if (_mask != null)
                {
                    callback.Invoke(_mask);
                }
            }
        }

        /// <summary>
        /// Removes a previously registered callback.
        /// </summary>
        public void UnregisterMaskListener(Action<RenderTexture> callback)
        {
            if (callback == null)
            {
                return;
            }

            _onMaskReady.Remove(callback);
        }

        private void NotifyMaskReady()
        {
            if (_onMaskReady.Count == 0 || _mask == null)
            {
                return;
            }

            for (int i = 0; i < _onMaskReady.Count; i++)
            {
                _onMaskReady[i]?.Invoke(_mask);
            }
        }

        private void ApplyFade(float fadeAmount)
        {
            if (_mask == null || _fadeMaterial == null)
            {
                return;
            }

            fadeAmount = Mathf.Clamp01(fadeAmount);
            if (fadeAmount <= 0f)
            {
                return;
            }

            _fadeMaterial.SetFloat(FadeAmountId, fadeAmount);

            var temp = RenderTexture.GetTemporary(_mask.width, _mask.height, 0, _mask.format);
            Graphics.Blit(_mask, temp, _fadeMaterial, 0);
            Graphics.Blit(temp, _mask);
            RenderTexture.ReleaseTemporary(temp);

            NotifyMaskReady();
        }

        private void UpdateShaderGlobals()
        {
            if (_mask == null)
            {
                return;
            }

            Shader.SetGlobalTexture(FootMaskId, _mask);
            Shader.SetGlobalVector(FootTileOriginSizeId, new Vector4(tileOrigin.x, tileOrigin.y, tileSize, tileSize));
        }

        private void EnsureStampTexture()
        {
            if (_stampMaterial == null)
            {
                return;
            }

            Texture stamp = footprintStamp;
            if (stamp == null)
            {
                if (_runtimeStamp == null)
                {
                    _runtimeStamp = GenerateRuntimeStampTexture(128);
                }

                stamp = _runtimeStamp;
            }

            _stampMaterial.SetTexture(StampTextureId, stamp);
        }

        private static Texture2D GenerateRuntimeStampTexture(int size)
        {
            size = Mathf.Clamp(size, 32, 512);
            var texture = new Texture2D(size, size, TextureFormat.R8, false, true)
            {
                name = "FootprintStampRuntime",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };

            float invSize = 1f / (size - 1f);
            Vector2 heelCenter = new Vector2(0f, -0.45f);
            Vector2 toeCenter = new Vector2(0f, 0.35f);
            Vector2 invHeelScale = new Vector2(1f / 0.6f, 1f / 0.55f);
            Vector2 invToeScale = new Vector2(1f / 0.45f, 1f / 0.45f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x * invSize * 2f - 1f;
                    float v = y * invSize * 2f - 1f;

                    Vector2 sample = new Vector2(u, v);
                    float heel = Mathf.Clamp01(1f - Vector2.Scale(sample - heelCenter, invHeelScale).magnitude);
                    float toe = Mathf.Clamp01(1f - Vector2.Scale(sample - toeCenter, invToeScale).magnitude);

                    float value = Mathf.Pow(Mathf.Max(heel, toe), 2f);
                    texture.SetPixel(x, y, new Color(value, value, value, value));
                }
            }

            texture.Apply();
            return texture;
        }

        private Vector2 CalculateTargetOrigin(Vector3 worldPos)
        {
            if (!snapOriginToTile)
            {
                return new Vector2(worldPos.x - tileSize * 0.5f, worldPos.z - tileSize * 0.5f);
            }

            float snappedX = Mathf.Floor(worldPos.x / tileSize) * tileSize;
            float snappedZ = Mathf.Floor(worldPos.z / tileSize) * tileSize;
            return new Vector2(snappedX, snappedZ);
        }
    }
}
