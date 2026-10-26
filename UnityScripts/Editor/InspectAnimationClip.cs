using UnityEngine;
using UnityEditor;

public class InspectAnimationClip
{
    [MenuItem("Tools/Kiem tra doan dau cua Standard Walk")]
    public static void InspectWalk()
    {
        string path = "Assets/character_V2/Standard Walk.fbx";
        Object[] objs = AssetDatabase.LoadAllAssetsAtPath(path);
        AnimationClip clip = null;
        foreach (var obj in objs)
        {
            if (obj is AnimationClip c && !c.name.Contains("__preview__"))
            {
                clip = c;
                break;
            }
        }

        if (clip != null)
        {
            Debug.Log($"[InspectWalk] Clip: {clip.name}, Length: {clip.length}s, FrameRate: {clip.frameRate}, TotalFrames: {clip.length * clip.frameRate}");
            EditorUtility.DisplayDialog("Clip Info", $"Clip: {clip.name}\nThời lượng: {clip.length:F2}s\nFrame Rate: {clip.frameRate}\nTổng số frame: {clip.length * clip.frameRate:F0}", "OK");
        }
        else
        {
            Debug.LogError("Không tìm thấy clip animation trong Standard Walk.fbx");
        }
    }
}
