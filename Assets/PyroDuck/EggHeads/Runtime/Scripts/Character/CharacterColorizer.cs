using System.Collections.Generic;
using com.pyroduck.eggheads.Runtime.Scripts.Character;
using com.pyroduck.eggheads.Runtime.Scripts.Events;
using com.pyroduck.eggheads.Runtime.Scripts.Combat;
using System.Linq;
using com.pyroduck.eggheads.Runtime.Scripts.Enums;
using UnityEngine;

namespace com.pyroduck.eggheads.Runtime.Scripts.Character
{
    public class CharacterColorizer : MonoBehaviour
    {
        public List<BodyPart> bodyParts;

        private CharacterVisualController _visualController;
        private readonly Dictionary<VisualType, List<SpriteRenderer>> _renderersByType = new();
        private readonly Dictionary<VisualType, Color> _colorsByType = new();

        private void Awake()
        {
            _visualController = GetComponent<CharacterVisualController>();
        }

        private void OnValidate()
        {
            // OnValidate is called on the loading thread and for prefab assets. Avoid
            // mutating scene/prefab state here; only push cached colours to already-
            // bound renderers. Do not touch the serialized bodyParts list.
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // Defer to the next editor tick so we never write during asset import.
                UnityEditor.EditorApplication.delayCall += SafeApplyColorsEditor;
                return;
            }
#endif
            ApplyColorsInPlace();
        }

#if UNITY_EDITOR
        private void SafeApplyColorsEditor()
        {
            if (this == null) return;
            ApplyColorsInPlace();
        }
#endif

        /// <summary>
        /// Pushes the currently configured bodyPart colours onto their bound renderers
        /// without allocating or mutating the bodyParts list itself. Safe to call from
        /// OnValidate-style contexts.
        /// </summary>
        private void ApplyColorsInPlace()
        {
            if (bodyParts == null) return;

            foreach (BodyPart bodyPart in bodyParts)
            {
                if (bodyPart == null || bodyPart.visuals == null) continue;

                foreach (SpriteRenderer v in bodyPart.visuals)
                {
                    if (v != null)
                        v.color = bodyPart.color;
                }
            }
        }

        public void RefreshForVisualType(VisualType type)
        {
            if (_visualController == null)
            {
                _visualController = GetComponent<CharacterVisualController>();
            }

            if (_visualController == null)
            {
                return;
            }

            GameObject current = _visualController.GetCurrentVisual(type);
            if (current == null)
            {
                _renderersByType.Remove(type);
                return;
            }

            List<SpriteRenderer> renderers = current.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(s => s != null)
                .ToList();

            _renderersByType[type] = renderers;

            if (!_colorsByType.ContainsKey(type) && renderers.Count > 0)
            {
                _colorsByType[type] = renderers[0].color;
            }

            ApplyColorsForRuntimeType(type);
        }

        public void SetColorForVisualType(VisualType type, Color color)
        {
            _colorsByType[type] = color;
            ApplyColorsForRuntimeType(type);
        }

        public Color GetColorForVisualType(VisualType type)
        {
            if (_colorsByType.TryGetValue(type, out Color stored))
            {
                return stored;
            }

            if (_renderersByType.TryGetValue(type, out List<SpriteRenderer> list) &&
                list != null && list.Count > 0 && list[0] != null)
            {
                return list[0].color;
            }

            return Color.white;
        }

        private void ApplyColorsForRuntimeType(VisualType type)
        {
            if (!_renderersByType.TryGetValue(type, out List<SpriteRenderer> list) || list == null)
            {
                return;
            }

            Color color = _colorsByType.TryGetValue(type, out Color c) ? c : Color.white;
            bodyParts ??= new List<BodyPart>();

            if (bodyParts.Count(b => b.name == type.ToString()) > 0)
            {
                var bodyPart = bodyParts.Find(b => b.name == type.ToString());
                bodyPart.visuals = list;
                bodyPart.color = color;
            }
            else
            {
                bodyParts.Add(new BodyPart { name = type.ToString(), visuals = list, color = color });
            }

            foreach (SpriteRenderer spriteRenderer in list)
            {
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = color;
                }
            }
        }

        public void ApplyColors()
        {
            foreach (VisualType key in _renderersByType.Keys.ToList())
            {
                ApplyColorsForRuntimeType(key);
            }

            if (bodyParts == null || bodyParts.Count == 0)
            {
                return;
            }

            foreach (BodyPart bodyPart in bodyParts)
            {
                if (bodyPart.visuals == null)
                {
                    continue;
                }

                foreach (SpriteRenderer v in bodyPart.visuals)
                {
                    if (v != null)
                    {
                        v.color = bodyPart.color;
                    }
                }
            }
        }
    }

    [System.Serializable]
    public class BodyPart
    {
        public string name;
        public List<SpriteRenderer> visuals;
        public Color color = Color.white;
    }
}
