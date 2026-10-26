using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public class InspectAnimator
{
    [MenuItem("Tools/Kiem tra toan bo Animator Controller")]
    public static void Inspect()
    {
        string path = "Assets/character_V2/character_v2_anmator.controller";
        AnimatorController ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (ac == null)
        {
            Debug.LogError("Không tìm thấy controller tại " + path);
            return;
        }

        Debug.Log($"=== THONG TIN ANIMATOR: {ac.name} ===");
        var rootSm = ac.layers[0].stateMachine;
        Debug.Log($"Default State: {rootSm.defaultState.name}");

        foreach (var state in rootSm.states)
        {
            var s = state.state;
            Debug.Log($"State [{s.name}] - Motion: {(s.motion != null ? s.motion.name : "NULL")}, Speed: {s.speed}");
            foreach (var t in s.transitions)
            {
                string conds = "";
                foreach (var c in t.conditions) conds += $"{c.parameter} {c.mode} {c.threshold}; ";
                Debug.Log($"   -> Transition to [{t.destinationState?.name}]: HasExitTime={t.hasExitTime}, Duration={t.duration}, Offset={t.offset}, ExitTime={t.exitTime}, Conds=[{conds}]");
            }
        }

        foreach (var t in rootSm.anyStateTransitions)
        {
            string conds = "";
            foreach (var c in t.conditions) conds += $"{c.parameter} {c.mode} {c.threshold}; ";
            Debug.Log($"AnyState -> [{t.destinationState?.name}]: HasExitTime={t.hasExitTime}, Duration={t.duration}, Offset={t.offset}, Conds=[{conds}]");
        }
    }
}
