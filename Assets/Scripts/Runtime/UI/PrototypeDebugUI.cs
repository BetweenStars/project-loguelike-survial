using UnityEngine;

// Optional functional UI for the cube sample; replace it with the view components for final UI.
public class PrototypeDebugUI : MonoBehaviour
{
    [SerializeField] private SceneNavigation navigation;
    [SerializeField] private GameFlowController flow;
    [SerializeField] private PlayerStats player;
    [SerializeField] private LevelUpController levelUp;
    [SerializeField] private ShopController shop;
    [SerializeField] private GamePause pause;
    [SerializeField] private WaveManager waves;
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(20, 20, 400, Screen.height - 40), GUI.skin.box);
        if (flow == null)
        {
            GUILayout.Label("SURVIVAL - Cube Sample");
            if (GUILayout.Button("Start Game"))
                navigation.StartGame();
            GUILayout.EndArea();
            return;
        }
        GUILayout.Label($"Stage {flow.CurrentStage + 1} | {flow.CurrentPhase} | {waves.Elapsed:0}s");
        GUILayout.Label($"HP {player.Hp}/{player.MaxHp} | Gold {player.Money} | Lv {player.Level} | EXP {player.Exp}/{player.ExperienceRequired}");
        GUILayout.Label("WASD move / Mouse aim / Left shoot / Q wire / E shop / ESC pause");
        if (waves.CurrentBoss != null)
            GUILayout.Label($"BOSS {waves.CurrentBoss.Health:0}/{waves.CurrentBoss.MaxHealth:0}");
        if (levelUp.IsChoosing)
        {
            GUILayout.Label("1 / 2 / 3 or W / S / Wheel: Select | Space: Confirm");
            for (int i = 0; i < levelUp.Offers.Count; i++)
                if (GUILayout.Button($"{(levelUp.SelectedIndex == i ? "> " : "")}{i + 1}. {levelUp.Offers[i].Title}"))
                {
                    levelUp.SelectChoice(i);
                    break;
                }
        }
        else if (shop.IsOpen)
        {
            GUILayout.Label("1 / 2 / 3 or W / S / Wheel: Select | Space: Buy | Esc: Exit");
            for (int i = 0; i < shop.Offers.Count; i++)
            {
                RewardOffer offer = shop.Offers[i];
                GUI.enabled = !offer.Purchased && player.Money >= offer.Price;
                if (GUILayout.Button($"{(shop.SelectedIndex == i ? "> " : "")}{i + 1}. " + (offer.Purchased ? "Sold" : $"{offer.Title} : {offer.Price}")))
                    shop.SelectChoice(i);
            }
            GUI.enabled = true;
            if (GUILayout.Button("Exit Shop (Esc)"))
                shop.Exit();
        }
        else if (shop.CanInteract)
            GUILayout.Label("Press E to open shop");
        if ((pause.Reasons & PauseReason.Menu) != 0)
        {
            if (GUILayout.Button("Resume"))
                pause.Resume();
            if (GUILayout.Button("Return to Title"))
                flow.ReturnToTitle();
        }
        if (flow.CurrentPhase == GamePhase.Clear && GUILayout.Button("Clear! Return to Title"))
            flow.ReturnToTitle();
        GUILayout.EndArea();
    }
}
