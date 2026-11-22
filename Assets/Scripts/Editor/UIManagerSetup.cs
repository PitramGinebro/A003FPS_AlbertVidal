using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Monta el UIManager sobre el HUD existente y añade el nombre del arma.</summary>
public static class UIManagerSetup
{
    [MenuItem("Tools/FPS/Setup UI Manager (HUD + hitmarker)", priority = 55)]
    private static void Run()
    {
        var hud = Object.FindAnyObjectByType<AmmoHUD>(FindObjectsInactive.Include);
        if (hud == null)
        {
            EditorUtility.DisplayDialog("UI Manager", "No hay HUD en la escena. Usa antes Tools > FPS > Add Ammo HUD To Open Scene.", "OK");
            return;
        }

        var so = new SerializedObject(hud);
        var panel = so.FindProperty("panel").objectReferenceValue as GameObject;
        if (panel != null && so.FindProperty("weaponNameText").objectReferenceValue == null)
        {
            var existing = panel.transform.Find("WeaponName");
            var label = existing != null ? existing.GetComponent<Text>() : CreateWeaponName(panel.transform);
            so.FindProperty("weaponNameText").objectReferenceValue = label;
            so.ApplyModifiedProperties();
        }

        var manager = EnsureUIManager(hud.gameObject);
        Selection.activeGameObject = manager.gameObject;

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        var mso = new SerializedObject(manager);
        EditorUtility.DisplayDialog("UI Manager",
            "UIManager configurado en " + manager.gameObject.name +
            "\nWeaponManager: " + Ok(mso, "weaponManager") +
            "\nAmmoHUD: " + Ok(mso, "ammoHUD") +
            "\nHitMarker: " + Ok(mso, "hitMarker") +
            "\nNombre del arma: " + (new SerializedObject(hud).FindProperty("weaponNameText").objectReferenceValue != null ? "OK" : "falta"), "OK");
    }

    private static string Ok(SerializedObject so, string prop) => so.FindProperty(prop).objectReferenceValue != null ? "OK" : "falta";

    /// <summary>Añade (o reutiliza) el UIManager en el canvas y conecta todas sus referencias.</summary>
    internal static UIManager EnsureUIManager(GameObject canvasGO)
    {
        var manager = canvasGO.GetComponent<UIManager>();
        if (manager == null) manager = Undo.AddComponent<UIManager>(canvasGO);

        var so = new SerializedObject(manager);
        so.FindProperty("weaponManager").objectReferenceValue = Object.FindAnyObjectByType<WeaponManager>(FindObjectsInactive.Include);
        so.FindProperty("ammoHUD").objectReferenceValue = canvasGO.GetComponentInChildren<AmmoHUD>(true);
        so.FindProperty("hitMarker").objectReferenceValue = canvasGO.GetComponentInChildren<HitMarkerUI>(true);
        so.ApplyModifiedProperties();
        return manager;
    }

    /// <summary>Etiqueta con el nombre del arma, encima del bloque de munición y alineada a la derecha.</summary>
    internal static Text CreateWeaponName(Transform panel)
    {
        var go = new GameObject("WeaponName", typeof(RectTransform), typeof(Text), typeof(Shadow));
        Undo.RegisterCreatedObjectUndo(go, "Add Weapon Name");
        go.transform.SetParent(panel, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
        rt.offsetMin = new Vector2(0, 6); rt.offsetMax = new Vector2(-6, 32);
        var t = go.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 18; t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.LowerRight;
        t.color = new Color(0.94f, 0.92f, 0.86f, 0.7f);
        t.raycastTarget = false; t.text = "RIFLE";
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        var sh = go.GetComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.55f); sh.effectDistance = new Vector2(2f, -2f);
        return t;
    }
}
