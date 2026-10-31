using UnityEngine;

// Страховка. Если при сборке склада скрипты не прикрепились к объектам
// (например, файлы скриптов добавили уже после сборки), при запуске игры они подключаются сами.
// Если всё подключено правильно, этот скрипт ничего не делает.
public static class GameplayFixer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Run()
    {
        Ensure<MopItem>("Mop");
        Ensure<WorkJack>("WorkJack");
        Ensure<ShipZone>("ShipZone");
        Ensure<KeyItem>("HiddenKey");

        PalletSpot spot = Ensure<PalletSpot>("PalletSpot");
        if (spot != null)
        {
            if (spot.palletMaterial == null) spot.palletMaterial = MaterialOf("AlleyPallet");
            if (spot.boxMaterial == null) spot.boxMaterial = MaterialOf("LoneBox");
            if (spot.filmMaterial == null) spot.filmMaterial = MaterialOf("Wrap");
        }

        // Весь мусор у входа
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name != "Trash") continue;
            if (all[i].GetComponent<TrashPiece>() == null)
            {
                all[i].gameObject.AddComponent<TrashPiece>();
                Debug.Log("Подключён скрипт TrashPiece к куску мусора");
            }
        }
    }

    static T Ensure<T>(string objectName) where T : Component
    {
        GameObject go = GameObject.Find(objectName);
        if (go == null)
        {
            Debug.LogWarning("Не найден объект " + objectName + ". Соберите склад заново: Smena, Построить склад (с нуля).");
            return null;
        }
        T c = go.GetComponent<T>();
        if (c == null)
        {
            c = go.AddComponent<T>();
            Debug.Log("Подключён скрипт " + typeof(T).Name + " к объекту " + objectName);
        }
        return c;
    }

    // Берёт материал у существующего объекта (или у его детей), чтобы паллета выглядела как остальные
    static Material MaterialOf(string objectName)
    {
        GameObject go = GameObject.Find(objectName);
        if (go == null) return null;
        Renderer r = go.GetComponent<Renderer>();
        if (r == null) r = go.GetComponentInChildren<Renderer>();
        return r != null ? r.sharedMaterial : null;
    }
}
