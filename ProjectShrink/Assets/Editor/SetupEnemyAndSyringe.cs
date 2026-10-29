using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

public class SetupEnemyAndSyringe
{
    [InitializeOnLoadMethod]
    private static void AutoRunOnceAfterCompile()
    {
        EditorApplication.delayCall += () =>
        {
            if (Application.isPlaying) return;

            ApplyInstantAnimatorAndSceneFixes();

            GameObject existingBeetle = GameObject.Find("Enemy_Beetle");
            GameObject existingAnt = GameObject.Find("Enemy_Ant");
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

            if (GameObject.Find("player_v2") != null)
            {
                if (existingBeetle == null || needsMaterialFix || !SessionState.GetBool("BeetleMaterialFixed_v3", false))
                {
                    SessionState.SetBool("BeetleMaterialFixed_v3", true);
                    SetupAll(false);
                }
                else if (existingAnt == null)
                {
                    CreateOrUpdateAntEnemyInScene();
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                    EditorSceneManager.SaveOpenScenes();
                }
            }
        };
    }

    private static void ApplyInstantAnimatorAndSceneFixes()
    {
        EditorSettings.enterPlayModeOptionsEnabled = false;

        if (SessionState.GetBool("InstantResponseFixed_v13", false)) return;
        SessionState.SetBool("InstantResponseFixed_v13", true);

        // Tắt các component thừa trên đối tượng bù nhìn Walk.fbx cũ ("Player") đang nằm trong bộ nhớ Scene
        GameObject oldDummy = GameObject.Find("Player");
        if (oldDummy != null && oldDummy.GetComponentInChildren<Animator>() == null)
        {
            PlayerController dpc = oldDummy.GetComponent<PlayerController>();
            if (dpc != null && dpc.enabled) { dpc.enabled = false; EditorUtility.SetDirty(dpc); }
            PlayerHealth dph = oldDummy.GetComponent<PlayerHealth>();
            if (dph != null && dph.enabled) { dph.enabled = false; EditorUtility.SetDirty(dph); }
        }

        // Dọn dẹp các Missing Script trong Scene để giảm cảnh báo và tối ưu hiệu năng
        foreach (GameObject rootGo in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Transform t in rootGo.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
                {
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                    EditorUtility.SetDirty(t.gameObject);
                }
            }
        }

        // 1. Giữ nguyên tốc độ Animation cao 1.50x và độ nhạy 0.00s delay cho nhân vật
        AnimatorController ac = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/character_V2/character_v2_anmator.controller");
        if (ac != null && ac.layers.Length > 0)
        {
            AnimatorStateMachine sm = ac.layers[0].stateMachine;
            foreach (var tr in sm.anyStateTransitions)
            {
                tr.duration = 0.02f;
                tr.offset = 0.30f;
                tr.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
            }
            foreach (var childState in sm.states)
            {
                AnimatorState st = childState.state;
                if (st.name == "walk") st.speed = 1.50f;
                else if (st.name == "running") st.speed = 1.35f;
                else if (st.name != "Idle") st.speed = 1.60f;

                foreach (var tr in st.transitions)
                {
                    tr.duration = 0.02f;
                    tr.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
                    if (st.name == "Idle" && tr.destinationState != null && tr.destinationState.name == "walk")
                        tr.offset = 0.30f;
                }
            }
            EditorUtility.SetDirty(ac);
        }

        // 2. Cập nhật tốc độ di chuyển chậm lại cho Nhân Vật (0.36f / 0.72f) mà không đổi Animation
        GameObject playerObj = GameObject.Find("player_v2");
        if (playerObj != null)
        {
            playerObj.SetActive(true);
            PlayerController pc = playerObj.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.enabled = true;
                pc.walkSpeed = 0.36f;
                pc.runSpeed = 0.72f;
                pc.rotationSpeed = 35f;
                pc.playerRadius = 0.025f;
                pc.playerHeight = 0.068f;
                pc.minEnemySeparation = 0.085f;
                EditorUtility.SetDirty(pc);
            }

            PlayerHealth ph = playerObj.GetComponent<PlayerHealth>();
            if (ph == null) ph = playerObj.AddComponent<PlayerHealth>();
            ph.enabled = true;
            ph.showHealthUI = true;
            EditorUtility.SetDirty(ph);

            InventoryManager inv = playerObj.GetComponent<InventoryManager>();
            if (inv == null) inv = playerObj.AddComponent<InventoryManager>();
            inv.enabled = true;
            inv.spearForwardOffset = new Vector3(0.0002f, -0.0005f, 0.0008f);
            inv.spearSizeMultiplier = 1.0f;
            inv.backpackSizeMultiplier = 1.0f;
            EditorUtility.SetDirty(inv);
        }

        GameObject syringeObj = GameObject.Find("HealthSyringe");
        if (syringeObj != null)
        {
            syringeObj.transform.localScale = new Vector3(0.035f, 0.035f, 0.035f);
            ItemPickup ip = syringeObj.GetComponent<ItemPickup>();
            if (ip != null)
            {
                ip.targetItemScale = new Vector3(0.035f, 0.035f, 0.035f);
                EditorUtility.SetDirty(ip);
            }
            EditorUtility.SetDirty(syringeObj.transform);
        }

        // 3. Cập nhật Bọ Cánh Cứng (Enemy_Beetle - Giảm kích cỡ đi 1 nửa: 0.019f)
        GameObject beetleObj = GameObject.Find("Enemy_Beetle");
        if (beetleObj != null)
        {
            beetleObj.transform.localScale = new Vector3(0.019f, 0.019f, 0.019f);
            EditorUtility.SetDirty(beetleObj.transform);

            CapsuleCollider cc = beetleObj.GetComponent<CapsuleCollider>();
            if (cc != null)
            {
                cc.isTrigger = true;
                cc.radius = 1.6f;
                EditorUtility.SetDirty(cc);
            }

            EnemyAI ai = beetleObj.GetComponent<EnemyAI>();
            if (ai != null)
            {
                ai.enabled = true;
                ai.species = EnemyAI.EnemySpecies.Beetle;
                ai.enemyName = "Bọ Cánh Cứng Độc";
                ai.moveSpeed = 0.22f;
                ai.patrolSpeed = 0.14f;
                ai.useTerritoryPatrol = true;
                ai.territoryCenter = beetleObj.transform.position;
                ai.territoryRadius = 0.46f;
                ai.attackRange = 0.13f;
                ai.minStopDistance = 0.085f;
                ai.showHealthBar = true;
                ai.healthBarVisibleDistance = 0.65f;
                ai.healthBarHeightOffset = 0.052f;
                EditorUtility.SetDirty(ai);
            }
        }

        // 4. Tự động tạo hoặc cập nhật Kiến Khổng Lồ (Enemy_Ant) trên bản đồ
        CreateOrUpdateAntEnemyInScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }

    public static GameObject CreateOrUpdateAntEnemyInScene()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        GameObject oldAnt = GameObject.Find("Enemy_Ant");
        if (oldAnt != null)
        {
            Object.DestroyImmediate(oldAnt);
        }

        string antPrefabSourcePath = "Assets/enemy côn trùng/enemy côn trùng/Stylized Ant Character/Stylized Ant Character/Prefabs/Ant_Red.prefab";
        GameObject antSource = AssetDatabase.LoadAssetAtPath<GameObject>(antPrefabSourcePath);
        if (antSource == null)
        {
            Debug.LogWarning("Không tìm thấy prefab Ant_Red.prefab tại: " + antPrefabSourcePath);
            return null;
        }

        string antMatPath = "Assets/enemy côn trùng/enemy côn trùng/Stylized Ant Character/Stylized Ant Character/Materials/Ant_material_Color1.mat";
        Material antMat = AssetDatabase.LoadAssetAtPath<Material>(antMatPath);
        if (antMat != null)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");
            if (urpLit != null) antMat.shader = urpLit;

            Texture2D antAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/enemy côn trùng/enemy côn trùng/Stylized Ant Character/Stylized Ant Character/Textures/ant_albedo.png");
            if (antAlbedo != null)
            {
                antMat.SetTexture("_BaseMap", antAlbedo);
                antMat.SetTexture("_MainTex", antAlbedo);
                antMat.SetColor("_BaseColor", Color.white);
                antMat.SetColor("_Color", Color.white);
            }
            EditorUtility.SetDirty(antMat);
        }

        GameObject antInstance = (GameObject)PrefabUtility.InstantiatePrefab(antSource);
        antInstance.name = "Enemy_Ant";

        if (antMat != null)
        {
            foreach (Renderer r in antInstance.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = antMat;
            }
        }

        // Đặt vị trí vùng lãnh thổ riêng của Kiến Khổng Lồ ở khu vực giữa-trái phòng (Giảm kích cỡ 1 nửa: 0.0475f)
        Vector3 antTerritoryPos = new Vector3(1.48f, 0.05f, 0.52f);
        antInstance.transform.position = antTerritoryPos;
        antInstance.transform.rotation = Quaternion.Euler(0f, 160f, 0f);
        antInstance.transform.localScale = new Vector3(0.0475f, 0.0475f, 0.0475f);

        // Chuyển mọi Collider có sẵn thành Trigger để không bao giờ chặn cứng người chơi
        foreach (Collider c in antInstance.GetComponentsInChildren<Collider>(true))
        {
            c.isTrigger = true;
        }

        Animator anim = antInstance.GetComponent<Animator>();
        if (anim == null) anim = antInstance.AddComponent<Animator>();
        RuntimeAnimatorController antController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Prefabs/Enemy_Ant_Animator.controller");
        if (antController != null)
        {
            anim.runtimeAnimatorController = antController;
        }
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        EnemyAI ai = antInstance.GetComponent<EnemyAI>();
        if (ai == null) ai = antInstance.AddComponent<EnemyAI>();
        ai.species = EnemyAI.EnemySpecies.Ant;
        ai.enemyName = "Kiến Khổng Lồ";
        ai.maxHealth = 45f;
        ai.currentHealth = 45f;
        ai.moveSpeed = 0.22f;
        ai.patrolSpeed = 0.14f;
        ai.useTerritoryPatrol = true;
        ai.territoryCenter = antTerritoryPos;
        ai.territoryRadius = 0.45f;
        ai.detectionRange = 0.48f;
        ai.attackRange = 0.12f;
        ai.minStopDistance = 0.085f;
        ai.attackDamage = 12f;
        ai.showHealthBar = true;
        ai.healthBarVisibleDistance = 0.65f;
        ai.healthBarHeightOffset = 0.058f;
        ai.animator = anim;

        string savedAntPrefabPath = "Assets/Prefabs/Enemy_Ant.prefab";
        PrefabUtility.SaveAsPrefabAsset(antInstance, savedAntPrefabPath);
        Undo.RegisterCreatedObjectUndo(antInstance, "Place Enemy_Ant");

        return antInstance;
    }

    [MenuItem("Tools/Tao Ong Tiem va Quai Vat")]
    public static void MenuSetupAll()
    {
        SetupAll(true);
    }

    public static void SetupAll(bool showDialog)
    {
        GameObject oldSyringe = GameObject.Find("HealthSyringe");
        if (oldSyringe != null) Object.DestroyImmediate(oldSyringe);
        GameObject oldBeetle = GameObject.Find("Enemy_Beetle");
        if (oldBeetle != null) Object.DestroyImmediate(oldBeetle);

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        // 1. TẠO PREFAB ỐNG TIÊM HỒI MÁU (HealthSyringe.prefab)
        GameObject syringeModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/health_syringe.obj");
        GameObject syringePrefab = null;
        if (syringeModel != null)
        {
            GameObject syringeInstance = (GameObject)PrefabUtility.InstantiatePrefab(syringeModel);
            syringeInstance.name = "HealthSyringe";
            syringeInstance.transform.localScale = new Vector3(0.035f, 0.035f, 0.035f);
            syringeInstance.transform.rotation = Quaternion.Euler(45f, 0f, 0f);

            SphereCollider sc = syringeInstance.GetComponent<SphereCollider>();
            if (sc == null) sc = syringeInstance.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 1.5f;

            ItemPickup pickup = syringeInstance.GetComponent<ItemPickup>();
            if (pickup == null) pickup = syringeInstance.AddComponent<ItemPickup>();
            pickup.itemType = ItemPickup.ItemType.Health;
            pickup.healAmount = 30f;
            pickup.enableFloating = true;
            pickup.floatAmplitude = 0.012f;
            pickup.rotationSpeed = 65f;

            string syringePrefabPath = "Assets/Prefabs/HealthSyringe.prefab";
            syringePrefab = PrefabUtility.SaveAsPrefabAsset(syringeInstance, syringePrefabPath);

            syringeInstance.transform.position = new Vector3(1.68f, 0.06f, 0.05f);
            Undo.RegisterCreatedObjectUndo(syringeInstance, "Place HealthSyringe");
        }

        // 2. TẠO PREFAB BỌ CÁNH CỨNG ĐỘC (Enemy_Beetle.prefab)
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

            if (beetleMat != null)
            {
                foreach (Renderer r in beetleInstance.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterial = beetleMat;
                }
            }

            Vector3 beetlePos = new Vector3(1.68f, 0.05f, 1.40f);
            beetleInstance.transform.localScale = new Vector3(0.019f, 0.019f, 0.019f);
            beetleInstance.transform.position = beetlePos;
            beetleInstance.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            CapsuleCollider cc = beetleInstance.GetComponent<CapsuleCollider>();
            if (cc == null) cc = beetleInstance.AddComponent<CapsuleCollider>();
            cc.isTrigger = true;
            cc.center = new Vector3(0f, 2.5f, 0f);
            cc.radius = 1.6f;
            cc.height = 8f;

            EnemyAI ai = beetleInstance.GetComponent<EnemyAI>();
            if (ai == null) ai = beetleInstance.AddComponent<EnemyAI>();
            ai.species = EnemyAI.EnemySpecies.Beetle;
            ai.enemyName = "Bọ Cánh Cứng Độc";
            ai.maxHealth = 40f;
            ai.currentHealth = 40f;
            ai.moveSpeed = 0.22f;
            ai.patrolSpeed = 0.14f;
            ai.useTerritoryPatrol = true;
            ai.territoryCenter = beetlePos;
            ai.territoryRadius = 0.46f;
            ai.detectionRange = 0.50f;
            ai.attackRange = 0.22f;
            ai.attackDamage = 12f;
            ai.dropItemPrefab = syringePrefab;

            Animator anim = beetleInstance.GetComponent<Animator>();
            if (anim != null) ai.animator = anim;

            string beetlePrefabPath = "Assets/Prefabs/Enemy_Beetle.prefab";
            PrefabUtility.SaveAsPrefabAsset(beetleInstance, beetlePrefabPath);
            Undo.RegisterCreatedObjectUndo(beetleInstance, "Place Enemy_Beetle");
        }

        // 3. TẠO PREFAB KIẾN KHỔNG LỒ (Enemy_Ant.prefab) CÓ LÃNH THỔ TUẦN TRA RIÊNG
        CreateOrUpdateAntEnemyInScene();

        // 4. Cập nhật player_v2
        GameObject playerObj = GameObject.Find("player_v2");
        if (playerObj != null)
        {
            playerObj.tag = "Player";
            PlayerController pc = playerObj.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.walkSpeed = 0.36f;
                pc.runSpeed = 0.72f;
                EditorUtility.SetDirty(pc);
            }
            InventoryManager inv = playerObj.GetComponent<InventoryManager>();
            if (inv == null) inv = playerObj.AddComponent<InventoryManager>();
            EditorUtility.SetDirty(inv);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "Thiết Lập Hoàn Tất!",
                "Đã cài đặt thành công:\n\n" +
                "1. Bọ Cánh Cứng Độc (Enemy_Beetle): Khi chết rơi ra [Mạng Nhện] + [Gai Độc] + [Ống Máu].\n" +
                "2. Kiến Khổng Lồ (Enemy_Ant): Có lãnh thổ tuần tra riêng, thanh máu đi theo quái, khi chết rơi ra [Xương Dài].\n" +
                "3. Hệ thống Chế Tạo (Phím C hoặc nút Tab trong Túi Đồ B): Đặt 3 nguyên liệu vào 3 ô để chế tạo & trang bị [Giáo Gai Độc]!\n" +
                "4. Đã giảm nhẹ tốc độ di chuyển của Nhân vật và Quái vật (giữ nguyên tốc độ Animation 0.00s delay)!",
                "Tuyệt vời"
            );
        }
    }
}
