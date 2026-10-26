using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class SoccerBallGenerator
{
    const string FolderPath = "Assets/SoccerBall";
    const float Radius = 0.5f;
    const int Subdivisions = 10;      // per fan triangle of each panel (minimum 8)
    const float SeamDepth = 0.015f;   // fraction of radius pushed inward at panel edges
    const float SeamWidth = 0.04f;    // radians from panel edge over which the seam blends out
    const float Bulge = 0.01f;        // fraction of radius pushed outward at panel interiors

    [MenuItem("Tools/Create Soccer Ball")]
    public static void CreateSoccerBall()
    {
        EnsureFolder();

        Mesh mesh = BuildMesh();
        Material black = CreateMaterial("SoccerBall_Black", Color.black);
        Material white = CreateMaterial("SoccerBall_White", Color.white);

        mesh = SaveAsset(mesh, FolderPath + "/SoccerBallMesh.asset");
        black = SaveAsset(black, FolderPath + "/SoccerBall_Black.mat");
        white = SaveAsset(white, FolderPath + "/SoccerBall_White.mat");
        AssetDatabase.SaveAssets();

        GameObject go = new GameObject("SoccerBall");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { black, white };
        go.AddComponent<SphereCollider>().radius = Radius;

        Undo.RegisterCreatedObjectUndo(go, "Create Soccer Ball");
        Selection.activeGameObject = go;
    }

    static Mesh BuildMesh()
    {
        float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
        Vector3[] ico =
        {
            new Vector3(-1,  phi, 0), new Vector3( 1,  phi, 0), new Vector3(-1, -phi, 0), new Vector3( 1, -phi, 0),
            new Vector3(0, -1,  phi), new Vector3(0,  1,  phi), new Vector3(0, -1, -phi), new Vector3(0,  1, -phi),
            new Vector3( phi, 0, -1), new Vector3( phi, 0,  1), new Vector3(-phi, 0, -1), new Vector3(-phi, 0,  1),
        };
        const float edge = 2f;

        bool Adjacent(int a, int b) => Mathf.Abs(Vector3.Distance(ico[a], ico[b]) - edge) < 0.01f;
        Vector3 Lerp3(int a, int b, float t) => Vector3.Lerp(ico[a], ico[b], t);

        var pentagons = new List<List<Vector3>>();
        var hexagons = new List<List<Vector3>>();

        // Pentagons: one per icosahedron vertex, made of points 1/3 along each edge.
        for (int i = 0; i < ico.Length; i++)
        {
            var pts = new List<Vector3>();
            for (int j = 0; j < ico.Length; j++)
                if (j != i && Adjacent(i, j))
                    pts.Add(Lerp3(i, j, 1f / 3f));
            pentagons.Add(SortAroundAxis(pts, ico[i]));
        }

        // Hexagons: one per icosahedron face.
        for (int a = 0; a < ico.Length; a++)
        for (int b = a + 1; b < ico.Length; b++)
        {
            if (!Adjacent(a, b)) continue;
            for (int c = b + 1; c < ico.Length; c++)
            {
                if (!Adjacent(a, c) || !Adjacent(b, c)) continue;
                hexagons.Add(new List<Vector3>
                {
                    Lerp3(a, b, 1f / 3f), Lerp3(a, b, 2f / 3f),
                    Lerp3(b, c, 1f / 3f), Lerp3(b, c, 2f / 3f),
                    Lerp3(c, a, 1f / 3f), Lerp3(c, a, 2f / 3f),
                });
            }
        }

        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var pentaTris = new List<int>();
        var hexaTris = new List<int>();

        foreach (var poly in pentagons) AddPolygon(poly, vertices, uvs, pentaTris);
        foreach (var poly in hexagons) AddPolygon(poly, vertices, uvs, hexaTris);

        var mesh = new Mesh { name = "SoccerBallMesh", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(pentaTris, 0);
        mesh.SetTriangles(hexaTris, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    static List<Vector3> SortAroundAxis(List<Vector3> pts, Vector3 axis)
    {
        Vector3 n = axis.normalized;
        Vector3 t1 = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 t2 = Vector3.Cross(n, t1);
        Vector3 center = Vector3.zero;
        foreach (var p in pts) center += p;
        center /= pts.Count;
        pts.Sort((p, q) =>
        {
            Vector3 dp = p - center, dq = q - center;
            float ap = Mathf.Atan2(Vector3.Dot(dp, t2), Vector3.Dot(dp, t1));
            float aq = Mathf.Atan2(Vector3.Dot(dq, t2), Vector3.Dot(dq, t1));
            return ap.CompareTo(aq);
        });
        return pts;
    }

    // Each panel gets its own vertices: welded inside the panel, never shared with neighbours.
    class Panel
    {
        public Vector3[] EdgePlaneNormals;
        public Dictionary<Vector3Int, int> Lookup = new Dictionary<Vector3Int, int>();
    }

    static void AddPolygon(List<Vector3> poly, List<Vector3> verts, List<Vector2> uvs, List<int> tris)
    {
        Vector3 center = Vector3.zero;
        foreach (var p in poly) center += p;
        center /= poly.Count;

        // Great-circle planes through the panel's edges, used for angular distance to the edge.
        var panel = new Panel { EdgePlaneNormals = new Vector3[poly.Count] };
        for (int i = 0; i < poly.Count; i++)
            panel.EdgePlaneNormals[i] = Vector3.Cross(poly[i].normalized, poly[(i + 1) % poly.Count].normalized).normalized;

        for (int i = 0; i < poly.Count; i++)
        {
            Vector3 a = center, b = poly[i], c = poly[(i + 1) % poly.Count];
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), center) < 0f)
            {
                Vector3 tmp = b; b = c; c = tmp;
            }
            SubdivideTriangle(a, b, c, panel, verts, uvs, tris);
        }
    }

    static void SubdivideTriangle(Vector3 a, Vector3 b, Vector3 c, Panel panel, List<Vector3> verts, List<Vector2> uvs, List<int> tris)
    {
        int n = Subdivisions;
        var grid = new int[n + 1, n + 1];

        // Grid point (i, j): i steps from a toward b, j steps from a toward c, i + j <= n.
        for (int i = 0; i <= n; i++)
        for (int j = 0; j <= n - i; j++)
        {
            Vector3 p = a + (b - a) * ((float)i / n) + (c - a) * ((float)j / n);
            grid[i, j] = GetOrAddVertex(p, panel, verts, uvs);
        }

        for (int i = 0; i < n; i++)
        for (int j = 0; j < n - i; j++)
        {
            tris.Add(grid[i, j]); tris.Add(grid[i + 1, j]); tris.Add(grid[i, j + 1]);
            if (j < n - i - 1)
            {
                tris.Add(grid[i + 1, j]); tris.Add(grid[i + 1, j + 1]); tris.Add(grid[i, j + 1]);
            }
        }
    }

    static int GetOrAddVertex(Vector3 p, Panel panel, List<Vector3> verts, List<Vector2> uvs)
    {
        var key = new Vector3Int(Mathf.RoundToInt(p.x * 100000f), Mathf.RoundToInt(p.y * 100000f), Mathf.RoundToInt(p.z * 100000f));
        if (panel.Lookup.TryGetValue(key, out int index)) return index;

        Vector3 dir = p.normalized;

        float d = float.MaxValue;
        foreach (var plane in panel.EdgePlaneNormals)
            d = Mathf.Min(d, Mathf.Asin(Mathf.Clamp(Mathf.Abs(Vector3.Dot(dir, plane)), 0f, 1f)));

        float x = Mathf.Clamp01(d / SeamWidth);
        float t = x * x * (3f - 2f * x);
        float r = Radius * (1f + Bulge * t - SeamDepth * (1f - t));

        index = verts.Count;
        verts.Add(dir * r);
        uvs.Add(new Vector2(0.5f + Mathf.Atan2(dir.z, dir.x) / (2f * Mathf.PI), 0.5f + Mathf.Asin(dir.y) / Mathf.PI));
        panel.Lookup.Add(key, index);
        return index;
    }

    static Material CreateMaterial(string name, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader) { name = name };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.4f);
        return mat;
    }

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(FolderPath))
            AssetDatabase.CreateFolder("Assets", "SoccerBall");
    }

    static T SaveAsset<T>(T asset, string path) where T : Object
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(asset, existing);
            existing.name = asset.name;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(asset);
            return existing;
        }
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
