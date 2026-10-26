using UnityEngine;
using UnityEditor;

public class FixAnimationLoops
{
    [MenuItem("Tools/Sua loi luot nhan vat (Bat Loop Time)")]
    public static void EnableLoopTimes()
    {
        string[] loopFbxNames = new string[]
        {
            "Running.fbx",
            "Running (1).fbx",
            "Standard Walk.fbx",
            "tripo_convert_b0f0bc7c-8966-417d-8980-40dfb7dbabb6@Idle.fbx"
        };

        int count = 0;
        foreach (string name in loopFbxNames)
        {
            string path = "Assets/character_V2/" + name;
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null)
            {
                ModelImporterClipAnimation[] clips = importer.clipAnimations;
                if (clips == null || clips.Length == 0)
                {
                    clips = importer.defaultClipAnimations;
                }

                if (clips != null && clips.Length > 0)
                {
                    foreach (var clip in clips)
                    {
                        clip.loopTime = true;
                        clip.loopPose = true;
                    }
                    importer.clipAnimations = clips;
                    importer.SaveAndReimport();
                    count++;
                    Debug.Log($"[FixAnimationLoops] Đã bật Loop Time thành công cho: {name}");
                }
            }
        }

        EditorUtility.DisplayDialog("Hoàn tất!", $"Đã tự động bật Loop Time cho {count} file Animation (Running, Walk, Idle)!", "OK");
    }
}
