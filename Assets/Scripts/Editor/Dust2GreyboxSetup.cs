using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Ajusta el grey box Dust2 de la escena abierta:
///  - escala relativa a la altura del jugador (el blockout se diseñó para ~1.8 m, 1 u = 1 m)
///  - MeshCollider estático en todas sus piezas
///  - suelo del mapa a la altura del Plane y objetos existentes recolocados sobre él (zona T)
public static class Dust2GreyboxSetup
{
    const float DesignPlayerHeight = 1.8f;
    const float DesignMapSize = 120f;
    // Punto libre de la zona de spawn T, en coordenadas del blockout (Blender X,Y)
    static readonly Vector2 DropPoint = new Vector2(80f, 11f);

    [MenuItem("Tools/FPS/Fix Dust2 Greybox (scale, colliders, ground)", priority = 40)]
    static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        var map = roots.FirstOrDefault(r => r.name.StartsWith("Dust2_Greybox"));
        if (map == null)
        {
            EditorUtility.DisplayDialog("Dust2 Greybox", "No encuentro 'Dust2_Greybox' en la escena.", "OK");
            return;
        }

        var plane = roots.FirstOrDefault(r => r.name == "Plane");
        var player = roots.FirstOrDefault(r => r.name == "PlayerEntity");
        var movable = roots.Where(r => r != map && r != plane
                                       && r.GetComponentInChildren<Light>(true) == null
                                       && r.name != "Global Volume"
                                       && (r.GetComponentInChildren<Renderer>(true) != null || r.GetComponentInChildren<Collider>(true) != null))
                           .ToList();

        Undo.SetCurrentGroupName("Fix Dust2 Greybox");
        int group = Undo.GetCurrentGroup();
        Undo.RegisterFullObjectHierarchyUndo(map, "Fix Dust2 Greybox");
        foreach (var m in movable) Undo.RecordObject(m.transform, "Fix Dust2 Greybox");

        var all = map.GetComponentsInChildren<Transform>(true);
        Transform Find(string n) => all.FirstOrDefault(t => t.name == n);
        var floor = Find("Floor");
        var floorR = floor != null ? floor.GetComponent<Renderer>() : null;
        if (floorR == null)
        {
            EditorUtility.DisplayDialog("Dust2 Greybox", "No encuentro la pieza 'Floor' dentro del mapa.", "OK");
            return;
        }

        // 1) Escala
        float playerH = 2f;
        if (player != null)
        {
            var cap = player.GetComponentInChildren<CapsuleCollider>(true);
            if (cap != null) playerH = cap.height * cap.transform.lossyScale.y;
        }
        float width = Mathf.Max(floorR.bounds.size.x, floorR.bounds.size.z);
        float desired = DesignMapSize * playerH / DesignPlayerHeight;
        float k = desired / width;
        map.transform.localScale *= k;

        // 2) Colisiones
        int colliders = 0;
        foreach (var mf in map.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            var go = mf.gameObject;
            foreach (var c in go.GetComponents<Collider>()) Undo.DestroyObjectImmediate(c);
            var mc = Undo.AddComponent<MeshCollider>(go);
            mc.sharedMesh = mf.sharedMesh;
            go.isStatic = true;
            colliders++;
        }

        // 3) Altura: parte superior del suelo del mapa justo encima del Plane
        float groundY = 0f;
        if (plane != null)
        {
            var pr = plane.GetComponent<Renderer>();
            groundY = pr != null ? pr.bounds.max.y : plane.transform.position.y;
        }
        const float lift = 0.01f;
        float floorTop = floorR.bounds.max.y;
        map.transform.position += Vector3.up * (groundY + lift - floorTop);

        // 4) Recolocar objetos existentes dentro del mapa, en zona libre del spawn T
        string moved = "no movidos (faltan los empties Spawn_T/Spawn_CT/Site_B)";
        var sT = Find("Spawn_T"); var sCT = Find("Spawn_CT"); var sB = Find("Site_B");
        if (sT != null && sCT != null && sB != null && movable.Count > 0)
        {
            Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
            Vector3 T = Flat(sT.position), CT = Flat(sCT.position), B = Flat(sB.position);
            Vector3 vy = (CT - T) / 98f;                       // +Y del blockout
            Vector3 vx = -((B - T) - 80f * vy) / 36f;          // +X del blockout
            Vector3 target = T + (DropPoint.x - 60f) * vx + (DropPoint.y - 10f) * vy;

            Vector3 centroid = Vector3.zero;
            foreach (var m in movable) centroid += Flat(m.transform.position);
            centroid /= movable.Count;

            Vector3 delta = target - centroid;
            delta.y = lift; // seguían sobre el Plane; el suelo del mapa queda 1 cm por encima
            foreach (var m in movable) m.transform.position += delta;
            moved = string.Join(", ", movable.Select(m => m.name));
        }

        // 5) Aviso si algo ha quedado dentro de una pared
        Physics.SyncTransforms();
        var mapCols = new HashSet<Collider>(map.GetComponentsInChildren<Collider>(true));
        var clipping = new List<string>();
        foreach (var m in movable)
        {
            var rs = m.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) continue;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var ext = b.extents * 0.9f;
            var center = b.center + Vector3.up * 0.05f;
            if (Physics.OverlapBox(center, ext).Any(c => mapCols.Contains(c) && c.name != "Floor"))
                clipping.Add(m.name);
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        string msg = $"Escala x{k:0.###} (jugador {playerH:0.##} m, mapa ~{desired:0} m)\n" +
                     $"MeshColliders: {colliders}\nObjetos recolocados: {moved}" +
                     (clipping.Count > 0 ? $"\n\nOJO, se solapan con el mapa: {string.Join(", ", clipping)}" : "");
        Debug.Log("[Dust2Greybox] " + msg.Replace("\n", " | "));
        EditorUtility.DisplayDialog("Dust2 Greybox", msg, "OK");
    }
}
