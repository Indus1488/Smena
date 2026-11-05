using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Растительность Poly Haven (Assets/Art/Vegetation/<модель>/): трава, кусты, деревца, ёлочки, сухие ветки.
//  - текстуры: *_nor_gl_* — карта нормалей; *_diffa_* — цвет с прозрачностью (листья, травинки: вырез, видно с двух сторон);
//  - модели: материалы URP Lit создаются сами и подключаются к FBX (Smena → Окрестности → Настроить растительность —
//    то же самое вручную, если что-то добавил);
//  - если в этой версии Unity есть «Mesh LOD», модели получают упрощённые копии для дальних деревьев.
// Новую модель Poly Haven (FBX, 2K) просто клади в Assets/Art/Vegetation/<имя>/ вместе с текстурами.
public class VegetationImport : AssetPostprocessor
{
    const string Root = "Assets/Art/Vegetation";
    static bool scheduled;

    static bool IsVeg(string path) { return path.Replace('\\', '/').Contains("/Art/Vegetation/"); }

    void OnPreprocessTexture()
    {
        if (!IsVeg(assetPath)) return;
        TextureImporter ti = (TextureImporter)assetImporter;
        string n = Path.GetFileName(assetPath).ToLowerInvariant();
        ti.maxTextureSize = 2048;
        if (n.Contains("_nor_")) ti.textureType = TextureImporterType.NormalMap;
        if (n.Contains("_diffa"))
        {
            ti.alphaIsTransparency = true;
            ti.mipMapsPreserveCoverage = true;     // листья не «тают» вдали
            ti.alphaTestReferenceValue = 0.45f;
        }
    }

    void OnPreprocessModel()
    {
        if (!IsVeg(assetPath)) return;
        ModelImporter mi = (ModelImporter)assetImporter;
        mi.addCollider = false;
        mi.importAnimation = false;
        mi.importCameras = false;
        mi.importLights = false;
        // Mesh LOD (Unity 6.2+): упрощённые копии для дальних деревьев. Через отражение — чтобы не ломалось на других версиях.
        PropertyInfo p = typeof(ModelImporter).GetProperty("generateMeshLods");
        if (p != null && p.CanWrite) p.SetValue(mi, true);
    }

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string a in imported)
            if (IsVeg(a) && a.ToLowerInvariant().EndsWith(".fbx") && !scheduled)
            {
                scheduled = true;
                EditorApplication.delayCall += () => { scheduled = false; Setup(false); };
                break;
            }
    }

    [MenuItem("Smena/Окрестности/Настроить растительность")]
    static void SetupMenu() { Setup(true); }

    static void Setup(bool force)
    {
        if (!AssetDatabase.IsValidFolder(Root)) return;
        int made = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) continue;
            string folder = Path.GetDirectoryName(path).Replace('\\', '/');
            Dictionary<string, string> diff, nrm;
            HashSet<string> alpha;
            ScanTextures(folder, out diff, out nrm, out alpha);

            Dictionary<AssetImporter.SourceAssetIdentifier, Object> map = mi.GetExternalObjectMap();
            bool changed = false;
            foreach (Object o in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
            {
                Material em = o as Material;
                if (em == null) continue;
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), em.name);
                if (map.ContainsKey(id) && map[id] != null && !force) continue;

                string matPath = folder + "/" + em.name + ".mat";
                Material m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                bool isNew = m == null;
                if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                Fill(m, Best(em.name, diff.Keys, alpha), diff, nrm, alpha);
                if (isNew) AssetDatabase.CreateAsset(m, matPath); else EditorUtility.SetDirty(m);
                mi.AddRemap(id, m);
                changed = true;
                made++;
            }
            // уже переназначенные материалы тоже обновляем (если поменял текстуры)
            foreach (var kv in map)
            {
                Material m = kv.Value as Material;
                if (m == null || !force) continue;
                Fill(m, Best(kv.Key.name, diff.Keys, alpha), diff, nrm, alpha);
                EditorUtility.SetDirty(m);
            }
            if (changed) mi.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
        if (made > 0 || force) Debug.Log("[Vegetation] материалов настроено: " + made);
    }

    static void Fill(Material m, string prefix, Dictionary<string, string> diff, Dictionary<string, string> nrm, HashSet<string> alpha)
    {
        if (prefix == null) return;
        Texture2D d = AssetDatabase.LoadAssetAtPath<Texture2D>(diff[prefix]);
        if (d != null) { m.SetTexture("_BaseMap", d); m.mainTexture = d; }
        m.SetColor("_BaseColor", Color.white);
        string np;
        if (nrm.TryGetValue(prefix, out np))
        {
            Texture2D n = AssetDatabase.LoadAssetAtPath<Texture2D>(np);
            if (n != null) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f); }
        }
        m.SetFloat("_Smoothness", 0.12f);
        m.SetFloat("_Metallic", 0f);
        if (alpha.Contains(prefix))
        {
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.45f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0f);                                   // видно с обеих сторон
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            m.SetOverrideTag("RenderType", "TransparentCutout");
        }
        else
        {
            m.SetFloat("_AlphaClip", 0f);
            m.DisableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 2f);
            m.renderQueue = -1;
        }
        m.enableInstancing = true;
    }

    // Текстуры в папке модели: «префикс» — часть имени до _diff / _nor_
    static void ScanTextures(string folder, out Dictionary<string, string> diff, out Dictionary<string, string> nrm, out HashSet<string> alpha)
    {
        diff = new Dictionary<string, string>(); nrm = new Dictionary<string, string>(); alpha = new HashSet<string>();
        foreach (string f in Directory.GetFiles(folder))
        {
            string ext = Path.GetExtension(f).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".exr" && ext != ".tga") continue;
            string n = Path.GetFileNameWithoutExtension(f);
            string p = f.Replace('\\', '/');
            int i = n.IndexOf("_diffa");
            if (i > 0) { string k = n.Substring(0, i); diff[k] = p; alpha.Add(k); continue; }
            i = n.IndexOf("_diff");
            if (i > 0) { string k = n.Substring(0, i); if (!alpha.Contains(k)) diff[k] = p; continue; }
            i = n.IndexOf("_nor_");
            if (i > 0) nrm[n.Substring(0, i)] = p;
        }
    }

    // Какой набор текстур подходит материалу: точное имя → самый длинный префикс → больше всего общих букв (лучше без прозрачности)
    static string Best(string mat, IEnumerable<string> prefixes, HashSet<string> alpha)
    {
        string best = null; int score = -1;
        foreach (string p in prefixes)
        {
            int s;
            if (p == mat) s = 10000;
            else if (mat.StartsWith(p)) s = 1000 + p.Length;
            else
            {
                int c = 0;
                while (c < p.Length && c < mat.Length && p[c] == mat[c]) c++;
                s = c * 2 + (alpha.Contains(p) ? 0 : 1);
            }
            if (s > score) { score = s; best = p; }
        }
        return best;
    }
}
