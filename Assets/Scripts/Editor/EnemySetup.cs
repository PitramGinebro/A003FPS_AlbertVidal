using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Coloca el enemigo (skibiditoilet.fbx) cerca del spawn del jugador y añade el hitmarker al HUD.</summary>
public static class EnemySetup
{
    const string ModelPath = "Assets/Models/Enemy/skibiditoilet.fbx";
    const string MarkerPath = "Assets/Art/UI/hit_marker.png";
    const string PrefabPath = "Assets/Prefabs/Enemies/Enemy_SkibidiToilet.prefab";

    [MenuItem("Tools/FPS/Add Enemy + Hitmarker", priority = 60)]
    private static void Run()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) { EditorUtility.DisplayDialog("Enemy", "No encuentro " + ModelPath, "OK"); return; }

        var scene = EditorSceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        var player = roots.FirstOrDefault(r => r.name == "PlayerEntity");
        if (player == null) { EditorUtility.DisplayDialog("Enemy", "No encuentro PlayerEntity en la escena.", "OK"); return; }

        float playerH = 2f;
        var cap = player.GetComponentInChildren<CapsuleCollider>(true);
        if (cap != null) playerH = cap.height * cap.transform.lossyScale.y;

        // ---------- Enemigo ----------
        var old = roots.FirstOrDefault(r => r.name == "Enemy_SkibidiToilet");
        if (old != null) Undo.DestroyObjectImmediate(old);

        var root = new GameObject("Enemy_SkibidiToilet");
        Undo.RegisterCreatedObjectUndo(root, "Add Enemy");
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
        visual.name = "Model";
        visual.transform.SetParent(root.transform, false);

        // Escala: que mida como el jugador
        Bounds b = WorldBounds(visual);
        float scale = b.size.y > 0.0001f ? playerH / b.size.y : 1f;
        visual.transform.localScale *= scale;
        b = WorldBounds(visual);
        // Pivote abajo y centrado
        visual.transform.position += new Vector3(-b.center.x, -b.min.y, -b.center.z);
        b = WorldBounds(visual);

        var box = root.AddComponent<BoxCollider>();
        box.center = b.center;
        box.size = b.size;
        root.AddComponent<Health>();

        // Posición: delante del jugador, en un hueco libre y con línea de visión
        Physics.SyncTransforms();
        Vector3 eye = player.transform.position + Vector3.up * playerH * 0.4f;
        Vector3 fwd = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        Vector3 spot = player.transform.position + fwd * 6f;
        bool placed = false;
        foreach (float dist in new[] { 7f, 5f, 9f, 4f })
        foreach (float ang in new[] { 0f, 25f, -25f, 50f, -50f, 90f, -90f, 135f, -135f, 180f })
        {
            Vector3 dir = Quaternion.Euler(0, ang, 0) * fwd;
            Vector3 p = player.transform.position + dir * dist;
            if (!Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out RaycastHit ground, 40f)) continue;
            p.y = ground.point.y;
            Vector3 c = p + Vector3.up * (b.size.y * 0.5f + 0.05f);
            var blockers = Physics.OverlapBox(c, b.extents * 1.1f, Quaternion.identity)
                                  .Where(col => !col.transform.IsChildOf(player.transform) && col != box && col.name != "Floor" && col.name != "Plane");
            if (blockers.Any()) continue;
            Vector3 to = c - eye;
            if (Physics.Raycast(eye, to.normalized, out RaycastHit los, to.magnitude) && !los.collider.transform.IsChildOf(player.transform) && los.collider != box) continue;
            spot = p; placed = true; break;
        }
        root.transform.position = spot;
        Vector3 look = player.transform.position - spot; look.y = 0;
        if (look.sqrMagnitude > 0.01f) root.transform.rotation = Quaternion.LookRotation(look);

        // Guardar como prefab para reutilizarlo
        System.IO.Directory.CreateDirectory("Assets/Prefabs/Enemies");
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.UserAction);

        // ---------- Hitmarker ----------
        var ti = (TextureImporter)AssetImporter.GetAtPath(MarkerPath);
        if (ti != null && ti.textureType != TextureImporterType.Sprite)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
        }
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MarkerPath);

        var hud = Object.FindAnyObjectByType<AmmoHUD>(FindObjectsInactive.Include);
        Transform canvas = hud != null ? hud.transform : null;
        if (canvas == null)
        {
            var cgo = new GameObject("HUD_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Undo.RegisterCreatedObjectUndo(cgo, "Add HUD");
            cgo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var sc = cgo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            canvas = cgo.transform;
        }
        var oldMarker = canvas.Find("HitMarker");
        if (oldMarker != null) Undo.DestroyObjectImmediate(oldMarker.gameObject);

        var mgo = new GameObject("HitMarker", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(mgo, "Add HitMarker");
        mgo.transform.SetParent(canvas, false);
        var rt = (RectTransform)mgo.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(56, 56);
        var img = mgo.GetComponent<Image>();
        img.sprite = sprite; img.raycastTarget = false; img.preserveAspect = true;
        img.color = new Color(1, 1, 1, 0);
        var hm = mgo.AddComponent<HitMarkerUI>();
        var so = new SerializedObject(hm);
        so.FindProperty("marker").objectReferenceValue = img;
        so.ApplyModifiedPropertiesWithoutUndo();
        UIManagerSetup.EnsureUIManager(canvas.gameObject);

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        EditorUtility.DisplayDialog("Enemy + Hitmarker",
            $"Enemigo colocado a {Vector3.Distance(player.transform.position, spot):0.0} m del jugador" +
            (placed ? "" : " (no encontré hueco libre, revisa la posición)") +
            $"\nAltura {b.size.y:0.00} m, escala modelo x{scale:0.###}\nHitmarker: {(sprite != null ? "OK" : "sin sprite")}", "OK");
    }

    private static Bounds WorldBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}
