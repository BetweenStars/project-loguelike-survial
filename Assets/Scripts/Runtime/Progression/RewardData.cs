using UnityEngine;

[CreateAssetMenu(fileName = "RewardSettings", menuName = "Survival/Reward Settings")]
public class RewardData : ScriptableObject
{
    public Sprite MoveSpeedIcon;
    public Sprite MaxHealthIcon;
    [Min(0.1f)] public float SpeedPercent = 10f;
    [Min(1)] public int MaxHealthIncrease = 20;
    [Min(0)] public int SpeedPrice = 15;
    [Min(0)] public int HealthPrice = 15;
    [Min(0)] public int DefaultWeaponPrice = 30;
    [Min(0)] public int DefaultUpgradePrice = 20;
}
