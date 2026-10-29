using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Công cụ đóng gói toàn bộ bài làm thành file .unitypackage chuẩn chỉnh để gửi cho nhóm:
/// - Tự động gom SampleScene + toàn bộ Script C# + thư mục Assets/Item (.glb) + Assets/Editor + Nhân vật + Quái vật.
/// - Tự động loại bỏ thư mục trùng lặp Assets/Resources/ItemModels (giúp giảm ngay hơn 1.08 GB dung lượng thừa).
/// - Tự động xuất kèm file Packages/manifest.json (chứa thư viện đọc .glb glTFast) và file hướng dẫn cài đặt.
/// </summary>
public static class ExportProjectPackage
{
    private const string DefaultExportFolder = @"C:\Users\vuong\OneDrive\Documents\BE_Doan";

    [MenuItem("Tools/1. Xuat UnityPackage DAY DU (Full Scene + Phong + Nhan Vat + Item)")]
    public static void ExportFullPackage()
    {
        ExportPackageInternal(includeRoomAssetStorage: true);
    }

    [MenuItem("Tools/2. Xuat UnityPackage NHE (Bo qua AssetStorage neu ban be da co san Phong)")]
    public static void ExportLightweightPackage()
    {
        ExportPackageInternal(includeRoomAssetStorage: false);
    }

    private static void ExportPackageInternal(bool includeRoomAssetStorage)
    {
        // Lưu Scene hiện tại trước khi xuất để đảm bảo không sót thay đổi nào
        if (!Application.isPlaying)
        {
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
        }

        string defaultFileName = includeRoomAssetStorage
            ? "ProjectShrink_Full_Scene_Items_Gameplay.unitypackage"
            : "ProjectShrink_Update_Items_Gameplay_NoRoom.unitypackage";

        string initialDir = Directory.Exists(DefaultExportFolder)
            ? DefaultExportFolder
            : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        string savePath = EditorUtility.SaveFilePanel(
            includeRoomAssetStorage
                ? "Lưu gói UnityPackage Đầy Đủ (Có cả Phòng AssetStorage)"
                : "Lưu gói UnityPackage Nhẹ (Chỉ Scene, Nhân Vật, Quái, Item & Script)",
            initialDir,
            defaultFileName,
            "unitypackage"
        );

        if (string.IsNullOrEmpty(savePath)) return;

        try
        {
            EditorUtility.DisplayProgressBar("Đang thu thập tài nguyên...", "Đang quét Scene, Script và Model 3D...", 0.2f);

            HashSet<string> assetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Lấy toàn bộ phụ thuộc của SampleScene.unity
            string scenePath = "Assets/Scenes/SampleScene.unity";
            if (File.Exists(scenePath))
            {
                assetPaths.Add(scenePath);
                string[] sceneDeps = AssetDatabase.GetDependencies(scenePath, true);
                for (int i = 0; i < sceneDeps.Length; i++)
                {
                    AddIfValid(assetPaths, sceneDeps[i], includeRoomAssetStorage);
                }
            }

            // 2. Bắt buộc lấy các thư mục quan trọng (đặc biệt là Assets/Item và Assets/Editor không gắn trực tiếp trên Scene)
            string[] requiredFolders = new string[]
            {
                "Assets/Item",
                "Assets/Editor",
                "Assets/Prefabs",
                "Assets/Resources/ItemIcons",
                "Assets/character_V2",
                "Assets/enemy côn trùng",
                "Assets/Blink",
                "Assets/mate",
                "Assets/Scenes",
                "Assets/Settings",
                "Assets/UnityScripts"
            };

            for (int f = 0; f < requiredFolders.Length; f++)
            {
                string folder = requiredFolders[f];
                if (!AssetDatabase.IsValidFolder(folder)) continue;

                string[] guids = AssetDatabase.FindAssets("", new[] { folder });
                for (int i = 0; i < guids.Length; i++)
                {
                    string p = AssetDatabase.GUIDToAssetPath(guids[i]);
                    AddIfValid(assetPaths, p, includeRoomAssetStorage);
                }
            }

            // 3. Lấy toàn bộ file rời nằm ngay trong thư mục gốc Assets/ (.cs, .fbx, .obj, .mtl, .png, .jpg, .controller, .inputactions)
            string[] rootFiles = Directory.GetFiles("Assets");
            for (int i = 0; i < rootFiles.Length; i++)
            {
                string p = rootFiles[i].Replace('\\', '/');
                if (p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                AddIfValid(assetPaths, p, includeRoomAssetStorage);
            }

            List<string> finalList = new List<string>(assetPaths);
            finalList.Sort(StringComparer.OrdinalIgnoreCase);

            EditorUtility.DisplayProgressBar(
                "Đang đóng gói .unitypackage...",
                $"Đang nén {finalList.Count} file tài nguyên (Vui lòng đợi 1-2 phút)...",
                0.6f
            );

            AssetDatabase.ExportPackage(finalList.ToArray(), savePath, ExportPackageOptions.Default);

            // 4. Sao chép kèm file Packages/manifest.json và tạo file Hướng Dẫn ngay cạnh file .unitypackage
            string outDir = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(outDir))
            {
                string manifestSrc = "Packages/manifest.json";
                if (File.Exists(manifestSrc))
                {
                    File.Copy(manifestSrc, Path.Combine(outDir, "manifest.json"), true);
                }

                string guidePath = Path.Combine(outDir, "HUONG_DAN_IMPORT_CHO_NHOM.txt");
                File.WriteAllText(guidePath, BuildGuideText(Path.GetFileName(savePath), includeRoomAssetStorage));
            }

            EditorUtility.ClearProgressBar();

            FileInfo fi = new FileInfo(savePath);
            float sizeMb = fi.Exists ? (fi.Length / (1024f * 1024f)) : 0f;

            Debug.Log($"✅ [ExportProjectPackage] Đã xuất thành công: {savePath} ({sizeMb:F1} MB, gồm {finalList.Count} assets).");
            EditorUtility.DisplayDialog(
                "Xuất UnityPackage Thành Công!",
                $"Đã đóng gói đầy đủ bài làm ra file:\n{savePath}\n\n" +
                $"• Dung lượng: {sizeMb:F1} MB ({finalList.Count} files)\n" +
                $"• Đã kèm: SampleScene + Toàn bộ Script C# + Assets/Item (.glb) + Assets/Editor + Nhân Vật + Quái Vật\n" +
                $"• Đã xuất kèm: manifest.json & HUONG_DAN_IMPORT_CHO_NHOM.txt cùng thư mục.",
                "Mở Thư Mục Chứa File"
            );

            EditorUtility.RevealInFinder(savePath);
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError("[ExportProjectPackage] Lỗi khi xuất package: " + ex);
            EditorUtility.DisplayDialog("Lỗi Xuất Package", ex.Message, "Đóng");
        }
    }

    private static void AddIfValid(HashSet<string> set, string assetPath, bool includeRoomAssetStorage)
    {
        if (string.IsNullOrEmpty(assetPath)) return;
        assetPath = assetPath.Replace('\\', '/');

        // Chỉ xuất các tài nguyên nằm trong Assets/
        if (!assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) return;

        // Bỏ qua thư mục bản sao trùng lặp Resources/ItemModels (tiết kiệm 1.08 GB vì đã có Assets/Item)
        if (assetPath.StartsWith("Assets/Resources/ItemModels/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(assetPath, "Assets/Resources/ItemModels", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Nếu chọn gói nhẹ (bạn bè đã có sẵn căn phòng AssetStorage) thì bỏ qua Assets/AssetStorage (tiết kiệm 2.28 GB)
        if (!includeRoomAssetStorage)
        {
            if (assetPath.StartsWith("Assets/AssetStorage/", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(assetPath, "Assets/AssetStorage", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        if (Directory.Exists(assetPath)) return;
        if (!File.Exists(assetPath)) return;

        set.Add(assetPath);
    }

    private static string BuildGuideText(string packageFileName, bool includeRoomAssetStorage)
    {
        return
@"================================================================================
HƯỚNG DẪN NHẬN BÀI & CHẠY DỰ ÁN (PROJECT SHRINK: ESCAPE FROM BLACK CORE)
================================================================================

PHẦN 1: CÀI ĐẶT TRONG UNITY
---------------------------
Bước 1: Copy đè file `manifest.json` (đi kèm trong thư mục này) vào thư mục `Packages/manifest.json`
        của Project Unity để Unity tự cài thư viện đọc model 3D `.glb` (`com.unity.cloud.gltfast`).
Bước 2: Mở Project Unity -> Chọn menu `Assets -> Import Package -> Custom Package...`
        -> Chọn file `" + packageFileName + @"` -> Bấm `All` -> Bấm `Import`.
Bước 3: Mở Scene tại đường dẫn `Assets/Scenes/SampleScene.unity` và bấm Play!
" + (includeRoomAssetStorage ? "" : "\n(Lưu ý: Gói nhẹ này dành cho máy đã có sẵn thư mục phòng `Assets/AssetStorage`).\n") + @"
PHẦN 2: CHẠY BACKEND API & WEB ADMIN (KẾT NỐI TRỰC TIẾP VỚI GAME)
-----------------------------------------------------------------
Bước 1: Lấy code nhánh `integrated-gameplay` từ GitHub:
        git fetch origin
        git checkout integrated-gameplay
        git pull origin integrated-gameplay

Bước 2: Bật Docker Database (PostgreSQL, MongoDB, Redis):
        docker-compose up -d

Bước 3: Chạy Backend Server & Nạp dữ liệu mẫu (Cổng 3000):
        npm install
        npm run seed
        npm start

Bước 4: Chạy Web Admin để chỉnh thông số Nhân Vật, Quái Vật, Vũ Khí, Công Thức (Cổng 5173):
        cd AdminWeb
        npm install
        npm run dev

- Truy cập Web Admin tại: http://localhost:5173
- Khi sửa thông số trên Web Admin và bấm Lưu, Game Unity đang chạy sẽ tự động cập nhật sau 4 giây
  (hoặc bấm phím F5 trong Game để cập nhật ngay lập tức).
================================================================================
";
    }
}
