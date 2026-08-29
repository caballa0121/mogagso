using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Canvas가 화면 크기에 맞춰 늘어나도록 고쳐 주는 도구.
///
/// Canvas Scaler가 'Constant Pixel Size'면 UI가 화면 크기를 무시하고
/// 픽셀 좌표를 그대로 씁니다. 그래서 에디터에서 맞춰둔 자리가
/// 다른 해상도로 빌드하면 화면 밖으로 밀려납니다.
///
/// 'Scale With Screen Size'로 바꾸고 기준 해상도를 1920x1080으로 맞춥니다.
/// </summary>
public static class CanvasScalerFixer
{
    private static readonly Vector2 Reference = new Vector2(1920f, 1080f);
    // 💥 Match = 0 (Width 기준)
    //    화면 비율이 어떻든 논리 캔버스 폭이 항상 1920으로 고정됩니다.
    //    0.5로 두면 16:10 같은 비율에서 캔버스가 좌우로 좁아져 UI가 화면 밖으로 잘립니다.
    private const float Match = 0f;

    [MenuItem("Tools/UI/현재 씬의 Canvas 해상도 대응 고치기", false, 10)]
    public static void FixCurrentScene()
    {
        int changed = FixScene(SceneManager.GetActiveScene(), out string report);

        if (changed > 0) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Canvas 해상도 대응",
            report + (changed > 0 ? "\nCtrl+S로 저장해 주세요." : ""), "확인");
    }

    [MenuItem("Tools/UI/빌드 목록의 모든 씬 고치기", false, 11)]
    public static void FixAllBuildScenes()
    {
        bool ok = EditorUtility.DisplayDialog("Canvas 해상도 대응",
            "Build Settings에 등록된 모든 씬을 열어서 Canvas Scaler를\n" +
            "Scale With Screen Size (1920x1080) 로 바꾸고 저장합니다.\n\n" +
            "되돌리기가 안 되니 먼저 백업해 두시는 걸 권합니다.\n\n계속할까요?",
            "진행", "취소");
        if (!ok) return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string originalScene = SceneManager.GetActiveScene().path;
        var lines = new List<string>();
        int total = 0;

        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled) continue;
            if (!System.IO.File.Exists(entry.path)) continue;

            Scene scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
            int n = FixScene(scene, out string report);

            if (n > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                total += n;
            }

            lines.Add(System.IO.Path.GetFileNameWithoutExtension(entry.path) + " : " + report.Replace("\n", " "));
        }

        // 원래 보던 씬으로 되돌립니다.
        if (!string.IsNullOrEmpty(originalScene) && System.IO.File.Exists(originalScene))
        {
            EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
        }

        string msg = $"고친 Canvas : 모두 {total}개\n\n";
        foreach (var l in lines) msg += "  " + l + "\n";

        Debug.Log("[Canvas 해상도 대응] " + msg.Replace("\n", " / "));
        EditorUtility.DisplayDialog("Canvas 해상도 대응", msg, "확인");
    }

    // ───────────────────────────────────────────────────────────

    private static int FixScene(Scene scene, out string report)
    {
        var scalers = new List<CanvasScaler>();

        foreach (var root in scene.GetRootGameObjects())
        {
            scalers.AddRange(root.GetComponentsInChildren<CanvasScaler>(true));
        }

        if (scalers.Count == 0)
        {
            report = "Canvas 없음";
            return 0;
        }

        int changed = 0;
        int alreadyOk = 0;
        int skipped = 0;

        foreach (var scaler in scalers)
        {
            var canvas = scaler.GetComponent<Canvas>();

            // 월드 공간 캔버스는 화면 크기와 무관하므로 건드리지 않습니다.
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace) { skipped++; continue; }

            bool needsFix =
                scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize ||
                scaler.referenceResolution != Reference ||
                !Mathf.Approximately(scaler.matchWidthOrHeight, Match) ||
                scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            if (!needsFix) { alreadyOk++; continue; }

            Undo.RecordObject(scaler, "Canvas 해상도 대응");

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = Match;

            EditorUtility.SetDirty(scaler);
            changed++;
        }

        report = $"고침 {changed}개 / 이미 정상 {alreadyOk}개" + (skipped > 0 ? $" / 월드캔버스 건너뜀 {skipped}개" : "");
        return changed;
    }
}
