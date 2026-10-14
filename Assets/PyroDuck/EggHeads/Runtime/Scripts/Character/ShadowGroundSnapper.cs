using System;
using UnityEngine;

namespace com.pyroduck.eggheads.Runtime.Scripts
{
    public class ShadowGroundSnapper : MonoBehaviour
    {
        [SerializeField] private Transform _shadow;
        [SerializeField] private Transform _shadowVisual;
        [SerializeField] private Vector2 _offset;
        [SerializeField] private float distanceToGround;
        
        [SerializeField] private float disappearDistance = 5f;
        [SerializeField] private float maxSize = 0.6f;
        private void Update()
        {
            var hit = Physics2D.Raycast(
                transform.position,
                Vector2.down,
                Mathf.Infinity,
                LayerMask.GetMask("Ground"));
            if (hit.collider != null)
            {
                distanceToGround = hit.distance;
                float normalizedDistance = 1f-Mathf.Clamp01(distanceToGround / disappearDistance);
                _shadowVisual.localScale = Vector3.one * Mathf.Lerp(0, maxSize, normalizedDistance);
                
                _shadow.position = hit.point + _offset;
            }
        }
    }
}