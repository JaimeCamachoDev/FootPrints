using UnityEngine;

namespace Footprints
{
    /// <summary>
    /// Keeps a renderer material property block in sync with a <see cref="FootprintPainterRT"/>.
    /// This is meant for Shader Graph setups where the footprint mask is sampled via material properties
    /// rather than global shader parameters.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Renderer))]
    [DisallowMultipleComponent]
    public sealed class FootprintTerrainBinder : MonoBehaviour
    {
        [SerializeField] private FootprintPainterRT painter;
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private string maskPropertyName = "_FootMask";
        [SerializeField] private string tilePropertyName = "_FootTileOriginSize";

        private FootprintPainterRT _registeredPainter;
        private MaterialPropertyBlock _propertyBlock;
        private RenderTexture _lastMask;
        private Vector4 _lastTileData = new(float.NaN, float.NaN, float.NaN, float.NaN);
        private int _maskPropertyId;
        private int _tilePropertyId;
        private string _maskPropertyCache;
        private string _tilePropertyCache;

        private void Reset()
        {
            painter = GetComponent<FootprintPainterRT>();
            targetRenderer = GetComponent<Renderer>();
        }

        private void Awake()
        {
            EnsureRenderer();
            UpdatePropertyIds();
        }

        private void OnEnable()
        {
            EnsureRenderer();
            UpdatePropertyIds();
            BindPainter(painter);
            ForceApply();
        }

        private void OnDisable()
        {
            UnbindPainter();
        }

        private void OnValidate()
        {
            EnsureRenderer();
            UpdatePropertyIds();
            if (!Application.isPlaying && isActiveAndEnabled)
            {
                BindPainter(painter);
                ForceApply();
            }
        }

        private void LateUpdate()
        {
            ApplyIfNeeded(false);
        }

        private void OnDestroy()
        {
            UnbindPainter();
        }

        /// <summary>
        /// Binds this binder to a different painter at runtime.
        /// </summary>
        public void BindPainter(FootprintPainterRT newPainter)
        {
            EnsureRenderer();

            if (newPainter == null)
            {
                newPainter = painter != null ? painter : GetComponent<FootprintPainterRT>();
            }

            if (_registeredPainter == newPainter)
            {
                return;
            }

            UnbindPainter();

            _registeredPainter = newPainter;
            if (_registeredPainter != null)
            {
                _registeredPainter.RegisterMaskListener(OnMaskUpdated);
            }

            ForceApply();
        }

        private void UnbindPainter()
        {
            if (_registeredPainter != null)
            {
                _registeredPainter.UnregisterMaskListener(OnMaskUpdated);
                _registeredPainter = null;
            }
        }

        private void OnMaskUpdated(RenderTexture _)
        {
            ForceApply();
        }

        private void EnsureRenderer()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            if (painter == null)
            {
                painter = GetComponent<FootprintPainterRT>();
            }
        }

        private void UpdatePropertyIds()
        {
            if (maskPropertyName != _maskPropertyCache)
            {
                _maskPropertyCache = maskPropertyName;
                _maskPropertyId = string.IsNullOrEmpty(maskPropertyName) ? -1 : Shader.PropertyToID(maskPropertyName);
            }

            if (tilePropertyName != _tilePropertyCache)
            {
                _tilePropertyCache = tilePropertyName;
                _tilePropertyId = string.IsNullOrEmpty(tilePropertyName) ? -1 : Shader.PropertyToID(tilePropertyName);
            }
        }

        private void ForceApply()
        {
            _lastMask = null;
            _lastTileData = new Vector4(float.NaN, float.NaN, float.NaN, float.NaN);
            ApplyIfNeeded(true);
        }

        private void ApplyIfNeeded(bool force)
        {
            if (targetRenderer == null)
            {
                return;
            }

            RenderTexture mask = _registeredPainter != null ? _registeredPainter.MaskTexture : null;
            Vector4 tileData = _registeredPainter != null ? _registeredPainter.TileOriginSizeVector : Vector4.zero;

            bool maskChanged = mask != _lastMask;
            bool tileChanged = tileData != _lastTileData;

            if (!force && !maskChanged && !tileChanged)
            {
                return;
            }

            _lastMask = mask;
            _lastTileData = tileData;

            _propertyBlock ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(_propertyBlock);

            if (_maskPropertyId >= 0)
            {
                if (mask != null)
                {
                    _propertyBlock.SetTexture(_maskPropertyId, mask);
                }
                else
                {
                    _propertyBlock.SetTexture(_maskPropertyId, Texture2D.blackTexture);
                }
            }

            if (_tilePropertyId >= 0)
            {
                _propertyBlock.SetVector(_tilePropertyId, tileData);
            }

            targetRenderer.SetPropertyBlock(_propertyBlock);
        }
    }
}

