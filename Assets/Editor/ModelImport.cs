// Настройки импорта для моделей в Assets/Resources/Models/<ключ>/:
// - без камер, света, коллайдеров; анимации только у Володи (папки, где в пути есть "volodya");
// - текстуры не больше 1024, файлы *_normal становятся картами нормалей;
// - материалы переводятся на URP Lit, и к ним подключаются текстуры <ключ>_color и <ключ>_normal из той же папки.
// Если модель после импорта белая или розовая: меню Smena → Переимпортировать модели.
using System.IO;
using UnityEditor;
using UnityEngine;

public class ModelImport : AssetPostprocessor
{
    const string Folder = "Assets/Resources/Models";

    static bool InModels(string p)
    {
        return p.Replace('\\', '/').Contains("/Resources/Models/");
    }

    void OnPreprocessModel()
    {
        if (!InModels(assetPath)) return;
        ModelImporter mi = (ModelImporter)assetImporter;
        mi.importCameras = false;
        mi.importLights = false;
        mi.addCollider = false;
        mi.isReadable = false;
        if (!assetPath.ToLowerInvariant().Contains("volodya"))
        {
            mi.importAnimation = false;
        }
        else
        {
            // Володя — гуманоид: его позу задаёт код (VolodyaModel), а позже и анимации с Mixamo
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        }
    }

    void OnPreprocessTexture()
    {
        if (!InModels(assetPath)) return;
        TextureImporter ti = (TextureImporter)assetImporter;
        ti.maxTextureSize = 1024;
        string n = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
        if (n.Contains("_normal")) ti.textureType = TextureImporterType.NormalMap;
    }

    void OnPostprocessMaterial(Material m)
    {
        if (!InModels(assetPath)) return;

        // На URP Lit (иначе материал может стать розовым)
        if (!m.shader.name.StartsWith("Universal Render Pipeline"))
        {
            Color c = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            Texture t = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            Shader urp = Shader.Find("Universal Render Pipeline/Lit");
            if (urp != null)
            {
                m.shader = urp;
                m.SetColor("_BaseColor", c);
                if (t != null) m.SetTexture("_BaseMap", t);
            }
        }

        string dir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
        string key = Path.GetFileName(dir);
        Texture2D col = Load(dir, key + "_color");
        Texture2D nrm = Load(dir, key + "_normal");
        if (col != null && m.mainTexture == null)
        {
            m.mainTexture = col;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", col);
            if (m.color.maxColorComponent < 0.2f) m.color = Color.white;
        }
        if (nrm != null && m.HasProperty("_BumpMap") && m.GetTexture("_BumpMap") == null)
        {
            m.SetTexture("_BumpMap", nrm);
            m.EnableKeyword("_NORMALMAP");
        }
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", Mathf.Min(m.GetFloat("_Smoothness"), 0.3f));
    }

    static Texture2D Load(string dir, string name)
    {
        string[] ext = { ".jpg", ".png" };
        for (int i = 0; i < ext.Length; i++)
        {
            Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + name + ext[i]);
            if (t != null) return t;
        }
        return null;
    }

    // Один раз после появления этого скрипта переимпортируем модели сами (они могли импортироваться раньше без текстур)
    [InitializeOnLoadMethod]
    static void AutoOnce()
    {
        if (EditorPrefs.GetInt("SmenaModelImportV", 0) >= 1) return;
        EditorPrefs.SetInt("SmenaModelImportV", 1);
        EditorApplication.delayCall += ReimportAll;
    }

    [MenuItem("Smena/Переимпортировать модели")]
    static void ReimportAll()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) return;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Folder }))
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        Debug.Log("Модели переимпортированы.");
    }
}
