// «СМЕНА» — режим игры кнопкой в Unity: Smena → Режим игры → Хоррор / Спокойный.
// Хоррор — Володя, пульс, укрытия, скример. Спокойный — только работа и темнота.
using UnityEditor;
using UnityEngine;

public static class GameModeMenu
{
    const string Root = "Smena/Режим игры/";

    [MenuItem(Root + "Хоррор (Володя, пульс, скример)", false, 50)]
    static void Horror() { GameConfig.horror = true; Debug.Log("Режим игры: хоррор"); }
    [MenuItem(Root + "Хоррор (Володя, пульс, скример)", true)]
    static bool HorrorV() { Menu.SetChecked(Root + "Хоррор (Володя, пульс, скример)", GameConfig.horror); return true; }

    [MenuItem(Root + "Спокойный (только работа)", false, 51)]
    static void Calm() { GameConfig.horror = false; Debug.Log("Режим игры: спокойный"); }
    [MenuItem(Root + "Спокойный (только работа)", true)]
    static bool CalmV() { Menu.SetChecked(Root + "Спокойный (только работа)", !GameConfig.horror); return true; }
}
