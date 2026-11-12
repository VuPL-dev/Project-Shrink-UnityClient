using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectShrink.Security;

/// <summary>
/// Trình dựng Tự động PHÒNG 4: KHU AN NINH & CAMERA (Security Room):
/// - Tạo Prefab Robot An Ninh (SecurityDrone.prefab) 100% URP Lit/Emission.
/// - Tạo Scene hoàn chỉnh Assets/Scenes/Room4_Security.unity với:
///   + Kiến trúc phòng công nghiệp, tường chắn, kệ sắt & tủ (từ AssetStorage) làm vật cản chắn Laser.
///   + 2 Camera An Ninh (denanninh.glb) quét Spotlight phát hiện Player.
///   + Hệ thống Bẫy Quét Laser dày đặc (Nhấp nháy, Quét góc, Tia nhảy).
///   + 2 Robot An Ninh (SecurityDrone) bay tuần tra, nghe âm thanh, nhắm & bắn Laser.
///   + Trạm Điều Khiển (BanThiNghiem.glb) Hack phím [E] để tắt toàn bộ Laser & mở Cửa Thoát.
/// </summary>
public class SetupRoom4Security
{
    private const string ScenePath = "Assets/Scenes/Room4_Security.unity";
    private const string DronePrefabPath = "Assets/Prefabs/SecurityDrone.prefab";

    private const string AutoBuildKey = "Room4_AutoBuilt_v3";

    [InitializeOnLoadMethod]
    private static void AutoCreateRoom4Once()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            TryAutoBuildRoom4();
        };
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += TryAutoBuildRoom4;
        }
    }

    private static void TryAutoBuildRoom4()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(ScenePath) && File.Exists(DronePrefabPath) && SessionState.GetBool(AutoBuildKey, false)) return;
        SessionState.SetBool(AutoBuildKey, true);

        Scene active = SceneManager.GetActiveScene();
        if (active.name == "SampleScene")
        {
            EnsurePortalInSampleScene();
        }
        EditorSceneManager.SaveOpenScenes();

        // Tự động dựng lại và mở Map Phòng 4 với góc nhìn 2.5D Cutaway không bị tường che
        BuildRoom4Scene(false);
    }

    [MenuItem("Tools/Xay Dung va Mo Phong 4 (Khu An Ninh)")]
    public static void MenuBuildAndOpenRoom4()
    {
        EditorSceneManager.SaveOpenScenes();
        BuildRoom4Scene(true);
    }

    [MenuItem("Tools/Quay Ve Phong 1 (SampleScene)")]
    public static void MenuOpenSampleScene()
    {
        EditorSceneManager.SaveOpenScenes();
        if (File.Exists("Assets/Scenes/SampleScene.unity"))
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            EnsurePortalInSampleScene();
            EditorSceneManager.SaveOpenScenes();
        }
    }

    private static void EnsurePortalInSampleScene()
    {
        if (GameObject.Find("Portal_To_Room4") != null) return;

        GameObject doorRoot = new GameObject("Portal_To_Room4");
        doorRoot.transform.position = new Vector3(1.88f, 0.048f, 1.72f);

        Material statusMat = CreateUrpLitMaterial("SampleToRoom4Mat", new Color(0.2f, 1f, 0.45f), 0.1f, 0.9f, new Color(0.2f, 1f, 0.45f) * 2.0f);
        GameObject panel = CreateBox(doorRoot.transform, "PortalGlow", new Vector3(0f, 0.08f, 0f), new Vector3(0.12f, 0.16f, 0.02f), statusMat, false);

        RoomPortal portal = doorRoot.AddComponent<RoomPortal>();
        portal.targetSceneName = "Room4_Security";
        portal.portalLabel = "Phòng 4: Khu An Ninh & Camera";
        portal.isUnlocked = true;
        portal.interactRadius = 0.24f;
        portal.doorLightRenderer = panel.GetComponent<Renderer>();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    public static void BuildRoom4Scene(bool showDialog)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        // 1. Tạo hoặc cập nhật Prefab SecurityDrone
        GameObject dronePrefab = CreateOrUpdateSecurityDronePrefab();

        // 2. Tạo Scene mới cho Phòng 4
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Ánh sáng tổng thể (tông An ninh công nghệ lạnh, dễ quan sát)
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.18f, 0.22f, 0.28f, 1f);

        GameObject dirLightObj = new GameObject("Directional Light");
        Light dirLight = dirLightObj.AddComponent<Light>();
        dirLight.type = LightType.Directional;
        dirLight.color = new Color(0.75f, 0.86f, 1f);
        dirLight.intensity = 0.65f;
        dirLight.shadows = LightShadows.Soft;
        dirLightObj.transform.rotation = Quaternion.Euler(52f, -32f, 0f);

        // 3. Hệ thống quản lý báo động toàn phòng
        GameObject alarmMgrObj = new GameObject("Security_Alarm_Manager");
        SecurityAlarmSystem alarmSys = alarmMgrObj.AddComponent<SecurityAlarmSystem>();
        alarmSys.roomTitle = "PHÒNG 4: KHU AN NINH & CAMERA";
        alarmSys.alarmDuration = 6.0f;

        // 4. Dựng kiến trúc Sàn, Tường, Đường dẫn & Đèn trang trí
        GameObject envRoot = new GameObject("Room4_Environment");
        BuildEnvironment(envRoot.transform);

        // 5. Đặt Vật cản / Chỗ nấp từ AssetStorage (Kệ sắt, Tủ bảo hộ) - có Collider chắn tia Laser
        GameObject propsRoot = new GameObject("Room4_Props_And_Cover");
        BuildCoverProps(propsRoot.transform);

        // 6. Đặt Camera An Ninh (denanninh.glb) gắn trên tường trái quét chéo hành lang
        GameObject camsRoot = new GameObject("Room4_Security_Cameras");
        CreateSecurityCamera(camsRoot.transform, "SecurityCamera_1", new Vector3(-0.62f, 0.25f, 0.90f), Quaternion.Euler(28f, 112f, 0f), 0f);
        CreateSecurityCamera(camsRoot.transform, "SecurityCamera_2", new Vector3(-0.62f, 0.25f, 2.05f), Quaternion.Euler(28f, 108f, 0f), 1.6f);

        // 7. Hệ thống Lưới Quét Laser dày đặc
        GameObject lasersRoot = new GameObject("Room4_Laser_Grid");
        // Vùng 1: Nhấp nháy xen kẽ
        CreateLaserNode(lasersRoot.transform, "Laser_Pulse_1A", new Vector3(-0.62f, 0.088f, 0.32f), Quaternion.Euler(0f, 90f, 0f),
            LaserScanner.LaserMode.PulseBlink, 1.24f, 0f, 0f, 0f);
        CreateLaserNode(lasersRoot.transform, "Laser_Pulse_1B", new Vector3(0.62f, 0.088f, 0.52f), Quaternion.Euler(0f, -90f, 0f),
            LaserScanner.LaserMode.PulseBlink, 1.24f, 0f, 0f, 1.5f);

        // Vùng 2: Quét góc qua lại (Sweep)
        CreateLaserNode(lasersRoot.transform, "Laser_Sweep_2A", new Vector3(-0.62f, 0.092f, 1.28f), Quaternion.Euler(0f, 90f, 0f),
            LaserScanner.LaserMode.SweepPingPong, 1.32f, 28f, 1.35f, 0f);
        CreateLaserNode(lasersRoot.transform, "Laser_Sweep_2B", new Vector3(0.62f, 0.112f, 1.56f), Quaternion.Euler(0f, -90f, 0f),
            LaserScanner.LaserMode.SweepPingPong, 1.32f, 26f, 1.15f, 1.4f);

        // Vùng 3: Tia thấp nhảy qua + Tia bảo vệ Trạm điều khiển
        CreateLaserNode(lasersRoot.transform, "Laser_LowJump_3A", new Vector3(-0.62f, 0.064f, 2.02f), Quaternion.Euler(0f, 90f, 0f),
            LaserScanner.LaserMode.Static, 1.24f, 0f, 0f, 0f);
        CreateLaserNode(lasersRoot.transform, "Laser_Guard_3B", new Vector3(0.62f, 0.095f, 2.48f), Quaternion.Euler(0f, -90f, 0f),
            LaserScanner.LaserMode.SweepPingPong, 1.30f, 22f, 1.6f, 0.5f);

        // 8. Cổng Thoát cuối phòng & Trạm Hack Điều Khiển
        RoomPortal exitPortal = CreateExitPortal(new Vector3(0.0f, 0.045f, 3.16f));
        CreateSecurityTerminal(new Vector3(0.0f, 0.045f, 2.86f), exitPortal);

        // 9. Đặt 2 Robot An Ninh (Security Drone) bay tuần tra bắn laser
        GameObject syringePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/HealthSyringe.prefab");
        SpawnDroneInstance(dronePrefab, "SecurityDrone_Alpha", new Vector3(-0.18f, 0.20f, 1.22f), 0.42f, syringePrefab);
        SpawnDroneInstance(dronePrefab, "SecurityDrone_Beta", new Vector3(0.18f, 0.20f, 2.38f), 0.42f, syringePrefab);

        // 10. Đặt 2 Ống Tiêm Hồi Máu hỗ trợ sau chỗ nấp
        if (syringePrefab != null)
        {
            GameObject s1 = (GameObject)PrefabUtility.InstantiatePrefab(syringePrefab);
            s1.name = "HealthSyringe_Cover1";
            s1.transform.position = new Vector3(-0.46f, 0.075f, 0.80f);

            GameObject s2 = (GameObject)PrefabUtility.InstantiatePrefab(syringePrefab);
            s2.name = "HealthSyringe_Cover2";
            s2.transform.position = new Vector3(0.46f, 0.075f, 2.15f);
        }

        // 11. Đặt Nhân Vật (player_v2) & Camera 2.5D Little Nightmares
        GameObject playerObj = CreatePlayerInScene(new Vector3(0.0f, 0.048f, -0.32f));
        CreateFollowCamera(playerObj != null ? playerObj.transform : null);

        // Lưu Scene và đăng ký vào Build Settings
        EditorSceneManager.SaveScene(scene, ScenePath);
        EnsureSceneInBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.LookAt(new Vector3(0f, 0.15f, 1.25f), Quaternion.Euler(32f, -28f, 0f), 1.85f);
            SceneView.lastActiveSceneView.Repaint();
        }

        Debug.Log("✅ [SetupRoom4Security] Đã xây dựng và mở sẵn Map Phòng 4 tại: " + ScenePath);

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "Đã Dựng Xong Phòng 4: Khu An Ninh & Camera!",
                "Scene mới đã được tạo tại:\nAssets/Scenes/Room4_Security.unity\n\n" +
                "Bao gồm đầy đủ:\n" +
                "1. Hệ thống 6 bẫy tia Laser (Nhấp nháy, Quét góc, Tia thấp nhảy qua).\n" +
                "2. 2 Camera An Ninh (denanninh.glb) quét Spotlight.\n" +
                "3. 2 Robot An Ninh (SecurityDrone) bay tuần tra, ngắm cảnh báo & bắn Laser.\n" +
                "4. Kệ Sắt & Tủ chắn tia Laser làm chỗ nấp.\n" +
                "5. Trạm Điều Khiển cuối phòng: Giữ phím [E] để Hack tắt toàn bộ Laser & mở Cửa Thoát!\n\n" +
                "Bấm Play để chơi thử ngay!",
                "Chơi Ngay"
            );
        }
    }

    static Material CreateUrpLitMaterial(string name, Color baseColor, float metallic = 0.2f, float smoothness = 0.45f, Color? emission = null)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material mat = new Material(shader);
        mat.name = name;
        mat.color = baseColor;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (emission.HasValue)
        {
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission.Value);
        }
        return mat;
    }

    static void EnsureMaterialsAreUrp(GameObject go)
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) return;

        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && mats[i].shader != null &&
                    (mats[i].shader.name == "Standard" || mats[i].shader.name.Contains("Error")))
                {
                    mats[i].shader = urpLit;
                    EditorUtility.SetDirty(mats[i]);
                    changed = true;
                }
            }
            if (changed) r.sharedMaterials = mats;
        }
    }

    static GameObject CreateOrUpdateSecurityDronePrefab()
    {
        GameObject drone = new GameObject("SecurityDrone");

        Material bodyMat = CreateUrpLitMaterial("DroneBodyMat", new Color(0.18f, 0.21f, 0.26f), 0.75f, 0.65f);
        Material trimMat = CreateUrpLitMaterial("DroneTrimMat", new Color(0.42f, 0.46f, 0.52f), 0.85f, 0.7f);
        Material eyeMat = CreateUrpLitMaterial("DroneEyeMat", new Color(0.2f, 0.9f, 1f), 0.1f, 0.9f, new Color(0.2f, 0.9f, 1f) * 2.2f);
        Material rotorMat = CreateUrpLitMaterial("DroneRotorMat", new Color(0.08f, 0.09f, 0.11f), 0.5f, 0.4f);

        // Thân cầu chính
        GameObject chassis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        chassis.name = "Chassis";
        chassis.transform.SetParent(drone.transform, false);
        chassis.transform.localScale = new Vector3(0.09f, 0.055f, 0.105f);
        Object.DestroyImmediate(chassis.GetComponent<Collider>());
        chassis.GetComponent<Renderer>().sharedMaterial = bodyMat;

        // Vòng giáp bảo vệ
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "ArmorRing";
        ring.transform.SetParent(drone.transform, false);
        ring.transform.localScale = new Vector3(0.115f, 0.012f, 0.115f);
        Object.DestroyImmediate(ring.GetComponent<Collider>());
        ring.GetComponent<Renderer>().sharedMaterial = trimMat;

        // Mắt cảm biến phát sáng phía trước
        GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eye.name = "SensorEye";
        eye.transform.SetParent(drone.transform, false);
        eye.transform.localPosition = new Vector3(0f, -0.004f, 0.048f);
        eye.transform.localScale = new Vector3(0.028f, 0.022f, 0.022f);
        Object.DestroyImmediate(eye.GetComponent<Collider>());
        Renderer eyeRenderer = eye.GetComponent<Renderer>();
        eyeRenderer.sharedMaterial = eyeMat;

        // Nòng pháo Laser phía dưới
        GameObject muzzle = new GameObject("MuzzlePoint");
        muzzle.transform.SetParent(drone.transform, false);
        muzzle.transform.localPosition = new Vector3(0f, -0.022f, 0.052f);

        GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        barrel.name = "LaserBarrel";
        barrel.transform.SetParent(drone.transform, false);
        barrel.transform.localPosition = new Vector3(0f, -0.022f, 0.032f);
        barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        barrel.transform.localScale = new Vector3(0.014f, 0.024f, 0.014f);
        Object.DestroyImmediate(barrel.GetComponent<Collider>());
        barrel.GetComponent<Renderer>().sharedMaterial = trimMat;

        // 2 Cánh động cơ Rotor trái/phải
        Transform rotorL = CreateRotorAssembly(drone.transform, "RotorLeft", new Vector3(-0.072f, 0.018f, 0f), trimMat, rotorMat);
        Transform rotorR = CreateRotorAssembly(drone.transform, "RotorRight", new Vector3(0.072f, 0.018f, 0f), trimMat, rotorMat);

        // Đèn Spotlight quét sàn
        GameObject lightObj = new GameObject("ScannerSpotlight");
        lightObj.transform.SetParent(drone.transform, false);
        lightObj.transform.localPosition = new Vector3(0f, -0.01f, 0.04f);
        lightObj.transform.localRotation = Quaternion.Euler(42f, 0f, 0f);
        Light spot = lightObj.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.range = 1.15f;
        spot.spotAngle = 48f;
        spot.intensity = 2.6f;
        spot.color = new Color(0.2f, 0.85f, 1f);

        // Collider nhận sát thương cận chiến từ Player
        SphereCollider col = drone.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.085f;

        // Gắn Script AI
        SecurityDroneAI ai = drone.AddComponent<SecurityDroneAI>();
        ai.rotorLeft = rotorL;
        ai.rotorRight = rotorR;
        ai.muzzleTransform = muzzle.transform;
        ai.scannerLight = spot;
        ai.eyeRenderer = eyeRenderer;

        // Lưu material vào Assets để Prefab giữ reference sạch sẽ không bị mất khi lưu scene
        string matFolder = "Assets/Materials/Security";
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(matFolder)) AssetDatabase.CreateFolder("Assets/Materials", "Security");
        AssetDatabase.CreateAsset(bodyMat, matFolder + "/DroneBodyMat.mat");
        AssetDatabase.CreateAsset(trimMat, matFolder + "/DroneTrimMat.mat");
        AssetDatabase.CreateAsset(eyeMat, matFolder + "/DroneEyeMat.mat");
        AssetDatabase.CreateAsset(rotorMat, matFolder + "/DroneRotorMat.mat");

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(drone, DronePrefabPath);
        Object.DestroyImmediate(drone);
        return savedPrefab;
    }

    static Transform CreateRotorAssembly(Transform parent, string name, Vector3 localPos, Material armMat, Material bladeMat)
    {
        GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
        arm.name = name + "_Arm";
        arm.transform.SetParent(parent, false);
        arm.transform.localPosition = new Vector3(localPos.x * 0.55f, 0.008f, 0f);
        arm.transform.localScale = new Vector3(0.06f, 0.008f, 0.012f);
        Object.DestroyImmediate(arm.GetComponent<Collider>());
        arm.GetComponent<Renderer>().sharedMaterial = armMat;

        GameObject hub = new GameObject(name);
        hub.transform.SetParent(parent, false);
        hub.transform.localPosition = localPos;

        GameObject blade1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blade1.name = "Blade1";
        blade1.transform.SetParent(hub.transform, false);
        blade1.transform.localScale = new Vector3(0.085f, 0.003f, 0.012f);
        Object.DestroyImmediate(blade1.GetComponent<Collider>());
        blade1.GetComponent<Renderer>().sharedMaterial = bladeMat;

        GameObject blade2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blade2.name = "Blade2";
        blade2.transform.SetParent(hub.transform, false);
        blade2.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        blade2.transform.localScale = new Vector3(0.085f, 0.003f, 0.012f);
        Object.DestroyImmediate(blade2.GetComponent<Collider>());
        blade2.GetComponent<Renderer>().sharedMaterial = bladeMat;

        return hub.transform;
    }

    static void BuildEnvironment(Transform parent)
    {
        Material floorMat = CreateUrpLitMaterial("SecurityFloorMat", new Color(0.14f, 0.17f, 0.22f), 0.35f, 0.55f);
        Material wallMat = CreateUrpLitMaterial("SecurityWallMat", new Color(0.20f, 0.24f, 0.30f), 0.45f, 0.40f);
        Material pillarMat = CreateUrpLitMaterial("SecurityPillarMat", new Color(0.28f, 0.33f, 0.40f), 0.65f, 0.55f);
        Material stripMat = CreateUrpLitMaterial("SecurityNeonStripMat", new Color(0.12f, 0.78f, 1f), 0.1f, 0.9f, new Color(0.12f, 0.78f, 1f) * 1.5f);

        // Sàn chính: rộng 1.40m (x: -0.70 -> +0.70), dài 3.90m (z: -0.60 -> +3.30), mặt trên đúng y = 0.045f
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Security_Floor";
        floor.transform.SetParent(parent, false);
        floor.transform.localPosition = new Vector3(0f, 0.02f, 1.35f);
        floor.transform.localScale = new Vector3(1.40f, 0.05f, 3.90f);
        floor.GetComponent<Renderer>().sharedMaterial = floorMat;

        // Dải đèn dẫn hướng 2 bên sàn
        CreateBox(parent, "Floor_NeonLeft", new Vector3(-0.56f, 0.046f, 1.35f), new Vector3(0.022f, 0.004f, 3.60f), stripMat, false);
        CreateBox(parent, "Floor_NeonRight", new Vector3(0.56f, 0.046f, 1.35f), new Vector3(0.022f, 0.004f, 3.60f), stripMat, false);

        // Kiến trúc Cutaway 2.5D (Little Nightmares Diorama):
        // - Tường Trái (Wall_Left) và Tường Đích (Wall_Front) cao để tạo chiều sâu không gian.
        // - Phía Camera nhìn vào (Bên Phải +X và Phía Sau -Z) dùng gờ chắn thấp (Cutaway Curb) để KHÔNG BAO GIỜ che khuất tầm nhìn Camera!
        CreateBox(parent, "Wall_Left", new Vector3(-0.68f, 0.24f, 1.35f), new Vector3(0.06f, 0.40f, 3.95f), wallMat, true);
        CreateBox(parent, "Wall_Front", new Vector3(0f, 0.24f, 3.28f), new Vector3(1.40f, 0.40f, 0.06f), wallMat, true);

        // Các cột gân công nghiệp dọc tường trái
        for (int i = 0; i < 5; i++)
        {
            float zPos = -0.20f + i * 0.80f;
            CreateBox(parent, "Wall_Left_Rib_" + i, new Vector3(-0.64f, 0.24f, zPos), new Vector3(0.03f, 0.40f, 0.05f), pillarMat, false);
        }

        // Gờ chắn thấp phía Camera (bên phải +X và phía sau -Z) + Trụ gắn tia Laser bên phải
        CreateBox(parent, "Curb_Right_Cutaway", new Vector3(0.68f, 0.065f, 1.35f), new Vector3(0.06f, 0.045f, 3.95f), pillarMat, false);
        CreateBox(parent, "Curb_Back_Cutaway", new Vector3(0f, 0.065f, -0.58f), new Vector3(1.40f, 0.045f, 0.06f), pillarMat, true);

        // Trụ đỡ thấp cho các trạm phóng Laser bên phải (cao 0.13m, không che Camera)
        CreateBox(parent, "LaserPylon_R1", new Vector3(0.64f, 0.105f, 0.52f), new Vector3(0.04f, 0.12f, 0.04f), pillarMat, false);
        CreateBox(parent, "LaserPylon_R2", new Vector3(0.64f, 0.105f, 1.56f), new Vector3(0.04f, 0.12f, 0.04f), pillarMat, false);
        CreateBox(parent, "LaserPylon_R3", new Vector3(0.64f, 0.105f, 2.48f), new Vector3(0.04f, 0.12f, 0.04f), pillarMat, false);

        // Tường vô hình bên phải để chặn tia Laser dừng đúng mép hành lang mà không che khuất Camera
        GameObject invisibleRightBlocker = new GameObject("Wall_Right_InvisibleLaserBlocker");
        invisibleRightBlocker.transform.SetParent(parent, false);
        invisibleRightBlocker.transform.localPosition = new Vector3(0.68f, 0.24f, 1.35f);
        BoxCollider rightCol = invisibleRightBlocker.AddComponent<BoxCollider>();
        rightCol.size = new Vector3(0.06f, 0.40f, 3.95f);

        // Đèn trần chiếu sáng phân khu rõ nét
        CreateCeilingPointLight(parent, "Light_StartZone", new Vector3(0f, 0.45f, -0.05f), new Color(0.75f, 0.92f, 1f), 1.8f, 1.8f);
        CreateCeilingPointLight(parent, "Light_MidCorridor", new Vector3(0f, 0.45f, 1.35f), new Color(1f, 0.65f, 0.55f), 1.6f, 1.9f);
        CreateCeilingPointLight(parent, "Light_TerminalZone", new Vector3(0f, 0.45f, 2.75f), new Color(0.55f, 0.98f, 0.88f), 1.8f, 1.8f);
    }

    static GameObject CreateBox(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat, bool keepCollider)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static void CreateCeilingPointLight(Transform parent, string name, Vector3 pos, Color col, float intensity, float range)
    {
        GameObject lObj = new GameObject(name);
        lObj.transform.SetParent(parent, false);
        lObj.transform.localPosition = pos;
        Light l = lObj.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = col;
        l.intensity = intensity;
        l.range = range;
    }

    /// <summary>
    /// Chuẩn hóa kích thước mô hình 3D (.glb/.fbx) về đúng chiều cao mục tiêu và đặt chân sát mặt sàn.
    /// Giúp các kệ sắt/tủ vừa vặn làm chỗ nấp mà không bao giờ phóng đại khổng lồ che màn hình.
    /// </summary>
    static Bounds NormalizeModelToHeight(GameObject go, float targetHeight, float groundY)
    {
        Renderer[] rends = go.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0)
            return new Bounds(go.transform.position, Vector3.one * targetHeight);

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        if (b.size.y > 0.0005f)
        {
            float scaleFactor = targetHeight / b.size.y;
            go.transform.localScale *= scaleFactor;
        }

        // Tính lại Bounds sau khi scale để đặt đáy chạm đúng mặt sàn groundY
        b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        float deltaY = groundY - b.min.y;
        go.transform.position += new Vector3(0f, deltaY, 0f);

        b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }

    static void BuildCoverProps(Transform parent)
    {
        PlaceAssetCover(parent, "Cover_Rack_1", "Assets/AssetStorage/itemInRoom/kesat1.glb",
            new Vector3(-0.30f, 0.045f, 0.72f), Quaternion.Euler(90f, 0f, 0f), 0.13f);

        PlaceAssetCover(parent, "Cover_Cabinet_1", "Assets/AssetStorage/itemInRoom/tu1.glb",
            new Vector3(0.28f, 0.045f, 1.12f), Quaternion.Euler(90f, 0f, 90f), 0.13f);

        PlaceAssetCover(parent, "Cover_Rack_2", "Assets/AssetStorage/itemInRoom/kesat2.glb",
            new Vector3(-0.30f, 0.045f, 1.78f), Quaternion.Euler(90f, 0f, 0f), 0.13f);

        PlaceAssetCover(parent, "Cover_Cabinet_2", "Assets/AssetStorage/itemInRoom/tu2.glb",
            new Vector3(0.28f, 0.045f, 2.26f), Quaternion.Euler(90f, 0f, -90f), 0.13f);
    }

    static void PlaceAssetCover(Transform parent, string name, string assetPath, Vector3 pos, Quaternion rot, float targetHeight)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab != null)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = Vector3.one;
            EnsureMaterialsAreUrp(go);

            Bounds b = NormalizeModelToHeight(go, targetHeight, pos.y);

            // Gắn BoxCollider gọn gàng theo kích thước đã chuẩn hóa để chặn tia Laser
            GameObject colObj = new GameObject(name + "_Collider");
            colObj.transform.SetParent(parent, false);
            colObj.transform.position = b.center;
            BoxCollider bc = colObj.AddComponent<BoxCollider>();
            bc.size = new Vector3(
                Mathf.Clamp(b.size.x, 0.12f, 0.24f),
                Mathf.Clamp(b.size.y, 0.12f, 0.16f),
                Mathf.Clamp(b.size.z, 0.08f, 0.20f)
            );
        }
        else
        {
            Material fallbackMat = CreateUrpLitMaterial(name + "_Mat", new Color(0.30f, 0.35f, 0.42f), 0.6f, 0.4f);
            CreateBox(parent, name, pos + new Vector3(0f, 0.065f, 0f), new Vector3(0.18f, 0.13f, 0.10f), fallbackMat, true);
        }
    }

    static void CreateSecurityCamera(Transform parent, string name, Vector3 pos, Quaternion rot, float phaseOffset)
    {
        GameObject camRoot = new GameObject(name);
        camRoot.transform.SetParent(parent, false);
        camRoot.transform.position = pos;
        camRoot.transform.rotation = rot;

        GameObject camAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetStorage/itemInRoom/denanninh.glb");
        if (camAsset != null)
        {
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(camAsset);
            model.name = "CameraModel";
            model.transform.SetParent(camRoot.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            model.transform.localScale = Vector3.one;
            EnsureMaterialsAreUrp(model);

            Renderer[] rends = model.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (maxDim > 0.001f)
                {
                    model.transform.localScale *= (0.07f / maxDim);
                }
            }
        }

        // Đèn mắt camera phát sáng + Spotlight quét xuống sàn
        Material lensMat = CreateUrpLitMaterial(name + "_LensMat", new Color(0.15f, 0.85f, 1f), 0f, 1f, new Color(0.15f, 0.85f, 1f) * 2f);
        GameObject lens = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        lens.name = "CameraLens";
        lens.transform.SetParent(camRoot.transform, false);
        lens.transform.localPosition = Vector3.zero;
        lens.transform.localScale = Vector3.one * 0.026f;
        Object.DestroyImmediate(lens.GetComponent<Collider>());
        Renderer lensRend = lens.GetComponent<Renderer>();
        lensRend.sharedMaterial = lensMat;

        GameObject lightObj = new GameObject("CameraSpotlight");
        lightObj.transform.SetParent(camRoot.transform, false);
        lightObj.transform.localPosition = Vector3.zero;
        Light spot = lightObj.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.range = 1.35f;
        spot.spotAngle = 46f;
        spot.intensity = 2.8f;
        spot.color = new Color(0.15f, 0.85f, 1f);

        SecurityCameraController ctrl = camRoot.AddComponent<SecurityCameraController>();
        ctrl.headPivot = camRoot.transform;
        ctrl.scannerLight = spot;
        ctrl.indicatorRenderer = lensRend;
        ctrl.sweepAngle = 36f;
        ctrl.sweepSpeed = 0.95f;
        ctrl.phaseOffset = phaseOffset;
        ctrl.viewRadius = 0.95f;
        ctrl.viewAngle = 46f;
    }

    static void CreateLaserNode(Transform parent, string name, Vector3 pos, Quaternion rot,
        LaserScanner.LaserMode mode, float maxLength, float sweepAngle, float sweepSpeed, float phaseOffset)
    {
        GameObject emitter = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        emitter.name = name;
        emitter.transform.SetParent(parent, false);
        emitter.transform.position = pos;
        emitter.transform.rotation = rot;
        emitter.transform.localScale = new Vector3(0.025f, 0.015f, 0.025f);
        Object.DestroyImmediate(emitter.GetComponent<Collider>());

        Material emitterMat = CreateUrpLitMaterial(name + "_Mat", new Color(0.9f, 0.15f, 0.15f), 0.3f, 0.6f, new Color(0.9f, 0.1f, 0.1f) * 1.5f);
        emitter.GetComponent<Renderer>().sharedMaterial = emitterMat;

        LaserScanner scanner = emitter.AddComponent<LaserScanner>();
        scanner.mode = mode;
        scanner.maxLength = maxLength;
        scanner.sweepAngle = sweepAngle;
        scanner.sweepSpeed = sweepSpeed;
        scanner.phaseOffset = phaseOffset;
        scanner.beamWidth = 0.008f;
        scanner.laserDamage = 18f;
    }

    static void CreateSecurityTerminal(Vector3 pos, RoomPortal exitPortal)
    {
        GameObject termRoot = new GameObject("Security_Terminal_Console");
        termRoot.transform.position = pos;

        GameObject deskPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetStorage/itemInRoom/BanThiNghiem.glb");
        if (deskPrefab != null)
        {
            GameObject desk = (GameObject)PrefabUtility.InstantiatePrefab(deskPrefab);
            desk.name = "DeskModel";
            desk.transform.SetParent(termRoot.transform, false);
            desk.transform.localPosition = Vector3.zero;
            desk.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            desk.transform.localScale = Vector3.one;
            EnsureMaterialsAreUrp(desk);
            NormalizeModelToHeight(desk, 0.105f, pos.y);
        }
        else
        {
            Material deskMat = CreateUrpLitMaterial("TerminalDeskMat", new Color(0.22f, 0.26f, 0.32f));
            CreateBox(termRoot.transform, "DeskBox", new Vector3(0f, 0.055f, 0f), new Vector3(0.18f, 0.11f, 0.10f), deskMat, false);
        }

        // Màn hình phát sáng trên bàn điều khiển
        Material screenMat = CreateUrpLitMaterial("TerminalScreenMat", new Color(1f, 0.25f, 0.2f), 0.1f, 0.9f, new Color(1f, 0.25f, 0.2f) * 2.2f);
        GameObject screen = CreateBox(termRoot.transform, "HoloScreen", new Vector3(0f, 0.135f, 0f), new Vector3(0.12f, 0.07f, 0.012f), screenMat, false);

        GameObject lightObj = new GameObject("TerminalLight");
        lightObj.transform.SetParent(termRoot.transform, false);
        lightObj.transform.localPosition = new Vector3(0f, 0.18f, -0.04f);
        Light l = lightObj.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 0.7f;
        l.intensity = 1.6f;
        l.color = new Color(1f, 0.25f, 0.2f);

        SecurityTerminal terminal = termRoot.AddComponent<SecurityTerminal>();
        terminal.screenRenderer = screen.GetComponent<Renderer>();
        terminal.terminalLight = l;
        terminal.exitPortal = exitPortal;
        terminal.interactRadius = 0.25f;
        terminal.hackDuration = 1.5f;
    }

    static RoomPortal CreateExitPortal(Vector3 pos)
    {
        GameObject doorRoot = new GameObject("Exit_Security_Door");
        doorRoot.transform.position = pos;

        Material frameMat = CreateUrpLitMaterial("DoorFrameMat", new Color(0.28f, 0.30f, 0.35f), 0.7f, 0.5f);
        Material statusMat = CreateUrpLitMaterial("DoorStatusMat", new Color(1f, 0.22f, 0.22f), 0.1f, 0.9f, new Color(1f, 0.22f, 0.22f) * 2.0f);

        CreateBox(doorRoot.transform, "PillarLeft", new Vector3(-0.11f, 0.12f, 0f), new Vector3(0.03f, 0.24f, 0.04f), frameMat, false);
        CreateBox(doorRoot.transform, "PillarRight", new Vector3(0.11f, 0.12f, 0f), new Vector3(0.03f, 0.24f, 0.04f), frameMat, false);
        CreateBox(doorRoot.transform, "Header", new Vector3(0f, 0.245f, 0f), new Vector3(0.25f, 0.03f, 0.04f), frameMat, false);

        GameObject energyField = CreateBox(doorRoot.transform, "DoorStatusPanel", new Vector3(0f, 0.12f, 0f), new Vector3(0.18f, 0.20f, 0.01f), statusMat, false);

        GameObject lightObj = new GameObject("DoorLight");
        lightObj.transform.SetParent(doorRoot.transform, false);
        lightObj.transform.localPosition = new Vector3(0f, 0.16f, -0.05f);
        Light dl = lightObj.AddComponent<Light>();
        dl.type = LightType.Point;
        dl.range = 0.65f;
        dl.intensity = 1.5f;

        RoomPortal portal = doorRoot.AddComponent<RoomPortal>();
        portal.targetSceneName = "SampleScene";
        portal.portalLabel = "Khu Tiếp Theo (SampleScene)";
        portal.isUnlocked = false;
        portal.interactRadius = 0.22f;
        portal.doorLightRenderer = energyField.GetComponent<Renderer>();
        portal.doorPointLight = dl;

        return portal;
    }

    static void SpawnDroneInstance(GameObject prefab, string name, Vector3 pos, float territoryRadius, GameObject dropPrefab)
    {
        if (prefab == null) return;
        GameObject d = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        d.name = name;
        d.transform.position = pos;

        SecurityDroneAI ai = d.GetComponent<SecurityDroneAI>();
        if (ai != null)
        {
            ai.territoryCenter = pos;
            ai.territoryRadius = territoryRadius;
            ai.dropItemPrefab = dropPrefab;
        }
    }

    static GameObject CreatePlayerInScene(Vector3 pos)
    {
        GameObject playerModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/character_V2/tripo_convert_b0f0bc7c-8966-417d-8980-40dfb7dbabb6@Idle.fbx");
        GameObject playerObj;
        if (playerModel != null)
        {
            playerObj = (GameObject)PrefabUtility.InstantiatePrefab(playerModel);
            playerObj.name = "player_v2";
            EnsureMaterialsAreUrp(playerObj);
        }
        else
        {
            playerObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerObj.name = "player_v2";
            playerObj.transform.localScale = Vector3.one * 0.04f;
        }

        playerObj.tag = "Player";
        playerObj.transform.position = pos;
        playerObj.transform.localScale = Vector3.one;

        // Chuẩn hóa chiều cao nhân vật (~0.078m) và đặt bàn chân chạm chuẩn mặt sàn y = 0.046f
        NormalizeModelToHeight(playerObj, 0.078f, 0.046f);

        Animator anim = playerObj.GetComponentInChildren<Animator>(true);
        if (anim != null)
        {
            RuntimeAnimatorController ac = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/character_V2/character_v2_anmator.controller");
            if (ac != null) anim.runtimeAnimatorController = ac;
            anim.applyRootMotion = false;
        }

        PlayerController pc = playerObj.AddComponent<PlayerController>();
        pc.limitToRoom = true;
        pc.useCustomRoomBounds = true;
        pc.minX = -0.58f;
        pc.maxX = 0.58f;
        pc.minZ = -0.48f;
        pc.maxZ = 3.16f;
        pc.lockToGroundY = true;
        pc.groundY = playerObj.transform.position.y;
        pc.animator = anim;

        playerObj.AddComponent<PlayerHealth>();
        playerObj.AddComponent<InventoryManager>();

        return playerObj;
    }

    static void CreateFollowCamera(Transform target)
    {
        GameObject camObj = new GameObject("Main Camera");
        camObj.tag = "MainCamera";
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.08f, 0.12f, 1f);
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 50f;
        cam.fieldOfView = 50f;
        camObj.AddComponent<AudioListener>();

        // Góc nhìn 2.5D Diorama Cutaway cao thoáng, nhìn rõ nhân vật và toàn bộ hành lang Laser phía trước
        Vector3 camOffset = new Vector3(0.26f, 0.32f, -0.38f);
        Vector3 camEuler = new Vector3(32f, -24f, 0f);
        Vector3 startTargetPos = target != null ? target.position : new Vector3(0f, 0.048f, -0.32f);

        camObj.transform.position = startTargetPos + camOffset;
        camObj.transform.rotation = Quaternion.Euler(camEuler);

        CameraFollow25D follow = camObj.AddComponent<CameraFollow25D>();
        follow.target = target;
        follow.offset = camOffset;
        follow.fixedRotation = camEuler;
        follow.lockCurrentSceneView = false;
        follow.smoothSpeed = 0.06f;
    }

    static void EnsureSceneInBuildSettings(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool exists = false;
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path == scenePath)
            {
                scenes[i].enabled = true;
                exists = true;
                break;
            }
        }
        if (!exists)
        {
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
