using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

public class SetupAntEnemy
{
    private const string ControllerPath = "Assets/Stylized Ant Character/Animation Controlers/Ant_Enemy_Controller.controller";
    private const string PrefabPath = "Assets/Stylized Ant Character/Prefabs/Ant_Red.prefab";

    [InitializeOnLoadMethod]
    private static void AutoRunOnCompile()
    {
        EditorApplication.delayCall += () =>
        {
            if (Application.isPlaying) return;

            GameObject ant = GameObject.Find("Enemy_Ant");
            if (ant != null)
            {
                EnemyAI ai = ant.GetComponent<EnemyAI>();
                if (ai != null)
                {
                    ai.patrolSpeed = 0.15f;
                    ai.chaseSpeed = 0.32f;
                    ai.rotationSpeed = 5f;
                    ai.attackRange = 0.16f;
                    ai.attackCooldown = 1.5f;
                    ai.viewRadius = 0.70f;
                    ai.viewAngle = 65f;
                    ai.hearingRadius = 1.1f;
                    EditorUtility.SetDirty(ant);
                }
            }
            else if (GameObject.Find("player_v2") != null)
            {
                SpawnAntEnemy();
            }
        };
    }

    [MenuItem("Tools/Them Enemy Kien (Stylized Ant)")]
    public static void SpawnAntEnemy()
    {
        // 1. Tìm hoặc tạo AnimatorController hoàn chỉnh cho Kiến
        AnimatorController controller = SetupAntAnimatorController();

        // 2. Load Prefab Kiến
        GameObject antPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (antPrefab == null)
        {
            Debug.LogError($"[SetupAntEnemy] Không tìm thấy Prefab tại {PrefabPath}!");
            return;
        }

        // Xóa Enemy_Ant cũ nếu đã có để làm mới
        GameObject existingAnt = GameObject.Find("Enemy_Ant");
        if (existingAnt != null)
        {
            Object.DestroyImmediate(existingAnt);
        }

        // 3. Tạo Instance Kiến trong Scene
        GameObject antInstance = (GameObject)PrefabUtility.InstantiatePrefab(antPrefab);
        antInstance.name = "Enemy_Ant";

        // Tọa độ đứng giữa phòng, đối diện với player_v2
        antInstance.transform.position = new Vector3(1.65f, 0.046889f, 0.85f);
        antInstance.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // Quay mặt về phía người chơi
        antInstance.transform.localScale = new Vector3(0.04f, 0.04f, 0.04f); // Tỷ lệ mini vừa vặn với căn phòng

        // 4. Cấu hình Animator
        Animator anim = antInstance.GetComponent<Animator>();
        if (anim == null) anim = antInstance.AddComponent<Animator>();
        if (controller != null) anim.runtimeAnimatorController = controller;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // 5. Cấu hình Collider
        BoxCollider boxCol = antInstance.GetComponent<BoxCollider>();
        if (boxCol != null)
        {
            boxCol.center = new Vector3(0f, 0.5f, 0f);
            boxCol.size = new Vector3(1.2f, 1.2f, 1.5f);
        }
        else
        {
            CapsuleCollider capCol = antInstance.GetComponent<CapsuleCollider>();
            if (capCol == null) capCol = antInstance.AddComponent<CapsuleCollider>();
            capCol.center = new Vector3(0f, 0.5f, 0f);
            capCol.radius = 0.6f;
            capCol.height = 1.2f;
        }

        // 6. Gắn AI Enemy (EnemyAI.cs)
        EnemyAI ai = antInstance.GetComponent<EnemyAI>();
        if (ai == null) ai = antInstance.AddComponent<EnemyAI>();

        ai.maxHealth = 50f;
        ai.currentHealth = 50f;
        ai.patrolSpeed = 0.15f;
        ai.chaseSpeed = 0.32f;
        ai.rotationSpeed = 5f;
        ai.attackDamage = 15f;
        ai.attackRange = 0.16f;
        ai.attackCooldown = 1.5f;
        ai.viewRadius = 0.70f;
        ai.viewAngle = 65f;
        ai.hearingRadius = 1.1f;
        ai.animator = anim;

        // Gán mục tiêu là player_v2
        GameObject playerObj = GameObject.Find("player_v2");
        if (playerObj != null)
        {
            ai.targetPlayer = playerObj.transform;
        }

        // Gán Prefab rơi đồ khi chết là HealthSyringe
        GameObject syringePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/HealthSyringe.prefab");
        if (syringePrefab != null)
        {
            ai.dropItemPrefab = syringePrefab;
        }

        // 7. Lưu Prefab Enemy_Ant tái sử dụng
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        string newPrefabPath = "Assets/Prefabs/Enemy_Ant.prefab";
        PrefabUtility.SaveAsPrefabAsset(antInstance, newPrefabPath);
        Debug.Log($"✅ [SetupAntEnemy] Đã tạo thành công Enemy_Ant tại {newPrefabPath}");

        // 8. Đánh dấu lưu Scene
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Undo.RegisterCreatedObjectUndo(antInstance, "Spawn Enemy_Ant");
        Debug.Log("🎉 [SetupAntEnemy] ĐÃ THÊM QUÁI VẬT KIẾN (Enemy_Ant) THÀNH CÔNG VÀO PHÒNG!");
    }

    private static AnimatorController SetupAntAnimatorController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null) return controller;

        // Tạo Animator Controller mới
        controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var rootStateMachine = controller.layers[0].stateMachine;

        // Load các animation clips
        string animFolder = "Assets/Stylized Ant Character/FBX/Animations/";
        AnimationClip idleClip = LoadAnimationClip(animFolder + "AS_Ant_Idle.fbx");
        AnimationClip walkClip = LoadAnimationClip(animFolder + "AS_Ant_Walk.fbx");
        AnimationClip attackClip = LoadAnimationClip(animFolder + "AS_Ant_Attack.fbx");
        AnimationClip hitClip = LoadAnimationClip(animFolder + "AS_Ant_Taking_Damage_V1_L.fbx");
        AnimationClip deathClip = LoadAnimationClip(animFolder + "AS_Ant_Death_V1.fbx");

        // Thêm các States tương thích 100% với EnemyAI.cs
        var idleState = rootStateMachine.AddState("idle1");
        idleState.motion = idleClip;
        rootStateMachine.defaultState = idleState;

        var walkState = rootStateMachine.AddState("walk");
        walkState.motion = walkClip;

        var attackState = rootStateMachine.AddState("atack1");
        attackState.motion = attackClip;

        var hitState = rootStateMachine.AddState("gethit");
        hitState.motion = hitClip;

        var deathState = rootStateMachine.AddState("death");
        deathState.motion = deathClip;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static AnimationClip LoadAnimationClip(string fbxPath)
    {
        Object[] objects = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        foreach (Object obj in objects)
        {
            if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                return clip;
            }
        }
        return null;
    }
}
