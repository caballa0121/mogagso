using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 빈 챕터 씬을 만들어 주는 도구.
///
/// 카메라 / 페이드(ScreenFader) / 연출용 Director(AutoDirector) 가 배선된
/// 최소 구성의 챕터 씬을 만들고 Build Settings에 등록합니다.
///
/// 대사 UI(DialoguePanel, 초상화 등)는 들어있지 않습니다.
/// 대사가 필요해지면 CHAPTER 2의 Dialogue 관련 오브젝트를 복사해 붙이시면 됩니다.
/// </summary>
public static class ChapterSceneBuilder
{
    private const string TargetScenePath = "Assets/Scenes/CHAPTER 3.unity";

    [MenuItem("Tools/씬/빈 CHAPTER 3 만들기", false, 10)]
    public static void BuildChapter3()
    {
        if (File.Exists(TargetScenePath))
        {
            EditorUtility.DisplayDialog("챕터 씬",
                TargetScenePath + " 가 이미 있습니다.\n\n덮어쓰지 않고 그대로 두겠습니다.", "확인");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── 카메라 ─────────────────────────────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.position = new Vector3(0f, 0f, -10f);

        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGO.AddComponent<AudioListener>();

        // ── 캔버스 + 페이드 ────────────────────────────────────
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var fadeGO = new GameObject("ScreenFader", typeof(RectTransform));
        fadeGO.transform.SetParent(canvasGO.transform, false);

        var fadeRect = fadeGO.GetComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;

        var fadeImg = fadeGO.AddComponent<Image>();
        fadeImg.color = Color.black;
        fadeImg.raycastTarget = false;

        var fader = fadeGO.AddComponent<ScreenFader>();
        fader.fadeColor = Color.black;
        fader.fadeInOnStart = true;
        fader.fadeInDuration = 0.6f;
        fader.fadeOutDuration = 0.6f;

        // ── 연출 Director ──────────────────────────────────────
        // 시퀀스는 비어 있습니다. 인스펙터에서 스텝을 채워 넣으세요.
        var directorGO = new GameObject("Director");
        var director = directorGO.AddComponent<AutoDirector>();
        director.sequence = new SequenceStep[0];
        director.allBackgrounds = new List<GameObject>();

        // ── 저장 & 등록 ────────────────────────────────────────
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, TargetScenePath);
        RegisterInBuildSettings(TargetScenePath);

        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("챕터 씬 완성",
            "CHAPTER 3 씬을 만들고 Build Settings에 등록했습니다.\n\n" +
            "들어있는 것 : 카메라 / 페이드(ScreenFader) / 빈 Director\n\n" +
            "대사가 필요해지면 CHAPTER 2를 열어 Dialogue 관련 오브젝트를\n" +
            "복사(Ctrl+C)해서 여기에 붙여넣으시면 됩니다.",
            "확인");
    }

    /// <summary>
    /// 열려 있는 씬에 페이드(ScreenFader)를 넣어 줍니다.
    ///
    /// AutoDirector와 BattleManager는 씬을 넘길 때 ScreenFader를 찾아 씁니다.
    /// 없으면 경고만 남기고 페이드 없이 뚝 끊기며 넘어갑니다.
    /// CHAPTER 1 / CHAPTER 2 처럼 페이드가 없는 씬에 눌러 주세요.
    /// </summary>
    [MenuItem("Tools/씬/현재 씬에 페이드(ScreenFader) 추가", false, 11)]
    public static void AddFaderToOpenScene()
    {
        var existing = Object.FindAnyObjectByType<ScreenFader>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);

            EditorUtility.DisplayDialog("페이드",
                "이 씬에는 이미 ScreenFader가 있습니다.\n\n'" + existing.gameObject.name + "' 을(를) 골라 뒀습니다.", "확인");
            return;
        }

        // 씬에 있는 Overlay 캔버스를 찾아 그 안에 넣습니다. 없으면 새로 만듭니다.
        Canvas canvas = null;
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c.transform.parent == null) { canvas = c; break; }
        }

        if (canvas == null)
        {
            var canvasGO = new GameObject("Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            Undo.RegisterCreatedObjectUndo(canvasGO, "캔버스 추가");
        }

        var fadeGO = new GameObject("ScreenFader", typeof(RectTransform));
        fadeGO.transform.SetParent(canvas.transform, false);
        fadeGO.transform.SetAsLastSibling(); // 다른 UI 위를 덮도록

        var rect = fadeGO.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var img = fadeGO.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;

        var fader = fadeGO.AddComponent<ScreenFader>();
        fader.fadeColor = Color.black;
        fader.fadeInOnStart = true;
        fader.fadeInDuration = 0.6f;
        fader.fadeOutDuration = 0.6f;

        Undo.RegisterCreatedObjectUndo(fadeGO, "페이드 추가");
        EditorSceneManager.MarkSceneDirty(fadeGO.scene);

        Selection.activeGameObject = fadeGO;
        EditorGUIUtility.PingObject(fadeGO);

        EditorUtility.DisplayDialog("페이드",
            "'" + canvas.gameObject.name + "' 안에 ScreenFader를 넣었습니다.\n\n" +
            "이제 이 씬은 시작할 때 검은 화면에서 밝아지고,\n" +
            "다음 씬으로 넘어갈 때 어두워졌다 넘어갑니다.\n\n" +
            "Ctrl+S로 저장해 주세요.", "확인");
    }

    /// <summary>Build Settings 목록을 지금 씬 구성에 맞게 한 번에 정리합니다.</summary>
    [MenuItem("Tools/씬/Build Settings 순서 정리", false, 20)]
    public static void FixBuildSettings()
    {
        // 실제 진행 순서
        string[] wanted =
        {
            "Assets/Scenes/Intro.unity",
            "Assets/Scenes/CHAPTER 1.unity",
            "Assets/Scenes/Battle Scene.unity",
            "Assets/Scenes/NodeMap.unity",
            "Assets/Scenes/CHAPTER 2.unity",
            "Assets/Scenes/SampleScene.unity",
            "Assets/Scenes/CHAPTER 3.unity",
            "Assets/Scenes/PuzzleScene.unity", // 덧씌우기용 (직접 시작하지는 않음)
        };

        var list = new List<EditorBuildSettingsScene>();
        var added = new HashSet<string>();
        var missing = new List<string>();

        foreach (string path in wanted)
        {
            if (!File.Exists(path)) { missing.Add(path); continue; }

            list.Add(new EditorBuildSettingsScene(path, true));
            added.Add(path);
        }

        // 목록에 있던 나머지 씬(Test Scene 등)은 뒤로 밀어서 보존합니다.
        var leftover = new List<string>();
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (added.Contains(s.path)) continue;
            if (!File.Exists(s.path)) continue;

            list.Add(new EditorBuildSettingsScene(s.path, s.enabled));
            leftover.Add(s.path);
        }

        EditorBuildSettings.scenes = list.ToArray();

        string msg = "Build Settings를 진행 순서대로 정리했습니다.\n\n";
        for (int i = 0; i < list.Count; i++)
            msg += $"{i}. {Path.GetFileNameWithoutExtension(list[i].path)}\n";

        if (missing.Count > 0)
        {
            msg += "\n아직 없는 씬 (건너뜀) :\n";
            foreach (var m in missing) msg += "  " + Path.GetFileNameWithoutExtension(m) + "\n";
        }

        Debug.Log("[씬 정리] " + msg.Replace("\n", " / "));
        EditorUtility.DisplayDialog("Build Settings 정리", msg, "확인");
    }

    private static void RegisterInBuildSettings(string path)
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        foreach (var s in list)
        {
            if (s.path == path)
            {
                s.enabled = true;
                EditorBuildSettings.scenes = list.ToArray();
                return;
            }
        }

        list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
