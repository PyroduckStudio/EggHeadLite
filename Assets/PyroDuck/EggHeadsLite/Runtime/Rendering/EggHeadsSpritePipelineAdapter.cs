using UnityEngine;

namespace com.pyroduck.eggheadslite.Rendering
{
    // Pass sprite tint and atlas textures to the HDRP shader without material instances.
    // SpriteRenderer continues to handle geometry flipping.
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
    public sealed class EggHeadsSpritePipelineAdapter : MonoBehaviour
    {
        public const string ShaderName = "PyroDuck/EggHeads/HDRP Sprite Unlit";
        private static readonly int ColorId = Shader.PropertyToID("_EggHeadsRendererColor");
        private static readonly int SpriteId = Shader.PropertyToID("_EggHeadsIsSprite");
        private static readonly int TextureId = Shader.PropertyToID("_MainTex");
        private static readonly int AlphaId = Shader.PropertyToID("_AlphaTex");
        private static readonly int EnableAlphaId = Shader.PropertyToID("_EnableExternalAlpha");
        private SpriteRenderer _renderer;
        private MaterialPropertyBlock _properties;
        private Shader _lastShader;
        private bool _usesHDRPSpriteShader;

        private void OnEnable() { ApplyProperties(); }
        private void LateUpdate() { ApplyProperties(); }

        public void ApplyProperties()
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            var material = _renderer != null ? _renderer.sharedMaterial : null;
            var shader = material != null ? material.shader : null;
            if (shader != _lastShader)
            {
                _lastShader = shader;
                _usesHDRPSpriteShader = shader != null && shader.name == ShaderName;
            }
            if (!_usesHDRPSpriteShader || shader == null) return;
            _properties ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_properties);
            var texture = _renderer.sprite != null ? _renderer.sprite.texture : Texture2D.whiteTexture;
            var splitAlpha = _renderer.sprite != null ? _renderer.sprite.associatedAlphaSplitTexture : null;
            var alpha = splitAlpha != null ? splitAlpha : Texture2D.whiteTexture;
            float enableAlpha = splitAlpha != null ? 1 : 0;
            if (_properties.GetColor(ColorId) == _renderer.color && _properties.GetFloat(SpriteId) == 1 &&
                _properties.GetTexture(TextureId) == texture && _properties.GetTexture(AlphaId) == alpha && _properties.GetFloat(EnableAlphaId) == enableAlpha) return;
            _properties.SetColor(ColorId, _renderer.color);
            _properties.SetFloat(SpriteId, 1);
            _properties.SetTexture(TextureId, texture);
            _properties.SetTexture(AlphaId, alpha);
            _properties.SetFloat(EnableAlphaId, enableAlpha);
            _renderer.SetPropertyBlock(_properties);
        }
    }
}
