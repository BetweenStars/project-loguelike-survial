using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class RewardPanelView : MonoBehaviour
{
    [Tooltip("Keep this component outside the panel root so it can reopen the panel.")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RewardChoiceView[] choices;
    [SerializeField] private ShopController shop;
    [SerializeField] private LevelUpController levelUp;
    [SerializeField] private PlayerStats player;
    [SerializeField] private PlayerWeaponController inventory;
    [SerializeField] private bool showShop;
    private void OnEnable()
    {
        if (shop != null)
            shop.Changed += Refresh;
        if (levelUp != null)
            levelUp.Changed += Refresh;
        if (player != null)
            player.Changed += Refresh;
        if (inventory != null)
            inventory.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (shop != null)
            shop.Changed -= Refresh;
        if (levelUp != null)
            levelUp.Changed -= Refresh;
        if (player != null)
            player.Changed -= Refresh;
        if (inventory != null)
            inventory.Changed -= Refresh;
    }

    public void Refresh()
    {
        bool open = showShop ? shop != null && shop.IsOpen && (levelUp == null || !levelUp.HasPending) : levelUp != null && levelUp.IsChoosing;
        if (panelRoot != null && panelRoot != gameObject)
            panelRoot.SetActive(open);
        if (!open || choices == null)
            return;
        GameObject focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (focused != null && panelRoot != null && focused.transform.IsChildOf(panelRoot.transform))
            EventSystem.current.SetSelectedGameObject(null);
        IReadOnlyList<RewardOffer> offers = showShop ? shop.Offers : levelUp.Offers;
        int selectedIndex = showShop ? shop.SelectedIndex : levelUp.SelectedIndex;
        for (int i = 0; i < choices.Length; i++)
        {
            if (choices[i] == null)
                continue;
            choices[i].gameObject.SetActive(i < offers.Count);
            if (i >= offers.Count)
                continue;
            choices[i].Bind(offers[i], showShop);
            choices[i].SetSelected(i == selectedIndex);
        }
    }
}
