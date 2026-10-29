using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHudView : MonoBehaviour
{
    [SerializeField] private PlayerStats player;
    [SerializeField] private PlayerWeaponController inventory;
    [SerializeField] private GameFlowController flow;
    [SerializeField] private WaveManager waves;
    [SerializeField] private GrappleController grapple;
    [SerializeField] private Slider health;
    [SerializeField] private Slider experience;
    [SerializeField] private Slider bossHealth;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text stageText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text weaponsText;
    [SerializeField] private TMP_Text grappleText;
    [Tooltip("Optional image near the crosshair, shown only when the wire can be fired.")]
    [SerializeField] private Image grappleAvailableImage;
    private void OnEnable()
    {
        if (grappleAvailableImage != null)
        {
            grappleAvailableImage.enabled = false;
            grappleAvailableImage.raycastTarget = false;
        }
        if (player != null)
            player.Changed += Refresh;
        if (inventory != null)
            inventory.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (grappleAvailableImage != null)
            grappleAvailableImage.enabled = false;
        if (player != null)
            player.Changed -= Refresh;
        if (inventory != null)
            inventory.Changed -= Refresh;
    }

    public void Refresh()
    {
        if (player != null)
        {
            SetBar(health, player.Hp, player.MaxHp);
            SetBar(experience, player.Exp, player.ExperienceRequired);
            if (healthText != null)
                healthText.text = $"{player.Hp} / {player.MaxHp}";
            if (moneyText != null)
                moneyText.text = player.Money.ToString();
            if (levelText != null)
                levelText.text = $"Lv. {player.Level}";
        }
        if (inventory != null && weaponsText != null)
        {
            var text = new StringBuilder();
            foreach (Weapon weapon in inventory.Weapons)
                text.AppendLine($"{weapon.DisplayName} Lv.{weapon.Level}");
            weaponsText.text = text.ToString();
        }
    }

    private void Update()
    {
        if (stageText != null && flow != null)
            stageText.text = $"Stage {flow.CurrentStage + 1}";
        if (timerText != null && waves != null)
            timerText.text = $"{(int)waves.Elapsed / 60:00}:{(int)waves.Elapsed % 60:00}";
        if (grappleText != null && grapple != null)
            grappleText.text = grapple.CooldownRemaining > 0 ? $"Wire {grapple.CooldownRemaining:0.0}s" : "Wire Ready";
        if (bossHealth != null && waves != null)
        {
            bossHealth.gameObject.SetActive(waves.CurrentBoss != null);
            if (waves.CurrentBoss != null)
                SetBar(bossHealth, waves.CurrentBoss.Health, waves.CurrentBoss.MaxHealth);
        }
    }

    private void LateUpdate()
    {
        if (grappleAvailableImage != null)
            grappleAvailableImage.enabled = grapple != null && grapple.CanFire;
    }

    private static void SetBar(Slider slider, float value, float max)
    {
        if (slider != null)
        {
            slider.minValue = 0;
            slider.maxValue = Mathf.Max(1, max);
            slider.value = value;
        }
    }
}
