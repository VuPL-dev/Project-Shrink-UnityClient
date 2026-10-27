using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class SetupEnemyAndSyringe
{
    [InitializeOnLoadMethod]
    private static void AutoRunOnceAfterCompile()
    {
        EditorApplication.delayCall += () =>
        {
            if (Application.isPlaying) return;

            GameObject existingBeetle = GameObject.Find("Enemy_Beetle");
            bool needsMaterialFix = false;
            if (existingBeetle != null)
            {
                foreach (Renderer r in existingBeetle.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.sharedMaterial == null || r.sharedMaterial.shader == null || r.sharedMaterial.shader.name.Contains("Error"))
                    {
                        needsMaterialFix = true;
                        break;
                    }
                }
            }

            // Nếu trong Scene chưa có Enemy_Beetle hoặc quái đang bị thiếu Material (màu tím) thì tự động chạy Setup luôn!
            if ((existingBeetle == null || needsMaterialFix || !SessionState.GetBool("BeetleMaterialFixed_v2", false)) && GameObject.Find("player_v2") != null)
            {
                SessionState.SetBool("BeetleMaterialFixed_v2", true);
                SetupAll();
            }
        };
    }

    [MenuItem("Tools/Tao Ong Tiem va Quai Vat")]
    public static void SetupAll()
    {
        // Xóa bản cũ trên Scene (nếu bấm chạy lại lần 2) để tránh trùng lặp
        GameObject oldSyringe = GameObject.Find("HealthSyringe");
        if (oldSyringe != null) Object.DestroyImmediate(oldSyringe);
        GameObject oldBeetle = GameObject.Find("Enemy_Beetle");
        if (oldBeetle != null) Object.DestroyImmediate(oldBeetle);

        // 1. Tạo thư mục Prefabs nếu chưa có
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        // 2. TẠO PREFAB ỐNG TIÊM HỒI MÁU (HealthSyringe.prefab)
        GameObject syringeModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/health_syringe.obj");
        if (syringeModel == null)
        {
            Debug.LogError("Không tìm thấy model Assets/health_syringe.obj!");
            return;
        }

        GameObject syringeInstance = (GameObject)PrefabUtility.InstantiatePrefab(syringeModel);
        syringeInstance.name = "HealthSyringe";

        // Tinh chỉnh scale phù hợp kích thước mini của phòng và người
        syringeInstance.transform.localScale = new Vector3(0.04f, 0.04f, 0.04f);
        // Đặt xoay nằm nghiêng 45 độ cho đẹp mắt
        syringeInstance.transform.rotation = Quaternion.Euler(45f, 0f, 0f);

        // Thêm Collider dạng Trigger để nhặt được
        SphereCollider sc = syringeInstance.GetComponent<SphereCollider>();
        if (sc == null) sc = syringeInstance.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 1.5f; // Bán kính nhận diện nhặt đồ vừa tầm tay

        // Gắn Script nhặt đồ ItemPickup
        ItemPickup pickup = syringeInstance.GetComponent<ItemPickup>();
        if (pickup == null) pickup = syringeInstance.AddComponent<ItemPickup>();
        pickup.itemId = "health_syringe";
        pickup.itemName = "Health Syringe";
        pickup.quantity = 1;
        // pickup.enableFloating = true;
        pickup.hoverHeight = 0.012f;
        pickup.rotationSpeed = 65f;

        // Lưu thành Prefab tái sử dụng
        string syringePrefabPath = "Assets/Prefabs/HealthSyringe.prefab";
        GameObject syringePrefab = PrefabUtility.SaveAsPrefabAsset(syringeInstance, syringePrefabPath);
        Debug.Log("✅ Đã tạo thành công Prefab Ống Tiêm: " + syringePrefabPath);

        // Đặt 1 ống tiêm mẫu ngay trong phòng để người chơi test nhặt
        syringeInstance.transform.position = new Vector3(1.65f, 0.06f, 0.2f);
        Undo.RegisterCreatedObjectUndo(syringeInstance, "Place HealthSyringe");

        // 3. TẠO PREFAB QUÁI VẬT CÔN TRÙNG (Enemy_Beetle.prefab) & SỬA LỖI MÀU TÍM (MISSING MATERIAL)
        string beetleMatPath = "Assets/enemy côn trùng/enemy côn trùng/Nightmare Beetle/Nightmare Beetle/materials/body material.mat";
        Material beetleMat = AssetDatabase.LoadAssetAtPath<Material>(beetleMatPath);
        if (beetleMat != null)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");
            if (urpLit != null) beetleMat.shader = urpLit;

            string texFolder = "Assets/enemy côn trùng/enemy côn trùng/Nightmare Beetle/Nightmare Beetle/textures/";
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "Bug 1_Albedo.tga");
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "Bug 1_Normal.tga");
            Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "Bug 1_Metallic.tga");
            Texture2D occlusion = AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "Bug 1_Occlusion.tga");
            Texture2D emission = AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "Bug 1_Emission.tga");

            if (albedo != null)
            {
                beetleMat.SetTexture("_BaseMap", albedo);
                beetleMat.SetTexture("_MainTex", albedo);
                beetleMat.SetColor("_BaseColor", Color.white);
                beetleMat.SetColor("_Color", Color.white);
            }
            if (normal != null) beetleMat.SetTexture("_BumpMap", normal);
            if (metallic != null) beetleMat.SetTexture("_MetallicGlossMap", metallic);
            if (occlusion != null) beetleMat.SetTexture("_OcclusionMap", occlusion);
            if (emission != null)
            {
                beetleMat.EnableKeyword("_EMISSION");
                beetleMat.SetTexture("_EmissionMap", emission);
                beetleMat.SetColor("_EmissionColor", Color.white);
            }
            EditorUtility.SetDirty(beetleMat);
        }

        string beetleSourcePath = "Assets/enemy côn trùng/enemy côn trùng/Nightmare Beetle/Nightmare Beetle/prefab/Nightmare Beetle.prefab";
        GameObject beetleSource = AssetDatabase.LoadAssetAtPath<GameObject>(beetleSourcePath);
        if (beetleSource != null)
        {
            GameObject beetleInstance = (GameObject)PrefabUtility.InstantiatePrefab(beetleSource);
            beetleInstance.name = "Enemy_Beetle";

            // Gán lại đầy đủ Material cho toàn bộ các bộ phận (vỏ, đầu, chân, càng) để hết màu tím!
            if (beetleMat != null)
            {
                foreach (Renderer r in beetleInstance.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterial = beetleMat;
                }
            }

            // Scale quái vật côn trùng phù hợp kích cỡ phòng
            beetleInstance.transform.localScale = new Vector3(0.038f, 0.038f, 0.038f);
            beetleInstance.transform.position = new Vector3(1.65f, 0.05f, 1.35f);
            beetleInstance.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            // Thêm CapsuleCollider để nhận diện đòn đánh
            CapsuleCollider cc = beetleInstance.GetComponent<CapsuleCollider>();
            if (cc == null) cc = beetleInstance.AddComponent<CapsuleCollider>();
            cc.center = new Vector3(0f, 2.5f, 0f);
            cc.radius = 3.5f;
            cc.height = 8f;

            // Gắn AI cho quái vật
            EnemyAI ai = beetleInstance.GetComponent<EnemyAI>();
            if (ai == null) ai = beetleInstance.AddComponent<EnemyAI>();
            ai.maxHealth = 40f;
            ai.moveSpeed = 0.35f;
            ai.detectionRange = 1.6f;
            ai.attackRange = 0.22f;
            ai.attackDamage = 12f;
            ai.dropItemPrefab = syringePrefab; // KHI QUÁI CHẾT SẼ RƠI ỐNG TIÊM NÀY!

            // Gắn Animator nếu chưa có
            Animator anim = beetleInstance.GetComponent<Animator>();
            if (anim != null) ai.animator = anim;

            string beetlePrefabPath = "Assets/Prefabs/Enemy_Beetle.prefab";
            PrefabUtility.SaveAsPrefabAsset(beetleInstance, beetlePrefabPath);
            Debug.Log("✅ Đã tạo thành công Prefab Quái Vật (đã gán đầy đủ Texture & Material URP): " + beetlePrefabPath);
            Undo.RegisterCreatedObjectUndo(beetleInstance, "Place Enemy_Beetle");
        }

        // 4. Gắn Tag "Player" và thêm InventoryNetworkManager cho player_v2
        GameObject playerObj = GameObject.Find("player_v2");
        if (playerObj != null)
        {
            playerObj.tag = "Player";
            InventoryNetworkManager inv = playerObj.GetComponent<InventoryNetworkManager>();
            if (inv == null)
            {
                inv = playerObj.AddComponent<InventoryNetworkManager>();
            }
            Debug.Log("✅ Đã đặt Tag 'Player' và gắn InventoryNetworkManager cho player_v2");
        }

        // 5. Lưu Scene
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        EditorUtility.DisplayDialog(
            "Thiết Lập Hoàn Tất!",
            "Đã cài đặt thành công:\n\n" +
            "1. Ống tiêm hồi máu (HealthSyringe) đã đặt trong phòng.\n" +
            "2. Quái vật Côn Trùng (Nightmare Beetle) đã được đặt ở phía cuối phòng.\n" +
            "3. Khi bạn đánh bại quái vật (phím J, K, L), quái sẽ rơi ống tiêm.\n" +
            "4. Túi đồ (Inventory) đã kích hoạt: Bấm 'B', 'Tab' hoặc 'I' để mở túi đồ, xem 12 ô chứa, bấm 'DÙNG' hồi 30 HP và đồng bộ MongoDB / Postgres!",
            "Tuyệt vời"
        );
    }
}
