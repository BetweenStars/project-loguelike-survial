using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PlayerWeaponController : MonoBehaviour
{
    [SerializeField] private Weapon mainWeapon;
    [SerializeField] private List<Weapon> auxiliaryWeapons = new List<Weapon>();
    [SerializeField, Min(0)] private int auxiliarySlots = 3;
    private readonly List<Weapon> weapons = new List<Weapon>();
    public IReadOnlyList<Weapon> Weapons => weapons;
    public IReadOnlyList<Weapon> AvailableAuxiliaries => auxiliaryWeapons;
    public Weapon MainWeapon => mainWeapon;
    public int AuxiliaryCount => weapons.Count - (mainWeapon != null && mainWeapon.IsOwned ? 1 : 0);
    public event Action Changed;
    public UnityEvent InventoryChanged = new UnityEvent();
    public List<Weapon> GetWeapons()
    {
        return new List<Weapon>(weapons);
    }

    public void ResetLoadout()
    {
        foreach (Weapon weapon in weapons)
            if (weapon != null)
                weapon.Changed -= Notify;
        weapons.Clear();
        foreach (Weapon weapon in auxiliaryWeapons)
            if (weapon != null && weapon != mainWeapon)
                weapon.ResetOwnership();
        if (mainWeapon != null)
        {
            mainWeapon.ResetOwnership();
            Register(mainWeapon);
        }
        else
            Debug.LogError("PlayerWeaponController: assign Main Weapon.", this);
        Notify();
    }

    public bool CanAcquire(Weapon weapon)
    {
        return weapon != null && weapon != mainWeapon && !weapon.IsOwned && auxiliaryWeapons.Contains(weapon) && AuxiliaryCount < auxiliarySlots;
    }

    public bool TryAcquire(Weapon weapon)
    {
        if (!CanAcquire(weapon))
            return false;
        Register(weapon);
        Notify();
        return true;
    }

    public bool TryUpgrade(Weapon weapon)
    {
        if (weapon == null || !weapons.Contains(weapon) || !weapon.CanUpgrade())
            return false;
        weapon.Upgrade();
        return true;
    }

    private void Register(Weapon weapon)
    {
        weapons.Add(weapon);
        weapon.Changed += Notify;
        weapon.Acquire();
    }

    private void Notify()
    {
        Changed?.Invoke();
        InventoryChanged.Invoke();
    }

    private void OnDestroy()
    {
        foreach (Weapon weapon in weapons)
            if (weapon != null)
                weapon.Changed -= Notify;
    }
}
