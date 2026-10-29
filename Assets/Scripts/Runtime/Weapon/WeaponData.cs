using UnityEngine;

public enum WeaponType
{
    None = -1, Projectile = 0, Melee = 1, Ranged = 2
}
[CreateAssetMenu(fileName = "WeaponData", menuName = "Survival/Weapon Display Data")]
public class WeaponData : ScriptableObject
{
    public WeaponType WeaponType;
    public string DisplayName;
    [TextArea] public string Description;
    [Tooltip("Element 0: level 1 acquisition; elements 1–6: upgrades to levels 2–7. Empty entries use Description.")]
    [TextArea] public string[] LevelDescriptions = new string[Weapon.MaxLevel];
    public string GetLevelDescription(int level)
    {
        int index = level - 1;
        return LevelDescriptions != null && index >= 0 && index < LevelDescriptions.Length &&
            !string.IsNullOrWhiteSpace(LevelDescriptions[index]) ? LevelDescriptions[index] : Description;
    }
    public Sprite Icon;
    [Min(0)] public int PurchasePrice = 30;
    [Min(0)] public int UpgradePrice = 20;
}
