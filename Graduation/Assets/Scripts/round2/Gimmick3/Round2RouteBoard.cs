using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 발판과 출발/도착 땅의 실제 배치를 P2 안내판에 표시한다.
[RequireComponent(typeof(CanvasRenderer))]
public class Round2RouteBoard : MaskableGraphic
{
    [Header("기존 연결")]
    [SerializeField] private Round2Spawner spawner;
    [SerializeField] private Transform player1Tiles;

    [Header("출발 땅 / 도착 땅 / 첫 발판")]
    [SerializeField] private Transform startGround;
    [SerializeField] private Transform goalGround;
    [SerializeField] private BoxCollider firstTile;

    [Header("안내판 표시 범위")]
    [SerializeField] private Vector2 minimumViewSize = new Vector2(20f, 20f);
    [SerializeField, Range(0f, 0.4f)] private float padding = 0.15f;

    private bool wasVisible;
    private float nextRefreshTime;

    private struct Shape
    {
        public Vector2[] corners;
        public Color32 tint;
    }

    private bool ShouldShow()
    {
        return !Application.isPlaying ||
               (spawner != null && spawner.LocalPlayerSlot == 2);
    }

    private void LateUpdate()
    {
        bool visible = ShouldShow();
        if (visible != wasVisible ||
            (visible && Time.unscaledTime >= nextRefreshTime))
        {
            wasVisible = visible;
            nextRefreshTime = Time.unscaledTime + 0.25f;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (player1Tiles == null || !ShouldShow())
            return;

        var shapes = new List<Shape>();

        // 작은 블록 여러 개로 만든 땅도 실제 배치대로 표시한다.
        AddGround(shapes, startGround, new Color32(70, 150, 255, 255));
        AddGround(shapes, goalGround, new Color32(255, 199, 80, 255));

        bool foundTile = false;
        foreach (Transform tile in player1Tiles)
        {
            BoxCollider box = tile.GetComponent<BoxCollider>();
            if (box == null || !tile.gameObject.activeInHierarchy)
                continue;

            bool isFirst = firstTile != null ? box == firstTile : !foundTile;
            foundTile = true;
            Color32 tint = isFirst
                ? new Color32(80, 240, 120, 255)
                : new Color32(245, 245, 245, 255);
            AddBox(shapes, box, tint);
        }

        if (shapes.Count == 0)
            return;

        Vector2 min = Vector2.positiveInfinity;
        Vector2 max = Vector2.negativeInfinity;
        foreach (Shape shape in shapes)
        {
            foreach (Vector2 corner in shape.corners)
            {
                min = Vector2.Min(min, corner);
                max = Vector2.Max(max, corner);
            }
        }

        // 짧은 경로의 과도한 확대를 막고, 긴 경로는 전체가 들어오도록 축소한다.
        Vector2 extent = max - min;
        extent.x = Mathf.Max(extent.x, Mathf.Max(0.1f, minimumViewSize.x));
        extent.y = Mathf.Max(extent.y, Mathf.Max(0.1f, minimumViewSize.y));
        Rect area = rectTransform.rect;
        float usable = 1f - 2f * Mathf.Clamp(padding, 0f, 0.4f);
        float scale = usable * Mathf.Min(area.width / extent.x, area.height / extent.y);
        Vector2 center = (min + max) * 0.5f;

        foreach (Shape shape in shapes)
        {
            int start = mesh.currentVertCount;
            foreach (Vector2 corner in shape.corners)
                mesh.AddVert(area.center + (corner - center) * scale, shape.tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }

    private void AddGround(List<Shape> shapes, Transform ground, Color32 tint)
    {
        if (ground == null)
            return;

        foreach (BoxCollider box in ground.GetComponentsInChildren<BoxCollider>())
        {
            if (box.enabled && !box.isTrigger && box.gameObject.activeInHierarchy)
                AddBox(shapes, box, tint);
        }
    }

    private void AddBox(List<Shape> shapes, BoxCollider box, Color32 tint)
    {
        Vector3 half = box.size * 0.5f;
        var corners = new Vector2[4];
        Quaternion inverseRotation = Quaternion.Inverse(player1Tiles.rotation);

        for (int i = 0; i < 4; i++)
        {
            float x = i < 2 ? -half.x : half.x;
            float z = (i == 0 || i == 3) ? -half.z : half.z;
            Vector3 world = box.transform.TransformPoint(
                box.center + new Vector3(x, half.y, z));
            Vector3 local = inverseRotation * (world - player1Tiles.position);
            corners[i] = new Vector2(local.x, local.z);
        }

        shapes.Add(new Shape { corners = corners, tint = tint });
    }
}
