using UnityEngine;
using UnityEngine.UI;
using com.pyroduck.eggheads.Runtime.Scripts.Character;

namespace com.pyroduck.eggheads.Runtime.Scripts.UI
{
    public class HPBarController : MonoBehaviour
    {
        [SerializeField] private Image hpBarFill;
        [SerializeField] private HealthComponent healthComponent;
        [SerializeField] private Button ReviveButton;
        
        private void Awake()
        {
            if (healthComponent == null)
                healthComponent = GetComponentInParent<HealthComponent>();

            if (ReviveButton != null)
            {
                ReviveButton.onClick.AddListener(OnReviveButtonClicked);
                ReviveButton.gameObject.SetActive(false);
            }

            if (healthComponent != null)
                Bind(healthComponent);
        }

        private void OnReviveButtonClicked()
        {
            healthComponent?.ResetHealth();
        }

        public void Bind(HealthComponent source)
        {
            if (healthComponent != null)
            {
                healthComponent.OnDamaged       -= OnDamaged;
                healthComponent.OnHealed        -= OnHealed;
                healthComponent.OnHealthUpdated -= OnHealthUpdated;
                healthComponent.OnDied          -= OnDied;
            }

            healthComponent = source;

            if (source != null)
            {
                healthComponent.OnDamaged += OnDamaged;
                healthComponent.OnHealed += OnHealed;
                healthComponent.OnHealthUpdated += OnHealthUpdated;
                healthComponent.OnDied += OnDied;

                Refresh(healthComponent.Health, healthComponent.MaxHealth);
                SyncReviveButton();
            }
        }

        private void OnDestroy()
        {
            if (ReviveButton != null)
                ReviveButton.onClick.RemoveListener(OnReviveButtonClicked);

            if (healthComponent != null)
            {
                healthComponent.OnDamaged       -= OnDamaged;
                healthComponent.OnHealed        -= OnHealed;
                healthComponent.OnHealthUpdated -= OnHealthUpdated;
                healthComponent.OnDied          -= OnDied;
            }
        }

        private void OnHealthUpdated(float currentHealth)
        {
            Refresh(currentHealth, healthComponent.MaxHealth);
            SyncReviveButton();
        }

        private void OnDamaged(float amount, GameObject source)
            => Refresh(healthComponent.Health, healthComponent.MaxHealth);

        private void OnDied() => SyncReviveButton();

        private void OnHealed(float newHealth)
            => Refresh(newHealth, healthComponent.MaxHealth);

        /// <summary>Revive UI only while dead — hidden whenever <see cref="HealthComponent.IsAlive"/>.</summary>
        private void SyncReviveButton()
        {
            if (ReviveButton == null || healthComponent == null) return;
            ReviveButton.gameObject.SetActive(!healthComponent.IsAlive);
        }

        private void Refresh(float current, float max)
        {
            if (hpBarFill == null) return;
            hpBarFill.fillAmount = max > 0f ? current / max : 0f;
        }
    }
}
