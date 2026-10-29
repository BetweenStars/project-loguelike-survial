using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class RewardChoiceView : MonoBehaviour
{
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text price;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text upgradeText;
    [SerializeField] private TMP_Text description;
    [SerializeField] private GameObject selectionIndicator;
    [SerializeField] private Color selectionColor = new Color(1f, 0.8f, 0.2f, 1f);
    public UnityEvent<bool> SelectionChanged = new UnityEvent<bool>();
    public bool IsSelected { get; private set; }
    private Outline selectionOutline;
    private void Awake()
    {
        if (selectionIndicator == null)
        {
            Graphic background = GetComponent<Graphic>();
            if (background != null)
            {
                selectionOutline = background.gameObject.AddComponent<Outline>();
                selectionOutline.effectColor = selectionColor;
                selectionOutline.effectDistance = new Vector2(3f, -3f);
                selectionOutline.useGraphicAlpha = false;
                selectionOutline.enabled = false;
            }
        }
        if (selectionIndicator != null)
            selectionIndicator.SetActive(false);
    }

    public void Bind(RewardOffer offer, bool shop)
    {
        if (title != null)
            title.text = offer.Title;
        if (upgradeText != null)
            upgradeText.text = offer.UpgradeText;
        if (description != null)
            description.text = offer.Description;
        if (price != null)
            price.text = shop ? (offer.Purchased ? "구매 완료" : offer.Price.ToString()) : "";
        if (icon != null)
        {
            icon.sprite = offer.Icon;
            icon.enabled = offer.Icon != null;
        }
    }

    public void SetSelected(bool value)
    {
        if (selectionIndicator != null)
            selectionIndicator.SetActive(value);
        if (selectionOutline != null)
            selectionOutline.enabled = value;
        if (IsSelected == value)
            return;
        IsSelected = value;
        SelectionChanged.Invoke(value);
    }

    private void OnDisable()
    {
        SetSelected(false);
    }

}
