using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class AmmoHUDBuilder
{
    static readonly Color Ink = new Color(0.94f, 0.92f, 0.86f);
    static readonly Color Amber = new Color(1f, 0.64f, 0.16f);

    [MenuItem("Tools/FPS/Add Ammo HUD To Open Scene", priority = 50)]
    private static void Build()
    {
        // Sustituye el HUD anterior si existe
        var existing = Object.FindAnyObjectByType<AmmoHUD>(FindObjectsInactive.Include);
        var keepMarker = Object.FindAnyObjectByType<HitMarkerUI>(FindObjectsInactive.Include);
        if (keepMarker != null) Undo.SetTransformParent(keepMarker.transform, null, "Keep HitMarker");
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasGO = new GameObject("HUD_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Add Ammo HUD");
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = offMin; rt.offsetMax = offMax;
            return rt;
        }
        Image Bar(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax, Color c)
        {
            var img = Rect(name, parent, aMin, aMax, offMin, offMax).gameObject.AddComponent<Image>();
            img.color = c; img.raycastTarget = false;
            return img;
        }
        Text Label(string name, Transform parent, int size, TextAnchor align, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax, FontStyle style, Color color, string sample)
        {
            var rt = Rect(name, parent, aMin, aMax, offMin, offMax);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.alignment = align; t.fontStyle = style;
            t.color = color; t.raycastTarget = false; t.text = sample;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = rt.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.55f); sh.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        // Bloque abajo a la derecha, sin caja de fondo
        var panel = Rect("AmmoPanel", canvasGO.transform, new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, Vector2.zero);
        panel.pivot = new Vector2(1, 0);
        panel.sizeDelta = new Vector2(400, 150);
        panel.anchoredPosition = new Vector2(-64, 52);

        // Fila de balas inclinadas (arriba)
        var pips = Rect("Pips", panel, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -30), new Vector2(-6, 0));
        var layout = pips.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.spacing = 4f;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        // Número grande del cargador
        var magazine = Label("Magazine", panel, 104, TextAnchor.LowerRight,
            new Vector2(0, 0), new Vector2(0.66f, 1), new Vector2(0, 10), new Vector2(0, -30), FontStyle.Bold, Ink, "30");
        magazine.rectTransform.pivot = new Vector2(1f, 0.25f);

        // Separador inclinado en ámbar
        var slash = Bar("Slash", panel, new Vector2(0.66f, 0), new Vector2(0.66f, 0), Vector2.zero, Vector2.zero, Amber);
        slash.rectTransform.sizeDelta = new Vector2(5, 82);
        slash.rectTransform.anchoredPosition = new Vector2(18, 58);
        slash.rectTransform.localRotation = Quaternion.Euler(0, 0, -18);

        // Reserva
        var resLabelColor = Ink; resLabelColor.a = 0.45f;
        Label("ReserveLabel", panel, 14, TextAnchor.LowerLeft,
            new Vector2(0.66f, 0), new Vector2(1, 0), new Vector2(40, 64), new Vector2(0, 84), FontStyle.Bold, resLabelColor, "RESERVA");
        var resColor = Ink; resColor.a = 0.75f;
        var reserve = Label("Reserve", panel, 40, TextAnchor.LowerLeft,
            new Vector2(0.66f, 0), new Vector2(1, 0), new Vector2(40, 18), new Vector2(0, 66), FontStyle.Normal, resColor, "090");

        // Línea base + barra de recarga
        var trackColor = Ink; trackColor.a = 0.18f;
        var track = Bar("Track", panel, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 2), trackColor);
        Bar("Tick", panel, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-2, 0), new Vector2(0, 14), Amber);
        var fill = Bar("ReloadFill", track.transform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, -1), new Vector2(0, 1), Amber);
        fill.gameObject.SetActive(false);

        // Aviso central (recargando / sin munición / pulsa para recargar)
        var status = Label("Status", canvasGO.transform, 24, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, -130), new Vector2(300, -96), FontStyle.Bold, Amber, "RECARGANDO");
        status.gameObject.SetActive(false);

        var hud = canvasGO.AddComponent<AmmoHUD>();
        var so = new SerializedObject(hud);
        so.FindProperty("panel").objectReferenceValue = panel.gameObject;
        so.FindProperty("magazineText").objectReferenceValue = magazine;
        so.FindProperty("reserveText").objectReferenceValue = reserve;
        so.FindProperty("statusText").objectReferenceValue = status;
        so.FindProperty("pipContainer").objectReferenceValue = pips;
        so.FindProperty("reloadFill").objectReferenceValue = fill.rectTransform;
        so.FindProperty("weaponNameText").objectReferenceValue = UIManagerSetup.CreateWeaponName(panel);
        so.ApplyModifiedPropertiesWithoutUndo();

        if (keepMarker != null) Undo.SetTransformParent(keepMarker.transform, canvasGO.transform, "Keep HitMarker");
        UIManagerSetup.EnsureUIManager(canvasGO);

        Selection.activeGameObject = canvasGO;
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
