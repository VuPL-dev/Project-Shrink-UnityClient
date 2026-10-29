using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class ExportDrivePackage
{
    [MenuItem("Tools/Dong Goi UnityPackage de Up Drive")]
    public static void ExportFullPackageForDrive()
    {
        // 1. Lưu toàn bộ Scene hiện tại trước khi đóng gói
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        // 2. Đường dẫn xuất file .unitypackage
        string outputDir = @"C:\Users\vuong\OneDrive\Documents\BE_Doan";
        if (!Directory.Exists(outputDir))
        {
            outputDir = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        string packagePath = Path.Combine(outputDir, "DO_AN_Character_Animations_FullScene.unitypackage");

        // 3. Danh sách toàn bộ các thư mục và file quan trọng (Model, Animation nhân vật, Quái, Scene, Code)
        string[] assetPaths = new string[]
        {
            "Assets/character_V2",
            "Assets/mate",
            "Assets/enemy côn trùng",
            "Assets/Prefabs",
            "Assets/Scenes",
            "Assets/Settings",
            "Assets/Editor",
            "Assets/health_syringe.obj",
            "Assets/health_syringe.mtl",
            "Assets/PlayerController.cs",
            "Assets/PlayerHealth.cs",
            "Assets/EnemyAI.cs",
            "Assets/ItemPickup.cs",
            "Assets/InventoryManager.cs"
        };

        EditorUtility.DisplayProgressBar("Đang đóng gói UnityPackage...", "Đang gom toàn bộ Model, Animation nhân vật, Quái vật và Scene...", 0.5f);
        try
        {
            AssetDatabase.ExportPackage(
                assetPaths,
                packagePath,
                ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies
            );
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log("✅ [EXPORT DRIVE] Đã đóng gói thành công tại: " + packagePath);

        if (EditorUtility.DisplayDialog(
            "Đóng Gói Hoàn Tất!",
            "Đã xuất file trọn bộ (Model + Animation nhân vật + Quái + Scene + Code) tại:\n\n" + packagePath + "\n\nBạn có muốn mở thư mục chứa file để kéo lên Google Drive ngay không?",
            "Mở Thư Mục Ngay",
            "Đóng"))
        {
            EditorUtility.RevealInFinder(packagePath);
        }
    }
}
