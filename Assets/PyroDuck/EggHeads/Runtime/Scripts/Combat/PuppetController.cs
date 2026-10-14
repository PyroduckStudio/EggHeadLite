using System;
using System.Collections;
using UnityEngine;

namespace com.pyroduck.eggheads.Runtime.Scripts.Combat
{
    public class PuppetController : MonoBehaviour
    {
        public PuppetActionChecker actionChecker;
        public SpriteRenderer spriteRenderer;
        private Coroutine flashCoroutine;

        private void OnEnable()
        {
            actionChecker.OnTakeDamage += HandleTakeDamage;
        }

        private void OnDisable()
        {
            actionChecker.OnTakeDamage -= HandleTakeDamage;
        }

        private void HandleTakeDamage()
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            spriteRenderer.color = Color.white;
            flashCoroutine = StartCoroutine(FlashRed());
        }

        private IEnumerator FlashRed()
        {
            spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            spriteRenderer.color = Color.white;
        }
    }
}