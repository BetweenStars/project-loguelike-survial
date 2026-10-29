using System.Collections.Generic;
using UnityEngine;

public enum RewardKind
{
    MoveSpeed, MaxHealth, AcquireWeapon, UpgradeWeapon
}

public class RewardOffer
{
    public RewardKind Kind
    {
        get;
    }

    public Weapon Weapon
    {
        get;
    }

    public int Price
    {
        get;
    }

    public float Amount
    {
        get;
    }

    public bool Purchased { get; private set; }
    public string Title
    {
        get
        {
            switch (Kind)
            {
                case RewardKind.MoveSpeed:
                    return "이동속도 증가";
                case RewardKind.MaxHealth:
                    return "최대체력 증가";
                case RewardKind.AcquireWeapon:
                    return Weapon.DisplayName;
                default:
                    return Weapon.DisplayName;
            }
        }
    }

    private readonly Sprite statIcon;
    private readonly int fromLevel;
    public string UpgradeText => Kind == RewardKind.AcquireWeapon ? "신규" :
        Kind == RewardKind.UpgradeWeapon ? $"{fromLevel}->{fromLevel + 1}" : "";
    public string Description => Kind == RewardKind.MoveSpeed ? $"이동속도 +{Amount:0.#}%" :
        Kind == RewardKind.MaxHealth ? $"최대체력 +{Amount:0}" :
        Weapon != null && Weapon.Data != null ? Weapon.Data.GetLevelDescription(fromLevel + 1) : "";
    public Sprite Icon => Weapon != null && Weapon.Data != null ? Weapon.Data.Icon : statIcon;
    public RewardOffer(RewardKind kind, int price, float amount = 0, Weapon weapon = null, Sprite icon = null)
    {
        Kind = kind;
        Price = Mathf.Max(0, price);
        Amount = amount;
        Weapon = weapon;
        statIcon = icon;
        fromLevel = kind == RewardKind.UpgradeWeapon && weapon != null ? weapon.Level : 0;
    }

    public bool IsAvailable(PlayerWeaponController inventory)
    {
        if (Purchased)
            return false;
        if (Kind == RewardKind.AcquireWeapon)
            return inventory != null && inventory.CanAcquire(Weapon);
        if (Kind == RewardKind.UpgradeWeapon)
            return inventory != null && inventory.Weapons.ContainsWeapon(Weapon) && Weapon.CanUpgrade();
        return Amount > 0;
    }

    public bool TryApply(PlayerStats player, PlayerWeaponController inventory, bool chargeMoney)
    {
        if (player == null || player.IsDead || !IsAvailable(inventory) || (chargeMoney && player.Money < Price))
            return false;
        // Reserve the offer before notifications so UI listeners cannot purchase it twice.
        Purchased = true;
        if (chargeMoney && !player.TrySpend(Price))
        {
            Purchased = false;
            return false;
        }
        switch (Kind)
        {
            case RewardKind.MoveSpeed:
                player.IncreaseSpeed(Amount);
                break;
            case RewardKind.MaxHealth:
                player.IncreaseMaxHp(Mathf.RoundToInt(Amount));
                break;
            case RewardKind.AcquireWeapon:
                inventory.TryAcquire(Weapon);
                break;
            case RewardKind.UpgradeWeapon:
                inventory.TryUpgrade(Weapon);
                break;
        }
        return true;
    }
}

public static class RewardCatalog
{
    public static bool ContainsWeapon(this IReadOnlyList<Weapon> weapons, Weapon target)
    {
        for (int i = 0; i < weapons.Count; i++)
            if (weapons[i] == target)
                return true;
        return false;
    }

    public static List<RewardOffer> CreateCandidates(PlayerWeaponController inventory, RewardData settings, bool shop)
    {
        var result = new List<RewardOffer>
        {
            new RewardOffer(RewardKind.MoveSpeed, settings != null ? settings.SpeedPrice : 15, settings != null ? settings.SpeedPercent : 10, icon: settings != null ? settings.MoveSpeedIcon : null),
            new RewardOffer(RewardKind.MaxHealth, settings != null ? settings.HealthPrice : 15, settings != null ? settings.MaxHealthIncrease : 20, icon: settings != null ? settings.MaxHealthIcon : null)
        };
        if (inventory == null)
            return result;
        foreach (Weapon weapon in inventory.Weapons)
            if (weapon != null && weapon.CanUpgrade())
                result.Add(new RewardOffer(RewardKind.UpgradeWeapon,
                weapon.Data != null ? weapon.Data.UpgradePrice : settings != null ? settings.DefaultUpgradePrice : 20, weapon: weapon));
        if (shop)
            foreach (Weapon weapon in inventory.AvailableAuxiliaries)
                if (inventory.CanAcquire(weapon) && !result.Exists(offer => offer.Weapon == weapon))
                    result.Add(new RewardOffer(RewardKind.AcquireWeapon,
                    weapon.Data != null ? weapon.Data.PurchasePrice : settings != null ? settings.DefaultWeaponPrice : 30, weapon: weapon));
        return result;
    }

    public static List<RewardOffer> Draw(List<RewardOffer> candidates, int count = 3)
    {
        var pool = new List<RewardOffer>(candidates);
        var result = new List<RewardOffer>();
        while (result.Count < count && pool.Count > 0)
        {
            int index = Random.Range(0, pool.Count);
            result.Add(pool[index]);
            pool.RemoveAt(index);
        }
        return result;
    }
}
