using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// World Space Canvas 위에 P1 발판 윗면을 평면도로 그린다.
public class Round2RouteBoard : MaskableGraphic
{
    [SerializeField] private Round2Spawner spawner;
    [SerializeField] private Transform player1Tiles;
    private bool wasVisible;

    private void LateUpdate()
    {
        bool visible = spawner && spawner.LocalPlayerSlot == 2;
        if (visible == wasVisible)
            return;
        wasVisible = visible;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (!player1Tiles || (Application.isPlaying && !wasVisible))
            return;

        var shapes = new List<Vector2[]>();
        Vector2 min = Vector2.positiveInfinity;
        Vector2 max = Vector2.negativeInfinity;
        foreach (Transform tile in player1Tiles)
        {
            BoxCollider box = tile.GetComponent<BoxCollider>();
            if (!box) continue;

            Vector3 half = box.size * 0.5f;
            Vector2[] corners = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                float x = i < 2 ? -half.x : half.x;
                float z = (i == 0 || i == 3) ? -half.z : half.z;
                Vector3 world = tile.TransformPoint(box.center + new Vector3(x, half.y, z));
                Vector3 local = player1Tiles.InverseTransformPoint(world);
                corners[i] = new Vector2(local.x, local.z);
                min = Vector2.Min(min, corners[i]);
                max = Vector2.Max(max, corners[i]);
            }
            shapes.Add(corners);
        }
        if (shapes.Count == 0) return;

        Rect area = rectTransform.rect;
        Vector2 extent = max - min;
        float scale = 0.88f * Mathf.Min(
            area.width / Mathf.Max(0.01f, extent.x),
            area.height / Mathf.Max(0.01f, extent.y));
        Vector2 center = (min + max) * 0.5f;
        for (int i = 0; i < shapes.Count; i++)
        {
            Color32 tint = Color.white;
            if (i == 0) tint = new Color32(75, 218, 227, 255);
            else if (i == shapes.Count - 1) tint = new Color32(255, 199, 80, 255);

            int start = mesh.currentVertCount;
            foreach (Vector2 corner in shapes[i])
                mesh.AddVert(area.center + (corner - center) * scale, tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
