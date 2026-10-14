using System.Collections.Generic;
using com.pyroduck.eggheads.Runtime.Scripts.Data;
using com.pyroduck.eggheads.Runtime.Scripts.Enums;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Animations;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace com.pyroduck.eggheads.Runtime.Scripts.Character
{
    public class CharacterVisualController : MonoBehaviour
    {
        private const string EyesPlaceName = "EyesPlace";
        private const string MouthPlaceName = "MouthPlace";
        private const string HatPlaceName = "HatPlace";
        private const string MoustachePlaceName = "BreadMustachePlace";
        private const string NosePlaceName = "NosePlace";
        private const string EyebrowPlaceName = "EyeBrowPlace";
        private const string WeaponPlaceName = "WeaponPlace";

        public Transform BodyRoot;
        public Transform BodyVisual;
        public GameObject BodyParent;
        public GameObject RotatingBodyParent;
        public GameObject DeathEyesParent;
        public GameObject EyesParent;
        public List<VisualMapping> VisualMappings;

        private Transform _animatedBodyScaleSource;

        public void SetAnimatedBody(Transform animatedBodyParent)
        {
            if (BodyParent == null)
                return;

            _animatedBodyScaleSource = animatedBodyParent;

            var rotationConstraint = BodyParent.GetComponent<RotationConstraint>()
                                     ?? BodyParent.AddComponent<RotationConstraint>();
            var scaleConstraint = BodyParent.GetComponent<ScaleConstraint>()
                                  ?? BodyParent.AddComponent<ScaleConstraint>();

            if (rotationConstraint != null)
                SetConstraintSource(rotationConstraint, animatedBodyParent);

            if (scaleConstraint != null)
            {
                // X is reserved for facing on BodyRoot. Y/Z remain animation-driven.
                scaleConstraint.scalingAxis = Axis.Y | Axis.Z;
                SetConstraintSource(scaleConstraint, animatedBodyParent);
            }

            SyncAnimatedBodyScaleX();
        }

        private void LateUpdate()
        {
            SyncAnimatedBodyScaleX();
        }

        private void SyncAnimatedBodyScaleX()
        {
            if (_animatedBodyScaleSource == null || BodyParent == null)
                return;

            var scale = BodyParent.transform.localScale;
            scale.x = Mathf.Abs(_animatedBodyScaleSource.localScale.x);
            BodyParent.transform.localScale = scale;
        }

        public void SetDeathEyes(bool isDead)
        {
            if (DeathEyesParent != null)
                DeathEyesParent.SetActive(isDead);

            if (EyesParent != null)
                EyesParent.SetActive(!isDead);
        }

        /// <summary>
        /// Preferred API — attaches a <see cref="VisualInstanceTag"/> on the spawned
        /// instance so <c>CharacterSerializer</c> can round-trip the exact source asset.
        /// </summary>
        public void CreateItem([CanBeNull] VisualDataSO data, VisualType visualType)
        {
            if (data == null)
            {
                CreateItem((GameObject)null, visualType);
                return;
            }

            CreateItem(data.BodyPartPrefab, visualType);
            var spawned = GetCurrentVisual(visualType);
            if (spawned != null)
            {
                var tag = spawned.GetComponent<VisualInstanceTag>();
                if (tag == null) tag = spawned.AddComponent<VisualInstanceTag>();
                tag.Bind(data);
            }
        }

        public void CreateItem(GameObject visualPrefab, VisualType visualType)
        {
            foreach (var visualMapping in VisualMappings)
            {
                if (visualMapping.Type == visualType)
                {
                    if (visualMapping.Parent == null)
                    {
                        Debug.LogWarning($"Parent transform for visual type {visualType} is not assigned.");
                    }
                    else
                    {
                        //Clear child in visualMapping.Parent
                        if (visualMapping.CurrentVisual != null)
                        {
                            DestroyVisual(visualMapping.CurrentVisual);
                            visualMapping.CurrentVisual = null;
                        }
                        else
                        {
                            if (visualMapping.Parent.childCount > 0)
                            {
                                foreach (Transform child in visualMapping.Parent)
                                {
                                    DestroyVisual(child.gameObject);
                                }
                            }
                        }

                        if (visualPrefab != null)
                        {
#if UNITY_EDITOR
                            visualMapping.CurrentVisual = !Application.isPlaying
                                ? (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, visualMapping.Parent)
                                : Instantiate(visualPrefab, visualMapping.Parent);
#else
                            visualMapping.CurrentVisual = Instantiate(visualPrefab, visualMapping.Parent);
#endif
                            visualMapping.CurrentVisual.transform.localPosition = Vector3.zero;
                        }
                    }

                    return;
                }
            }
        }

        private static void DestroyVisual(GameObject visual)
        {
            if (visual == null) return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(visual);
            else
                Destroy(visual);
#else
            Destroy(visual);
#endif
        }

        public GameObject GetCurrentVisual(VisualType visualType)
        {
            foreach (var visualMapping in VisualMappings)
            {
                if (visualMapping.Type == visualType)
                {
                    return visualMapping.CurrentVisual;
                }
            }

            return null;
        }

        public void SetRotatingBody(Transform rotatingBodyParent)
        {
            if (RotatingBodyParent == null)
                return;

            var rotationConstraint = RotatingBodyParent.GetComponent<RotationConstraint>()
                                     ?? RotatingBodyParent.AddComponent<RotationConstraint>();
            SetConstraintSource(rotationConstraint, rotatingBodyParent);
            RebindVisualAnchorConstraints();
        }

        public void RebindVisualAnchorConstraints()
        {
            if (RotatingBodyParent == null || VisualMappings == null)
                return;

            foreach (var visualMapping in VisualMappings)
            {
                if (visualMapping?.Parent == null)
                    continue;

                var anchorName = GetAnchorName(visualMapping.Type);
                var anchor = GetOrCreateAnchor(anchorName, visualMapping.Parent);
                BindTransformToAnchor(visualMapping.Parent, anchor);
            }

            var eyesAnchor = FindChildByName(RotatingBodyParent.transform, EyesPlaceName);
            if (eyesAnchor != null && DeathEyesParent != null)
                BindTransformToAnchor(DeathEyesParent.transform, eyesAnchor);
        }

        private static void SetConstraintSource(IConstraint constraint, Transform sourceTransform)
        {
            if (constraint == null || sourceTransform == null)
                return;

            // Unity components can become native-side "fake null" values while an
            // interface reference still looks non-null (notably in Unity 6.5).
            if (constraint is Object unityObject && unityObject == null)
                return;

            var source = new ConstraintSource { sourceTransform = sourceTransform, weight = 1f };
            if (constraint.sourceCount > 0)
                constraint.SetSource(0, source);
            else
                constraint.AddSource(source);

            constraint.weight = 1f;
            constraint.constraintActive = true;
            constraint.locked = true;
        }

        private static void BindTransformToAnchor(Transform constrainedTransform, Transform anchor)
        {
            if (constrainedTransform == null || anchor == null)
                return;

            var positionConstraint = constrainedTransform.GetComponent<PositionConstraint>()
                                     ?? constrainedTransform.gameObject.AddComponent<PositionConstraint>();
            var rotationConstraint = constrainedTransform.GetComponent<RotationConstraint>()
                                     ?? constrainedTransform.gameObject.AddComponent<RotationConstraint>();

            SetConstraintSource(positionConstraint, anchor);
            SetConstraintSource(rotationConstraint, anchor);
        }

        private Transform GetOrCreateAnchor(string anchorName, Transform constrainedTransform)
        {
            if (RotatingBodyParent == null || constrainedTransform == null || string.IsNullOrEmpty(anchorName))
                return null;

            var anchorRoot = RotatingBodyParent.transform;
            var anchor = FindChildByName(anchorRoot, anchorName);
            if (anchor != null)
                return anchor;

            anchor = new GameObject(anchorName).transform;
            anchor.SetParent(anchorRoot, false);
            anchor.SetPositionAndRotation(constrainedTransform.position, constrainedTransform.rotation);
            anchor.localScale = Vector3.one;
            return anchor;
        }

        private static string GetAnchorName(VisualType visualType)
        {
            switch (visualType)
            {
                case VisualType.Eyes:
                    return EyesPlaceName;
                case VisualType.Mouth:
                    return MouthPlaceName;
                case VisualType.HairOrHat:
                    return HatPlaceName;
                case VisualType.Moustache:
                    return MoustachePlaceName;
                case VisualType.Nose:
                    return NosePlaceName;
                case VisualType.Eyebrow:
                    return EyebrowPlaceName;
                case VisualType.Weapon:
                    return WeaponPlaceName;
                default:
                    return null;
            }
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
                return null;

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName)
                    return child;
            }

            return null;
        }

        public void LookBack()
        {
            SetFacingDirection(-1f);
        }

        public void LookForward()
        {
            SetFacingDirection(1f);
        }

        private void SetFacingDirection(float direction)
        {
            // BodyVisual is nested below BodyRoot in the supplied character prefab;
            // flipping both would cancel the visible direction change.
            var target = BodyRoot != null ? BodyRoot : BodyVisual;
            if (target == null)
                return;

            var scale = target.localScale;
            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(direction);
            target.localScale = scale;
        }
    }

    [System.Serializable]
    public class VisualMapping
    {
        public VisualType Type;
        public Transform Parent;
        public GameObject CurrentVisual;
    }
}
