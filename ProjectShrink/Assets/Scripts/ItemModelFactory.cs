using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Xưởng tạo Asset 3D cho toàn bộ hệ thống vật phẩm và công thức chế tạo:
/// - Mạng Nhện, Gai Độc, Xương Dài, Giáo Gai Độc
/// - Nguyên liệu cơ bản: Gỗ (wood), Cây Gậy (stick), Dây Thừng (rope), Vải (cloth), Đá (stone), Than (coal)
/// - Vật phẩm chế tạo mới: Đuốc Phát Sáng (torch), Thang Leo (ladder), Balo 24 Ô (backpack), Búa Đá (stone_hammer)
/// - Tự động Render Icon 2D sắc nét đồng bộ từ mô hình 3D (BakeAllIcons).
/// </summary>
public static class ItemModelFactory
{
    public const string IdSpiderSilk = "spider_silk";
    public const string IdPoisonThorn = "poison_thorn";
    public const string IdLongBone = "long_bone";
    public const string IdPoisonSpear = "poison_thorn_spear";

    public const string IdWood = "wood";
    public const string IdStick = "stick";
    public const string IdRope = "rope";
    public const string IdCloth = "cloth";
    public const string IdStone = "stone";
    public const string IdCoal = "coal";

    public const string IdTorch = "torch";
    public const string IdLadder = "ladder";
    public const string IdBackpack = "backpack";
    public const string IdStoneHammer = "stone_hammer";

    public static readonly string[] AllModelIds =
    {
        IdSpiderSilk, IdPoisonThorn, IdLongBone, IdPoisonSpear,
        IdWood, IdStick, IdRope, IdCloth, IdStone, IdCoal,
        IdTorch, IdLadder, IdBackpack, IdStoneHammer
    };

    private static Shader litShader;
    private static Mesh unitSphere;
    private static readonly Dictionary<string, Texture2D> iconCache = new Dictionary<string, Texture2D>();
    private static bool iconsBaked = false;

    // =========================================================================
    // VẬT LIỆU
    // =========================================================================
    public static Material MakeMat(Color color, float smoothness = 0.35f, float emission = 0f)
    {
        if (litShader == null)
        {
            litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");
        }

        Material m = new Material(litShader);
        m.SetColor("_BaseColor", color);
        m.SetColor("_Color", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Glossiness", smoothness);
        if (emission > 0.01f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * emission);
        }
        return m;
    }

    // =========================================================================
    // BỘ TẠO MESH CƠ BẢN
    // =========================================================================
    private static Mesh Finish(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
    {
        Mesh m = new Mesh();
        m.name = name;
        m.SetVertices(v);
        m.SetNormals(n);
        m.SetUVs(0, uv);
        m.SetTriangles(t, 0);
        m.RecalculateBounds();
        return m;
    }

    private static void AddGridTriangles(List<int> t, int rows, int seg)
    {
        for (int i = 0; i < rows - 1; i++)
        {
            for (int j = 0; j < seg; j++)
            {
                int a = i * (seg + 1) + j;
                int b = a + 1;
                int c = a + seg + 1;
                int d = c + 1;
                t.Add(a); t.Add(c); t.Add(b);
                t.Add(b); t.Add(c); t.Add(d);
            }
        }
    }

    /// <summary>Mặt tròn xoay quanh trục Y. profile: x = bán kính, y = độ cao (đi từ dưới lên trên).</summary>
    public static Mesh Lathe(string name, Vector2[] profile, int seg)
    {
        int n = profile.Length;
        List<Vector3> v = new List<Vector3>();
        List<Vector3> nr = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> t = new List<int>();

        for (int i = 0; i < n; i++)
        {
            Vector2 prev = profile[Mathf.Max(i - 1, 0)];
            Vector2 next = profile[Mathf.Min(i + 1, n - 1)];
            Vector2 tan = next - prev;
            Vector2 n2 = new Vector2(tan.y, -tan.x);
            if (n2.sqrMagnitude < 1e-8f) n2 = new Vector2(1f, 0f);
            n2.Normalize();

            for (int j = 0; j <= seg; j++)
            {
                float a = j / (float)seg * Mathf.PI * 2f;
                float cs = Mathf.Cos(a);
                float sn = Mathf.Sin(a);
                v.Add(new Vector3(profile[i].x * cs, profile[i].y, profile[i].x * sn));
                nr.Add(new Vector3(n2.x * cs, n2.y, n2.x * sn).normalized);
                uv.Add(new Vector2(j / (float)seg, i / (float)(n - 1)));
            }
        }

        AddGridTriangles(t, n, seg);
        return Finish(name, v, nr, uv, t);
    }

    /// <summary>Ống quét theo đường cong (path) với bán kính thay đổi (radii)</summary>
    public static Mesh Tube(string name, Vector3[] path, float[] radii, int seg, Vector3 side)
    {
        int n = path.Length;
        List<Vector3> v = new List<Vector3>();
        List<Vector3> nr = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> t = new List<int>();

        for (int i = 0; i < n; i++)
        {
            int ip = Mathf.Max(i - 1, 0);
            int inx = Mathf.Min(i + 1, n - 1);
            Vector3 dirVec = path[inx] - path[ip];
            float len = Mathf.Max(0.0001f, dirVec.magnitude);
            Vector3 T = dirVec / len;
            Vector3 N = Vector3.Cross(T, side).normalized;
            Vector3 B = Vector3.Cross(N, T);
            float slope = (radii[inx] - radii[ip]) / len;

            for (int j = 0; j <= seg; j++)
            {
                float a = j / (float)seg * Mathf.PI * 2f;
                Vector3 dir = Mathf.Cos(a) * N + Mathf.Sin(a) * B;
                v.Add(path[i] + dir * radii[i]);
                nr.Add((dir - T * slope).normalized);
                uv.Add(new Vector2(j / (float)seg, i / (float)(n - 1)));
            }
        }

        AddGridTriangles(t, n, seg);
        return Finish(name, v, nr, uv, t);
    }

    /// <summary>Vòng xuyến (torus) nằm trong mặt phẳng XZ, trục Y</summary>
    public static Mesh Torus(string name, float majorR, float minorR, int segMajor, int segMinor)
    {
        List<Vector3> v = new List<Vector3>();
        List<Vector3> nr = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> t = new List<int>();

        for (int i = 0; i <= segMajor; i++)
        {
            float u = i / (float)segMajor * Mathf.PI * 2f;
            Vector3 center = new Vector3(Mathf.Cos(u) * majorR, 0f, Mathf.Sin(u) * majorR);
            for (int j = 0; j <= segMinor; j++)
            {
                float a = j / (float)segMinor * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(u) * Mathf.Cos(a), Mathf.Sin(a), Mathf.Sin(u) * Mathf.Cos(a));
                v.Add(center + dir * minorR);
                nr.Add(dir);
                uv.Add(new Vector2(i / (float)segMajor, j / (float)segMinor));
            }
        }

        for (int i = 0; i < segMajor; i++)
        {
            for (int j = 0; j < segMinor; j++)
            {
                int a = i * (segMinor + 1) + j;
                int b = a + 1;
                int c = a + segMinor + 1;
                int d = c + 1;
                t.Add(a); t.Add(b); t.Add(c);
                t.Add(c); t.Add(b); t.Add(d);
            }
        }
        return Finish(name, v, nr, uv, t);
    }

    /// <summary>Mesh hình khối hộp chữ nhật sắc nét</summary>
    public static Mesh BoxMesh(string name, Vector3 size)
    {
        Vector3 h = size * 0.5f;
        List<Vector3> v = new List<Vector3>();
        List<Vector3> nr = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> t = new List<int>();

        System.Action<Vector3, Vector3, Vector3, Vector3, Vector3> addFace = (p0, p1, p2, p3, norm) =>
        {
            int baseIdx = v.Count;
            v.Add(p0); v.Add(p1); v.Add(p2); v.Add(p3);
            nr.Add(norm); nr.Add(norm); nr.Add(norm); nr.Add(norm);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
            t.Add(baseIdx); t.Add(baseIdx + 1); t.Add(baseIdx + 2);
            t.Add(baseIdx); t.Add(baseIdx + 2); t.Add(baseIdx + 3);
        };

        // Front, Back, Top, Bottom, Right, Left
        addFace(new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z), Vector3.forward);
        addFace(new Vector3(h.x, -h.y, -h.z), new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, h.y, -h.z), new Vector3(h.x, h.y, -h.z), Vector3.back);
        addFace(new Vector3(-h.x, h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z), Vector3.up);
        addFace(new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, -h.y, h.z), new Vector3(-h.x, -h.y, h.z), Vector3.down);
        addFace(new Vector3(h.x, -h.y, h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(h.x, h.y, h.z), Vector3.right);
        addFace(new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, h.y, h.z), new Vector3(-h.x, h.y, -h.z), Vector3.left);

        return Finish(name, v, nr, uv, t);
    }

    private static Mesh UnitSphere()
    {
        if (unitSphere != null) return unitSphere;
        int rings = 12;
        Vector2[] p = new Vector2[rings + 1];
        for (int k = 0; k <= rings; k++)
        {
            float a = Mathf.Lerp(-90f, 90f, k / (float)rings) * Mathf.Deg2Rad;
            p[k] = new Vector2((k == 0 || k == rings) ? 0f : 0.5f * Mathf.Cos(a), 0.5f * Mathf.Sin(a));
        }
        unitSphere = Lathe("UnitSphere", p, 20);
        return unitSphere;
    }

    public static GameObject Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 euler, Vector3 scale)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        return go;
    }

    public static GameObject Ball(Transform parent, string name, Material mat, Vector3 pos, Vector3 diameters)
    {
        return Part(parent, name, UnitSphere(), mat, pos, Vector3.zero, diameters);
    }

    public static GameObject Ring(Transform parent, string name, Material mat, Vector3 pos, Vector3 euler, float majorR, float minorR)
    {
        return Part(parent, name, Torus(name, majorR, minorR, 28, 8), mat, pos, euler, Vector3.one);
    }

    public static GameObject Box(Transform parent, string name, Material mat, Vector3 pos, Vector3 euler, Vector3 size)
    {
        return Part(parent, name, BoxMesh(name, size), mat, pos, euler, Vector3.one);
    }

    private static void AddThorn(Transform parent, string name, float length, float baseR, float curve,
        Vector3 pos, Vector3 euler, Material matBase, Material matTip)
    {
        int count = 16;
        Vector3[] path = new Vector3[count];
        float[] radii = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (count - 1f);
            path[i] = new Vector3(curve * length * t * t, length * t, 0f);
            radii[i] = baseR * Mathf.Pow(1f - t, 0.9f);
        }

        int split = Mathf.RoundToInt((count - 1) * 0.45f);

        Vector3[] pA = new Vector3[split + 1];
        float[] rA = new float[split + 1];
        System.Array.Copy(path, 0, pA, 0, split + 1);
        System.Array.Copy(radii, 0, rA, 0, split + 1);

        Vector3[] pB = new Vector3[count - split];
        float[] rB = new float[count - split];
        System.Array.Copy(path, split, pB, 0, count - split);
        System.Array.Copy(radii, split, rB, 0, count - split);

        GameObject holder = new GameObject(name);
        holder.transform.SetParent(parent, false);
        holder.transform.localPosition = pos;
        holder.transform.localRotation = Quaternion.Euler(euler);

        Part(holder.transform, name + "_Base", Tube(name + "_Base", pA, rA, 14, Vector3.forward), matBase, Vector3.zero, Vector3.zero, Vector3.one);
        Part(holder.transform, name + "_Tip", Tube(name + "_Tip", pB, rB, 14, Vector3.forward), matTip, Vector3.zero, Vector3.zero, Vector3.one);
    }

    // =========================================================================
    // MÔ HÌNH VẬT PHẨM (BUILDERS)
    // =========================================================================
    private static readonly Dictionary<string, string[]> CustomModelNames = new Dictionary<string, string[]>
    {
        { IdPoisonSpear, new string[] { "poison_thorn_spear", "giao_doc", "giao doc" } },
        { IdTorch,       new string[] { "torch", "cay_duoc", "cay duoc", "duoc" } },
        { IdPoisonThorn, new string[] { "poison_thorn", "gai doc", "gai_doc" } },
        { IdSpiderSilk,  new string[] { "spider_silk", "mang_nhen", "mang nhen" } },
        { IdLongBone,    new string[] { "long_bone", "xuong_dai", "xuong dai" } },
        { IdStick,       new string[] { "stick", "canh_cay", "canh cay" } },
        { IdWood,        new string[] { "wood", "van_go", "van go" } },
        { IdCoal,        new string[] { "coal", "than_da", "than da" } },
        { IdStone,       new string[] { "stone", "khoi_da", "khoi da" } },
        { IdLadder,      new string[] { "ladder", "thang_leo", "thang leo" } },
        { IdBackpack,    new string[] { "backpack", "ba_lo", "ba lo" } },
        { IdStoneHammer, new string[] { "stone_hammer", "bua_da", "bua da" } },
    };

    public static GameObject TryLoadCustomModel(string itemId)
    {
        if (!CustomModelNames.TryGetValue(itemId, out string[] names))
            names = new string[] { itemId };

        GameObject prefab = null;

        // 1. Thử load từ Resources/ItemModels/ hoặc Resources/Item/
        foreach (string name in names)
        {
            prefab = Resources.Load<GameObject>("ItemModels/" + name);
            if (prefab != null) break;
            prefab = Resources.Load<GameObject>("Item/" + name);
            if (prefab != null) break;
            prefab = Resources.Load<GameObject>(name);
            if (prefab != null) break;
        }

#if UNITY_EDITOR
        // 2. Trong Unity Editor: Thử load trực tiếp từ Assets/Item/
        if (prefab == null)
        {
            foreach (string name in names)
            {
                string[] paths = new string[]
                {
                    $"Assets/Item/{name}.glb",
                    $"Assets/Item/{name}.fbx",
                    $"Assets/Item/{name}.obj",
                    $"Assets/Resources/ItemModels/{name}.glb",
                    $"Assets/Resources/ItemModels/{name}.fbx"
                };
                foreach (string p in paths)
                {
                    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    if (prefab != null) break;
                }
                if (prefab != null) break;
            }
        }
#endif

        if (prefab == null) return null;

        GameObject root = new GameObject("Item_" + itemId);
        GameObject child = Object.Instantiate(prefab, root.transform);
        child.name = "ModelMesh";

        Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                b.Encapsulate(renderers[i].bounds);
            }

            float maxExtent = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (maxExtent > 0.0001f)
            {
                float normScale = 1.0f / maxExtent;
                child.transform.localScale = Vector3.one * normScale;
                Vector3 centerOffset = -child.transform.InverseTransformPoint(b.center);
                child.transform.localPosition = centerOffset * normScale;
            }
        }

        return root;
    }

    private static Bounds CalculateWorldBounds(Renderer[] renderers)
    {
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            b.Encapsulate(renderers[i].bounds);
        }
        return b;
    }

    /// <summary>
    /// Tự động xoay vũ khí (Giáo, Đuốc) để trục dài nhất nằm thẳng đứng dọc theo trục +Y của root,
    /// chuẩn hóa chiều dài dọc trục Y = 1.0 đơn vị, và đặt điểm tay cầm (gripNormalizedY từ đáy 0..1)
    /// nằm chính xác tuyệt đối tại tâm (0, 0, 0) của root.
    /// </summary>
    private static void AlignWeaponVerticallyAndSetGrip(GameObject root, Quaternion[] candidateRotations, float gripNormalizedY)
    {
        if (root == null) return;
        Transform meshChild = root.transform.Find("ModelMesh");
        if (meshChild == null) return;

        Vector3 savedPos = root.transform.position;
        Quaternion savedRot = root.transform.rotation;
        Vector3 savedScale = root.transform.localScale;
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        GameObject pivot = new GameObject("WeaponPivot");
        pivot.transform.SetParent(root.transform, false);
        meshChild.SetParent(pivot.transform, false);

        Renderer[] renderers = pivot.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Quaternion bestRot = candidateRotations != null && candidateRotations.Length > 0
                ? candidateRotations[0]
                : Quaternion.identity;
            float bestScore = -1f;

            if (candidateRotations != null && candidateRotations.Length > 0)
            {
                for (int i = 0; i < candidateRotations.Length; i++)
                {
                    pivot.transform.localRotation = candidateRotations[i];
                    Bounds testB = CalculateWorldBounds(renderers);
                    float horiz = Mathf.Max(0.0001f, Mathf.Max(testB.size.x, testB.size.z));
                    float score = testB.size.y / horiz;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestRot = candidateRotations[i];
                    }
                }
            }

            pivot.transform.localRotation = bestRot;
            Bounds b = CalculateWorldBounds(renderers);

            if (b.size.y > 0.0001f)
            {
                float scaleY = 1.0f / b.size.y;
                pivot.transform.localScale = Vector3.one * scaleY;
                b = CalculateWorldBounds(renderers);
            }

            float gripY = Mathf.Lerp(b.min.y, b.max.y, gripNormalizedY);
            Vector3 gripPointWorld = new Vector3(b.center.x, gripY, b.center.z);
            pivot.transform.localPosition -= gripPointWorld;
        }

        root.transform.position = savedPos;
        root.transform.rotation = savedRot;
        root.transform.localScale = savedScale;
    }

    public static GameObject Build(string itemId)
    {
        GameObject custom = TryLoadCustomModel(itemId);
        if (custom != null) return custom;

        switch (itemId)
        {
            case IdSpiderSilk: return BuildSilk();
            case IdPoisonThorn: return BuildThorn();
            case IdLongBone: return BuildBone();
            case IdPoisonSpear: return BuildSpear();
            case IdWood: return BuildWood();
            case IdStick: return BuildStick();
            case IdRope: return BuildRope();
            case IdCloth: return BuildCloth();
            case IdStone: return BuildStone();
            case IdCoal: return BuildCoal();
            case IdTorch: return BuildTorch();
            case IdLadder: return BuildLadder();
            case IdBackpack: return BuildBackpack();
            case IdStoneHammer: return BuildStoneHammer();
            default: return null;
        }
    }

    /// <summary>Xương Dài</summary>
    public static GameObject BuildBone()
    {
        GameObject custom = TryLoadCustomModel(IdLongBone);
        if (custom != null) return custom;

        GameObject root = new GameObject("Item_LongBone");
        Material bone = MakeMat(new Color(0.95f, 0.91f, 0.79f), 0.30f);
        Material shade = MakeMat(new Color(0.80f, 0.73f, 0.58f), 0.20f);

        Vector2[] prof =
        {
            new Vector2(0f, -0.36f), new Vector2(0.105f, -0.36f), new Vector2(0.088f, -0.30f), new Vector2(0.066f, -0.18f),
            new Vector2(0.056f, 0f), new Vector2(0.066f, 0.18f), new Vector2(0.088f, 0.30f), new Vector2(0.105f, 0.36f), new Vector2(0f, 0.36f)
        };
        Part(root.transform, "Shaft", Lathe("BoneShaft", prof, 18), bone, Vector3.zero, Vector3.zero, Vector3.one);

        for (int sy = -1; sy <= 1; sy += 2)
        {
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Ball(root.transform, "Condyle", bone, new Vector3(sx * 0.078f, sy * 0.40f, 0f), new Vector3(0.25f, 0.23f, 0.25f));
            }
            Ring(root.transform, "NeckRing", shade, new Vector3(0f, sy * 0.325f, 0f), Vector3.zero, 0.108f, 0.016f);
        }
        return root;
    }

    /// <summary>Gai Độc</summary>
    public static GameObject BuildThorn()
    {
        GameObject custom = TryLoadCustomModel(IdPoisonThorn);
        if (custom != null) return custom;

        GameObject root = new GameObject("Item_PoisonThorn");
        Material baseMat = MakeMat(new Color(0.26f, 0.07f, 0.40f), 0.55f, 0.15f);
        Material midMat = MakeMat(new Color(0.46f, 0.14f, 0.66f), 0.50f, 0.25f);
        Material ringMat = MakeMat(new Color(0.66f, 0.30f, 0.90f), 0.45f, 0.35f);
        Material tipMat = MakeMat(new Color(0.25f, 1f, 0.42f), 0.60f, 0.95f);

        Ball(root.transform, "Bulb", baseMat, new Vector3(0f, -0.50f, 0f), new Vector3(0.40f, 0.30f, 0.40f));
        AddThorn(root.transform, "MainThorn", 1.0f, 0.16f, 0.30f, new Vector3(0f, -0.50f, 0f), Vector3.zero, midMat, tipMat);
        AddThorn(root.transform, "SideThornL", 0.36f, 0.06f, 0.20f, new Vector3(-0.10f, -0.44f, 0f), new Vector3(0f, 0f, 42f), midMat, tipMat);
        AddThorn(root.transform, "SideThornR", 0.36f, 0.06f, 0.26f, new Vector3(0.12f, -0.44f, 0f), new Vector3(0f, 0f, -38f), midMat, tipMat);

        float[] ts = { 0.10f, 0.22f, 0.34f };
        for (int i = 0; i < ts.Length; i++)
        {
            float t = ts[i];
            Vector3 p = new Vector3(0.30f * t * t, -0.50f + t, 0f);
            float r = 0.16f * Mathf.Pow(1f - t, 0.9f) * 1.12f;
            float tilt = -Mathf.Atan(2f * 0.30f * t) * Mathf.Rad2Deg;
            Ring(root.transform, "ThornRing", ringMat, p, new Vector3(0f, 0f, tilt), r, 0.020f);
        }
        return root;
    }

    /// <summary>Mạng Nhện</summary>
    public static GameObject BuildSilk()
    {
        GameObject custom = TryLoadCustomModel(IdSpiderSilk);
        if (custom != null) return custom;

        GameObject root = new GameObject("Item_SpiderSilk");
        Material silk = MakeMat(new Color(0.94f, 0.97f, 1f), 0.20f, 0.18f);
        Material thread = MakeMat(new Color(0.80f, 0.90f, 1f), 0.30f, 0.30f);
        Material core = MakeMat(new Color(0.68f, 0.88f, 1f), 0.35f, 0.45f);

        Ball(root.transform, "Cocoon", silk, new Vector3(0f, 0f, 0f), new Vector3(0.44f, 0.62f, 0.44f));
        Ball(root.transform, "CocoonGlow", core, new Vector3(0f, 0.02f, 0f), new Vector3(0.20f, 0.30f, 0.20f));

        float[] tiltX = { 0f, 24f, -24f, 48f, -48f, 70f };
        float[] rotY = { 0f, 30f, 120f, 70f, 160f, 100f };
        for (int i = 0; i < tiltX.Length; i++)
        {
            Ring(root.transform, "Wrap", thread, new Vector3(0f, 0f, 0f), new Vector3(tiltX[i], rotY[i], 0f), 0.232f, 0.012f);
        }

        float webY = -0.29f;
        float[] ringR = { 0.22f, 0.34f, 0.46f };
        for (int i = 0; i < ringR.Length; i++)
        {
            Ring(root.transform, "WebRing", thread, new Vector3(0f, webY, 0f), Vector3.zero, ringR[i], 0.007f);
        }
        for (int k = 0; k < 8; k++)
        {
            Vector3[] path = { new Vector3(0.12f, 0f, 0f), new Vector3(0.30f, 0.004f, 0f), new Vector3(0.50f, 0f, 0f) };
            float[] rad = { 0.009f, 0.008f, 0.006f };
            Part(root.transform, "Spoke", Tube("Spoke", path, rad, 6, Vector3.up), thread, new Vector3(0f, webY, 0f), new Vector3(0f, k * 45f, 0f), Vector3.one);
        }
        return root;
    }

    /// <summary>Giáo Gai Độc</summary>
    public static GameObject BuildSpear()
    {
        GameObject custom = TryLoadCustomModel(IdPoisonSpear);
        if (custom != null)
        {
            AlignWeaponVerticallyAndSetGrip(
                custom,
                new Quaternion[]
                {
                    Quaternion.Euler(0f, 0f, -54.15f),
                    Quaternion.Euler(0f, 0f, 54.15f),
                    Quaternion.Euler(0f, 0f, -35.85f),
                    Quaternion.Euler(0f, 0f, 35.85f),
                    Quaternion.identity
                },
                0.34f
            );
            return custom;
        }

        GameObject root = new GameObject("Item_PoisonThornSpear");
        Material bone = MakeMat(new Color(0.95f, 0.91f, 0.79f), 0.30f);
        Material silk = MakeMat(new Color(0.94f, 0.97f, 1f), 0.20f, 0.15f);
        Material purple = MakeMat(new Color(0.40f, 0.11f, 0.60f), 0.55f, 0.25f);
        Material green = MakeMat(new Color(0.25f, 1f, 0.42f), 0.65f, 0.90f);

        Vector2[] shaft =
        {
            new Vector2(0f, -0.50f), new Vector2(0.040f, -0.50f), new Vector2(0.056f, -0.47f), new Vector2(0.036f, -0.43f),
            new Vector2(0.027f, -0.38f), new Vector2(0.026f, 0f), new Vector2(0.030f, 0.20f), new Vector2(0.046f, 0.27f), new Vector2(0f, 0.27f)
        };
        Part(root.transform, "Shaft", Lathe("SpearShaft", shaft, 16), bone, Vector3.zero, Vector3.zero, Vector3.one);
        Ball(root.transform, "TailKnobL", bone, new Vector3(-0.03f, -0.50f, 0f), new Vector3(0.10f, 0.09f, 0.10f));
        Ball(root.transform, "TailKnobR", bone, new Vector3(0.03f, -0.50f, 0f), new Vector3(0.10f, 0.09f, 0.10f));

        for (int k = 0; k < 5; k++)
        {
            Ring(root.transform, "GripWrap", silk, new Vector3(0f, -0.30f + k * 0.05f, 0f), new Vector3(0f, k * 20f, 0f), 0.031f, 0.010f);
        }
        for (int k = 0; k < 4; k++)
        {
            Ring(root.transform, "NeckWrap", silk, new Vector3(0f, 0.20f + k * 0.02f, 0f), Vector3.zero, 0.042f, 0.011f);
        }
        Ball(root.transform, "SilkKnot", silk, new Vector3(0.03f, 0.235f, 0f), new Vector3(0.07f, 0.08f, 0.07f));

        Vector2[] collar =
        {
            new Vector2(0f, 0.26f), new Vector2(0.050f, 0.26f), new Vector2(0.066f, 0.285f), new Vector2(0.052f, 0.315f), new Vector2(0f, 0.315f)
        };
        Part(root.transform, "Collar", Lathe("SpearCollar", collar, 16), purple, Vector3.zero, Vector3.zero, Vector3.one);

        Vector2[] blade =
        {
            new Vector2(0f, 0.30f), new Vector2(0.035f, 0.31f), new Vector2(0.074f, 0.35f), new Vector2(0.086f, 0.40f),
            new Vector2(0.070f, 0.45f), new Vector2(0.040f, 0.48f), new Vector2(0f, 0.50f)
        };
        Part(root.transform, "Blade", Lathe("SpearBlade", blade, 18), green, Vector3.zero, Vector3.zero, new Vector3(1f, 1f, 0.36f));

        AddThorn(root.transform, "BarbL", 0.13f, 0.022f, 0.15f, new Vector3(-0.05f, 0.28f, 0f), new Vector3(0f, 0f, 55f), purple, green);
        AddThorn(root.transform, "BarbR", 0.13f, 0.022f, 0.15f, new Vector3(0.05f, 0.28f, 0f), new Vector3(0f, 0f, -55f), purple, green);
        Ball(root.transform, "VenomDrop", green, new Vector3(0.07f, 0.285f, 0.012f), new Vector3(0.026f, 0.036f, 0.026f));
        return root;
    }

    /// <summary>Khúc Gỗ (wood): thanh gỗ tròn có vân vỏ cây nâu ấm, đầu gỗ sáng màu và mắt gỗ nhỏ</summary>
    public static GameObject BuildWood()
    {
        GameObject custom = TryLoadCustomModel(IdWood);
        if (custom != null) return custom;

        GameObject root = new GameObject("Item_Wood");
        Material bark = MakeMat(new Color(0.44f, 0.28f, 0.16f), 0.25f);
        Material cutWood = MakeMat(new Color(0.82f, 0.68f, 0.45f), 0.20f);
        Material knot = MakeMat(new Color(0.32f, 0.19f, 0.10f), 0.30f);

        Vector2[] logProf =
        {
            new Vector2(0f, -0.36f), new Vector2(0.125f, -0.36f), new Vector2(0.132f, -0.32f),
            new Vector2(0.128f, 0f), new Vector2(0.134f, 0.32f), new Vector2(0.125f, 0.36f), new Vector2(0f, 0.36f)
        };
        Part(root.transform, "LogBody", Lathe("LogBody", logProf, 16), bark, Vector3.zero, Vector3.zero, Vector3.one);

        // Mặt cắt gỗ ở 2 đầu
        Ball(root.transform, "EndCapTop", cutWood, new Vector3(0f, 0.355f, 0f), new Vector3(0.24f, 0.02f, 0.24f));
        Ball(root.transform, "EndCapBottom", cutWood, new Vector3(0f, -0.355f, 0f), new Vector3(0.24f, 0.02f, 0.24f));

        // Mắt gỗ và gờ vỏ cây
        Ball(root.transform, "Knot1", knot, new Vector3(0.125f, 0.06f, 0f), new Vector3(0.06f, 0.08f, 0.06f));
        Ring(root.transform, "BarkRidge", bark, new Vector3(0f, -0.12f, 0f), Vector3.zero, 0.136f, 0.012f);
        return root;
    }

    /// <summary>Cây Gậy (stick): cành cây thon dài, hơi cong tự nhiên có vân vỏ</summary>
    public static GameObject BuildStick()
    {
        GameObject custom = TryLoadCustomModel(IdStick);
        if (custom != null) return custom;

        GameObject root = new GameObject("Item_Stick");
        Material wood = MakeMat(new Color(0.66f, 0.48f, 0.28f), 0.30f);
        Material knot = MakeMat(new Color(0.46f, 0.30f, 0.16f), 0.35f);

        Vector2[] prof =
        {
            new Vector2(0f, -0.44f), new Vector2(0.038f, -0.44f), new Vector2(0.032f, -0.20f),
            new Vector2(0.030f, 0.10f), new Vector2(0.024f, 0.42f), new Vector2(0f, 0.44f)
        };
        Part(root.transform, "Stem", Lathe("StickStem", prof, 14), wood, Vector3.zero, Vector3.zero, Vector3.one);

        // Mắt cành nhỏ nhô ra
        Ball(root.transform, "KnotA", knot, new Vector3(0.028f, 0.15f, 0f), new Vector3(0.035f, 0.05f, 0.035f));
        Ball(root.transform, "KnotB", knot, new Vector3(-0.026f, -0.18f, 0.01f), new Vector3(0.038f, 0.06f, 0.035f));
        return root;
    }

    /// <summary>Dây Thừng (rope): bó dây thừng cuộn tròn nhiều vòng bện xoắn</summary>
    public static GameObject BuildRope()
    {
        GameObject custom = TryLoadCustomModel(IdRope);
        if (custom != null) return custom;

        GameObject root = new GameObject("Item_Rope");
        Material hemp = MakeMat(new Color(0.78f, 0.65f, 0.38f), 0.25f);
        Material tie = MakeMat(new Color(0.62f, 0.48f, 0.25f), 0.30f);

        for (int k = -1; k <= 1; k++)
        {
            Ring(root.transform, "Coil_" + k, hemp, new Vector3(0f, k * 0.065f, 0f), new Vector3(k * 4f, k * 25f, 0f), 0.28f, 0.055f);
        }

        // Dây buộc chéo quanh bó
        Ring(root.transform, "CrossTie1", tie, Vector3.zero, new Vector3(90f, 0f, 0f), 0.15f, 0.025f);
        Ring(root.transform, "CrossTie2", tie, Vector3.zero, new Vector3(90f, 60f, 0f), 0.15f, 0.025f);
        Ball(root.transform, "TieKnot", tie, new Vector3(0.28f, 0.02f, 0f), new Vector3(0.09f, 0.08f, 0.09f));
        return root;
    }

    /// <summary>Cuộn Vải (cloth): súc vải lanh cuộn tròn viền gập gọn gàng kèm dải nơ thắt</summary>
    public static GameObject BuildCloth()
    {
        GameObject custom = TryLoadCustomModel(IdCloth);
        if (custom != null) return custom;

        GameObject root = new GameObject("Item_Cloth");
        Material linen = MakeMat(new Color(0.92f, 0.90f, 0.85f), 0.25f);
        Material rib = MakeMat(new Color(0.85f, 0.28f, 0.28f), 0.35f);

        Vector2[] roll =
        {
            new Vector2(0f, -0.32f), new Vector2(0.16f, -0.32f), new Vector2(0.17f, -0.28f),
            new Vector2(0.165f, 0f), new Vector2(0.17f, 0.28f), new Vector2(0.16f, 0.32f), new Vector2(0f, 0.32f)
        };
        Part(root.transform, "FabricRoll", Lathe("FabricRoll", roll, 16), linen, Vector3.zero, Vector3.zero, Vector3.one);

        // Dải ruy băng đỏ thắt giữ cuộn vải
        Ring(root.transform, "RibbonA", rib, new Vector3(0f, -0.12f, 0f), Vector3.zero, 0.175f, 0.020f);
        Ring(root.transform, "RibbonB", rib, new Vector3(0f, 0.12f, 0f), Vector3.zero, 0.175f, 0.020f);
        Ball(root.transform, "RibbonBow", rib, new Vector3(0.18f, 0.12f, 0f), new Vector3(0.06f, 0.08f, 0.06f));
        return root;
    }

    /// <summary>Khối Đá (stone): khối đá mài gồ ghề góc cạnh sắc nét</summary>
    public static GameObject BuildStone()
    {
        GameObject custom = TryLoadCustomModel(IdStone);
        if (custom != null) return custom;

        GameObject root = new GameObject("Item_Stone");
        Material rock = MakeMat(new Color(0.55f, 0.58f, 0.62f), 0.40f);
        Material darkRock = MakeMat(new Color(0.38f, 0.40f, 0.44f), 0.35f);

        // Dựng khối đá từ các đa giác Lathe không đều
        Vector2[] stoneProf =
        {
            new Vector2(0f, -0.28f), new Vector2(0.14f, -0.25f), new Vector2(0.24f, -0.15f),
            new Vector2(0.26f, 0.05f), new Vector2(0.21f, 0.20f), new Vector2(0.11f, 0.28f), new Vector2(0f, 0.28f)
        };
        Part(root.transform, "MainRock", Lathe("MainRock", stoneProf, 7), rock, Vector3.zero, Vector3.zero, new Vector3(1.1f, 0.95f, 0.85f));
        Box(root.transform, "Chip1", darkRock, new Vector3(0.12f, -0.06f, 0.08f), new Vector3(25f, 40f, 15f), new Vector3(0.18f, 0.14f, 0.16f));
        Box(root.transform, "Chip2", rock, new Vector3(-0.08f, 0.12f, -0.06f), new Vector3(-20f, 30f, -10f), new Vector3(0.15f, 0.12f, 0.14f));
        return root;
    }

    /// <summary>Khối Than (coal): mẩu than đen ánh đốm than đỏ phát sáng</summary>
    public static GameObject BuildCoal()
    {
        GameObject custom = TryLoadCustomModel(IdCoal);
        if (custom != null) return custom;

        GameObject root = new GameObject("Item_Coal");
        Material coal = MakeMat(new Color(0.12f, 0.12f, 0.14f), 0.50f);
        Material ember = MakeMat(new Color(1f, 0.38f, 0.08f), 0.70f, 1.4f);

        Vector2[] coalProf =
        {
            new Vector2(0f, -0.26f), new Vector2(0.13f, -0.22f), new Vector2(0.23f, -0.08f),
            new Vector2(0.24f, 0.10f), new Vector2(0.14f, 0.24f), new Vector2(0f, 0.26f)
        };
        Part(root.transform, "CoalChunk", Lathe("CoalChunk", coalProf, 8), coal, Vector3.zero, Vector3.zero, new Vector3(1.1f, 0.9f, 0.8f));
        Box(root.transform, "FacetA", coal, new Vector3(0.08f, 0.06f, 0.08f), new Vector3(35f, 20f, 45f), new Vector3(0.17f, 0.16f, 0.15f));
        Box(root.transform, "FacetB", coal, new Vector3(-0.09f, -0.07f, -0.04f), new Vector3(-25f, 40f, -30f), new Vector3(0.16f, 0.15f, 0.14f));

        // Đốm than hồng phát sáng âm ỉ bên trong
        Ball(root.transform, "EmberCore", ember, new Vector3(0.04f, 0.02f, 0.03f), new Vector3(0.08f, 0.08f, 0.08f));
        return root;
    }

    /// <summary>Đuốc Phát Sáng (torch): cán gỗ, đầu quấn tơ/vải than, ngọn lửa vàng cam phát sáng rực rỡ</summary>
    public static GameObject BuildTorch()
    {
        GameObject custom = TryLoadCustomModel(IdTorch);
        if (custom != null)
        {
            AlignWeaponVerticallyAndSetGrip(
                custom,
                new Quaternion[]
                {
                    Quaternion.Euler(-90f, 0f, 0f),
                    Quaternion.Euler(90f, 0f, 0f),
                    Quaternion.identity
                },
                0.25f
            );
            return custom;
        }

        GameObject root = new GameObject("Item_Torch");
        Material wood = MakeMat(new Color(0.55f, 0.38f, 0.22f), 0.30f);
        Material headWrap = MakeMat(new Color(0.20f, 0.18f, 0.16f), 0.40f);
        Material flameCore = MakeMat(new Color(1f, 0.82f, 0.22f), 0.80f, 1.8f);
        Material flameOuter = MakeMat(new Color(1f, 0.42f, 0.08f), 0.70f, 1.4f);

        // Cán đuốc
        Box(root.transform, "TorchHaft", wood, new Vector3(0f, -0.15f, 0f), Vector3.zero, new Vector3(0.075f, 0.65f, 0.075f));

        // Đầu đuốc quấn vải than
        Box(root.transform, "TorchHead", headWrap, new Vector3(0f, 0.20f, 0f), Vector3.zero, new Vector3(0.13f, 0.18f, 0.13f));
        Ring(root.transform, "HeadTie", wood, new Vector3(0f, 0.12f, 0f), Vector3.zero, 0.075f, 0.015f);

        // Ngọn lửa 3D phát sáng
        Vector2[] flameProf =
        {
            new Vector2(0f, 0.28f), new Vector2(0.085f, 0.32f), new Vector2(0.10f, 0.38f),
            new Vector2(0.06f, 0.46f), new Vector2(0.02f, 0.52f), new Vector2(0f, 0.55f)
        };
        Part(root.transform, "FlameOuter", Lathe("FlameOuter", flameProf, 12), flameOuter, Vector3.zero, Vector3.zero, Vector3.one);
        Ball(root.transform, "FlameCore", flameCore, new Vector3(0f, 0.36f, 0f), new Vector3(0.10f, 0.15f, 0.10f));
        return root;
    }

    /// <summary>Thang Leo (ladder): 2 thanh dọc bằng gỗ + 4 bậc thang ngang có dây thừng gia cố</summary>
    public static GameObject BuildLadder()
    {
        GameObject custom = TryLoadCustomModel(IdLadder);
        if (custom != null) return custom;

        GameObject root = new GameObject("Item_Ladder");
        Material wood = MakeMat(new Color(0.62f, 0.44f, 0.26f), 0.30f);
        Material rope = MakeMat(new Color(0.78f, 0.65f, 0.38f), 0.25f);

        float ladderW = 0.32f;
        float ladderH = 0.90f;
        float halfW = ladderW * 0.5f;

        // 2 thanh dọc 2 bên
        Box(root.transform, "RailL", wood, new Vector3(-halfW, 0f, 0f), Vector3.zero, new Vector3(0.055f, ladderH, 0.055f));
        Box(root.transform, "RailR", wood, new Vector3(halfW, 0f, 0f), Vector3.zero, new Vector3(0.055f, ladderH, 0.055f));

        // 4 bậc ngang
        float[] rungsY = { -0.32f, -0.11f, 0.11f, 0.32f };
        for (int i = 0; i < rungsY.Length; i++)
        {
            float ry = rungsY[i];
            Box(root.transform, "Rung_" + i, wood, new Vector3(0f, ry, 0f), Vector3.zero, new Vector3(ladderW, 0.045f, 0.045f));
            Ring(root.transform, "TieL_" + i, rope, new Vector3(-halfW, ry, 0f), Vector3.zero, 0.038f, 0.010f);
            Ring(root.transform, "TieR_" + i, rope, new Vector3(halfW, ry, 0f), Vector3.zero, 0.038f, 0.010f);
        }
        return root;
    }

    /// <summary>Balo 24 Ô (backpack): túi da thám hiểm có nắp gập, khóa đồng, ngăn phụ và quai đeo</summary>
    public static GameObject BuildBackpack()
    {
        GameObject custom = TryLoadCustomModel(IdBackpack);
        if (custom != null) return custom;

        GameObject root = new GameObject("Item_Backpack");
        Material leather = MakeMat(new Color(0.42f, 0.25f, 0.14f), 0.30f);
        Material darkLeather = MakeMat(new Color(0.28f, 0.16f, 0.09f), 0.35f);
        Material brass = MakeMat(new Color(0.92f, 0.78f, 0.28f), 0.65f);
        Material bedroll = MakeMat(new Color(0.35f, 0.48f, 0.36f), 0.25f);

        // Thân chính balo
        Box(root.transform, "MainPack", leather, new Vector3(0f, 0f, 0f), Vector3.zero, new Vector3(0.38f, 0.44f, 0.24f));

        // Nắp trên gập xuống
        Box(root.transform, "TopFlap", darkLeather, new Vector3(0f, 0.17f, 0.04f), new Vector3(18f, 0f, 0f), new Vector3(0.40f, 0.16f, 0.26f));

        // Ngăn phụ phía trước
        Box(root.transform, "FrontPouch", darkLeather, new Vector3(0f, -0.06f, 0.14f), Vector3.zero, new Vector3(0.26f, 0.22f, 0.10f));

        // Khóa đồng vàng sáng
        Box(root.transform, "BuckleL", brass, new Vector3(-0.09f, 0.06f, 0.145f), Vector3.zero, new Vector3(0.04f, 0.06f, 0.02f));
        Box(root.transform, "BuckleR", brass, new Vector3(0.09f, 0.06f, 0.145f), Vector3.zero, new Vector3(0.04f, 0.06f, 0.02f));

        // Cuộn chăn ngủ phía dưới
        Vector2[] rollProf =
        {
            new Vector2(0f, -0.22f), new Vector2(0.08f, -0.22f), new Vector2(0.08f, 0.22f), new Vector2(0f, 0.22f)
        };
        Part(root.transform, "BedRoll", Lathe("BedRoll", rollProf, 14), bedroll, new Vector3(0f, -0.26f, 0f), new Vector3(0f, 0f, 90f), Vector3.one);

        // 2 quai đeo sau lưng
        Box(root.transform, "StrapL", darkLeather, new Vector3(-0.11f, 0.02f, -0.13f), new Vector3(0f, 0f, 5f), new Vector3(0.05f, 0.38f, 0.02f));
        Box(root.transform, "StrapR", darkLeather, new Vector3(0.11f, 0.02f, -0.13f), new Vector3(0f, 0f, -5f), new Vector3(0.05f, 0.38f, 0.02f));
        return root;
    }

    /// <summary>Búa Đá Chiến (stone_hammer): cán gậy chắc chắn, đầu búa đá mài góc cạnh buộc dây thừng</summary>
    public static GameObject BuildStoneHammer()
    {
        GameObject custom = TryLoadCustomModel(IdStoneHammer);
        if (custom != null) return custom;

        GameObject root = new GameObject("Item_StoneHammer");
        Material wood = MakeMat(new Color(0.60f, 0.42f, 0.24f), 0.30f);
        Material stone = MakeMat(new Color(0.52f, 0.55f, 0.60f), 0.45f);
        Material rope = MakeMat(new Color(0.78f, 0.65f, 0.38f), 0.25f);

        // Cán búa
        Vector2[] shaft =
        {
            new Vector2(0f, -0.48f), new Vector2(0.040f, -0.48f), new Vector2(0.032f, -0.20f),
            new Vector2(0.034f, 0.15f), new Vector2(0.036f, 0.35f), new Vector2(0f, 0.35f)
        };
        Part(root.transform, "HammerShaft", Lathe("HammerShaft", shaft, 14), wood, Vector3.zero, Vector3.zero, Vector3.one);
        Ball(root.transform, "ShaftPommel", wood, new Vector3(0f, -0.48f, 0f), new Vector3(0.10f, 0.08f, 0.10f));

        // Đầu búa đá
        Box(root.transform, "HammerHead", stone, new Vector3(0f, 0.24f, 0f), new Vector3(0f, 0f, 10f), new Vector3(0.40f, 0.18f, 0.20f));

        // Dây buộc chéo gia cố đầu búa
        Ring(root.transform, "HeadRope1", rope, new Vector3(0f, 0.24f, 0f), new Vector3(0f, 45f, 45f), 0.14f, 0.020f);
        Ring(root.transform, "HeadRope2", rope, new Vector3(0f, 0.24f, 0f), new Vector3(0f, -45f, -45f), 0.14f, 0.020f);
        return root;
    }

    /// <summary>Góc nằm nghỉ (trên mặt đất) và kích cỡ thực tế trong phòng cho từng vật phẩm rơi ra</summary>
    public static void GetWorldPose(string itemId, out Vector3 restEuler, out float worldSize)
    {
        switch (itemId)
        {
            case IdLongBone: restEuler = new Vector3(0f, 0f, 78f); worldSize = 0.050f; break;
            case IdPoisonThorn: restEuler = new Vector3(0f, 0f, 62f); worldSize = 0.042f; break;
            case IdSpiderSilk: restEuler = new Vector3(0f, 0f, 0f); worldSize = 0.046f; break;
            case IdPoisonSpear: restEuler = new Vector3(0f, 0f, 70f); worldSize = 0.060f; break;
            case IdWood: restEuler = new Vector3(0f, 0f, 85f); worldSize = 0.050f; break;
            case IdStick: restEuler = new Vector3(0f, 0f, 75f); worldSize = 0.048f; break;
            case IdRope: restEuler = new Vector3(0f, 0f, 0f); worldSize = 0.044f; break;
            case IdCloth: restEuler = new Vector3(0f, 0f, 80f); worldSize = 0.046f; break;
            case IdStone: restEuler = new Vector3(0f, 0f, 15f); worldSize = 0.045f; break;
            case IdCoal: restEuler = new Vector3(0f, 0f, 20f); worldSize = 0.042f; break;
            case IdTorch: restEuler = new Vector3(0f, 0f, 70f); worldSize = 0.052f; break;
            case IdLadder: restEuler = new Vector3(0f, 0f, 0f); worldSize = 0.065f; break;
            case IdBackpack: restEuler = new Vector3(0f, 0f, 0f); worldSize = 0.055f; break;
            case IdStoneHammer: restEuler = new Vector3(0f, 0f, 65f); worldSize = 0.056f; break;
            default: restEuler = new Vector3(0f, 0f, 70f); worldSize = 0.050f; break;
        }
    }

    // =========================================================================
    // RENDER ICON 2D TỪ MÔ HÌNH 3D
    // =========================================================================
    public static Texture2D GetIcon(string itemId)
    {
        Texture2D tex;
        return iconCache.TryGetValue(itemId, out tex) && tex != null ? tex : null;
    }

    public static Texture2D GetFallbackIcon(string itemId)
    {
        Texture2D tex = GetIcon(itemId);
        if (tex != null) return tex;

        Color c = new Color(0.9f, 0.9f, 0.9f);
        switch (itemId)
        {
            case IdPoisonThorn: c = new Color(0.45f, 0.95f, 0.45f); break;
            case IdSpiderSilk: c = new Color(0.85f, 0.93f, 1f); break;
            case IdLongBone: c = new Color(0.95f, 0.9f, 0.78f); break;
            case IdPoisonSpear: c = new Color(0.4f, 1f, 0.6f); break;
            case IdWood: c = new Color(0.72f, 0.48f, 0.28f); break;
            case IdStick: c = new Color(0.82f, 0.62f, 0.38f); break;
            case IdRope: c = new Color(0.88f, 0.74f, 0.44f); break;
            case IdCloth: c = new Color(0.95f, 0.92f, 0.88f); break;
            case IdStone: c = new Color(0.65f, 0.68f, 0.72f); break;
            case IdCoal: c = new Color(0.25f, 0.25f, 0.28f); break;
            case IdTorch: c = new Color(1f, 0.70f, 0.20f); break;
            case IdLadder: c = new Color(0.75f, 0.55f, 0.32f); break;
            case IdBackpack: c = new Color(0.55f, 0.35f, 0.20f); break;
            case IdStoneHammer: c = new Color(0.60f, 0.65f, 0.70f); break;
        }

        int size = 64;
        tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(size * 0.5f, size * 0.5f));
                tex.SetPixel(x, y, d < 24f ? c : new Color(0f, 0f, 0f, 0f));
            }
        }
        tex.Apply();
        iconCache[itemId] = tex;
        return tex;
    }

    public static void BakeAllIcons()
    {
        if (iconsBaked) return;
        iconsBaked = true;
        for (int i = 0; i < AllModelIds.Length; i++)
        {
            try
            {
                BakeIcon(AllModelIds[i]);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("Không render được icon 3D cho " + AllModelIds[i] + ": " + ex.Message);
            }
        }
    }

    private static void SafeDestroy(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
    }

    private static void BakeIcon(string itemId)
    {
        GameObject model = Build(itemId);
        if (model == null) return;

        Vector3 origin = new Vector3(5000f, 5000f, 5000f);
        model.transform.position = origin;

        switch (itemId)
        {
            case IdSpiderSilk: model.transform.rotation = Quaternion.Euler(-38f, 25f, 0f); break;
            case IdPoisonThorn: model.transform.rotation = Quaternion.Euler(0f, 0f, -28f); model.transform.position = origin + new Vector3(0.05f, 0f, 0f); break;
            case IdLongBone: model.transform.rotation = Quaternion.Euler(8f, 0f, -42f); break;
            case IdWood: model.transform.rotation = Quaternion.Euler(20f, -35f, -25f); break;
            case IdStick: model.transform.rotation = Quaternion.Euler(0f, 0f, -40f); break;
            case IdRope: model.transform.rotation = Quaternion.Euler(30f, 25f, 0f); break;
            case IdCloth: model.transform.rotation = Quaternion.Euler(15f, -30f, -35f); break;
            case IdStone: model.transform.rotation = Quaternion.Euler(25f, 40f, 0f); break;
            case IdCoal: model.transform.rotation = Quaternion.Euler(22f, 35f, 10f); break;
            case IdTorch: model.transform.rotation = Quaternion.Euler(0f, 0f, -35f); break;
            case IdLadder: model.transform.rotation = Quaternion.Euler(15f, -25f, 0f); break;
            case IdBackpack: model.transform.rotation = Quaternion.Euler(15f, -30f, 0f); break;
            case IdStoneHammer: model.transform.rotation = Quaternion.Euler(0f, 0f, -45f); break;
            default: model.transform.rotation = Quaternion.Euler(0f, 0f, -42f); break;
        }

        GameObject camGo = new GameObject("IconBakeCamera");
        Camera cam = camGo.AddComponent<Camera>();
        cam.enabled = false;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.orthographic = true;
        cam.orthographicSize = 0.62f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 10f;
        cam.transform.position = origin + new Vector3(0f, 0f, -3f);
        cam.transform.LookAt(origin);

        GameObject lightGo = new GameObject("IconBakeLight");
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        light.shadows = LightShadows.None;
        lightGo.transform.rotation = Quaternion.Euler(35f, -25f, 0f);

        int size = 256;
        RenderTexture rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        cam.targetTexture = rt;

        Texture2D result = new Texture2D(size, size, TextureFormat.RGBA32, false);
        result.filterMode = FilterMode.Bilinear;
        result.wrapMode = TextureWrapMode.Clamp;

        RenderTexture prev = RenderTexture.active;
        cam.Render();
        RenderTexture.active = rt;
        result.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        result.Apply();
        RenderTexture.active = prev;

        cam.targetTexture = null;
        rt.Release();
        SafeDestroy(rt);
        SafeDestroy(camGo);
        SafeDestroy(lightGo);
        SafeDestroy(model);

        Color[] px = result.GetPixels();
        int visible = 0;
        for (int i = 0; i < px.Length; i += 7)
        {
            if (px[i].a > 0.1f) visible++;
        }

        if (visible < 40)
        {
            SafeDestroy(result);
            return;
        }

        iconCache[itemId] = result;
    }
}
