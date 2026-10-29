using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Lưu các Asset 3D vật phẩm mới thành Prefab + Mesh + Material thật
/// trong thư mục Assets/Prefabs/Items để người dùng có thể xem / chỉnh sửa trực tiếp trong Unity.
/// Menu: Tools / Tao Asset Vat Pham Moi (tự chạy 1 lần mỗi phiên làm việc sau khi compile).
/// </summary>
public static class ItemAssetBuilder
{
    private const string Folder = "Assets/Prefabs/Items";

    [InitializeOnLoadMethod]
    private static void AutoBake()
    {
        EditorApplication.delayCall += () =>
        {
            if (Application.isPlaying) return;
            if (SessionState.GetBool("ItemAssetsBaked_v5", false)) return;
            SessionState.SetBool("ItemAssetsBaked_v5", true);
            BuildAll();
        };
    }

    [MenuItem("Tools/Tao Asset Vat Pham Moi")]
    public static void BuildAll()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Items");

        string[] ids = ItemModelFactory.AllModelIds;

        for (int k = 0; k < ids.Length; k++)
        {
            string itemId = ids[k];
            string prefabName = "Item_" + itemId;

            GameObject root = ItemModelFactory.Build(itemId);
            if (root == null) continue;
            root.name = prefabName;

            Dictionary<Material, Material> matMap = new Dictionary<Material, Material>();
            int meshIndex = 0;
            int matIndex = 0;

            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                // Nếu mesh đã nằm sẵn trong file .glb/.fbx trên ổ cứng thì giữ nguyên, không tạo file .asset trùng
                if (AssetDatabase.Contains(mf.sharedMesh)) continue;

                Mesh copy = Object.Instantiate(mf.sharedMesh);
                copy.name = prefabName + "_Mesh" + meshIndex;
                string meshPath = Folder + "/" + copy.name + ".asset";
                AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(copy, meshPath);
                mf.sharedMesh = copy;
                meshIndex++;
            }

            foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material src = mr.sharedMaterial;
                if (src == null) continue;
                // Nếu material đã là sub-asset nằm sẵn trong file .glb/.fbx thì giữ nguyên, tránh lỗi UnityException
                if (AssetDatabase.Contains(src)) continue;

                Material saved;
                if (!matMap.TryGetValue(src, out saved))
                {
                    string matPath = Folder + "/" + prefabName + "_Mat" + matIndex + ".mat";
                    AssetDatabase.DeleteAsset(matPath);
                    AssetDatabase.CreateAsset(src, matPath);
                    matMap[src] = src;
                    saved = src;
                    matIndex++;
                }
                mr.sharedMaterial = saved;
            }

            string prefabPath = Folder + "/" + prefabName + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Đã lưu toàn bộ Asset 3D vật phẩm mới vào " + Folder);
    }
}
