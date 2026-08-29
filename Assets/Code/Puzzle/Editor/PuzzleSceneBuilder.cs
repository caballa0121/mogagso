using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 퍼즐 씬을 자동으로 만들어 주는 에디터 도구.
///
/// Tools → 퍼즐 → [퍼즐 씬 만들기] 를 누르면
/// Assets/Scenes/PuzzleScene.unity 가 배선까지 끝난 채로 만들어집니다.
///
/// 이 씬은 다른 씬 '위에 덧씌워' 열리기 때문에 일부러
/// 카메라와 AudioListener를 넣지 않습니다. (밑 씬의 것과 부딪히지 않게)
/// </summary>
public static class PuzzleSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/PuzzleScene.unity";
    private const string KoreanFontPath = "Assets/2.asset";

    [MenuItem("Tools/퍼즐/퍼즐 씬 만들기", false, 20)]
    public static void BuildScene()
    {
        if (File.Exists(ScenePath))
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "퍼즐 씬 다시 만들기",
                ScenePath + " 가 이미 있습니다.\n\n덮어쓰면 그 씬에 직접 해둔 수정은 사라집니다.\n덮어쓸까요?",
                "덮어쓰기", "취소");
            if (!overwrite) return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        TMP_FontAsset font = FindKoreanFont();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── 캔버스 (카메라 없이 그려지는 Overlay) ──────────────
        var canvasGO = new GameObject("Puzzle Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // 밑에 깔린 씬의 UI보다 위에 오도록

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 밑 씬을 가리는 불투명 배경
        var backdrop = CreateStretchImage(canvasGO.transform, "Backdrop", new Color(0.07f, 0.07f, 0.10f, 1f));

        // 조각이 올라갈 판
        var boardGO = new GameObject("Board", typeof(RectTransform));
        boardGO.transform.SetParent(canvasGO.transform, false);
        var board = boardGO.GetComponent<RectTransform>();
        board.anchorMin = board.anchorMax = board.pivot = new Vector2(0.5f, 0.5f);
        board.anchoredPosition = Vector2.zero;
        board.sizeDelta = new Vector2(1920f, 1080f);

        // 완성본 (판의 자식이라 판과 같이 크기가 맞춰집니다)
        var completedGO = new GameObject("CompletedImage", typeof(RectTransform));
        completedGO.transform.SetParent(board, false);
        var completedRect = completedGO.GetComponent<RectTransform>();
        completedRect.anchorMin = completedRect.anchorMax = completedRect.pivot = new Vector2(0.5f, 0.5f);
        completedRect.anchoredPosition = Vector2.zero;
        completedRect.sizeDelta = new Vector2(1920f, 1080f);
        var completedImage = completedGO.AddComponent<Image>();
        completedImage.raycastTarget = false;
        completedImage.color = new Color(1f, 1f, 1f, 0f);
        completedImage.enabled = false;

        // 안내 문구
        var hintGO = new GameObject("HintText", typeof(RectTransform));
        hintGO.transform.SetParent(canvasGO.transform, false);
        var hintRect = hintGO.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.5f, 0f);
        hintRect.anchorMax = new Vector2(0.5f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.anchoredPosition = new Vector2(0f, 60f);
        hintRect.sizeDelta = new Vector2(1400f, 70f);
        var hintText = hintGO.AddComponent<TextMeshProUGUI>();
        if (font != null) hintText.font = font;
        hintText.fontSize = 44f;
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.raycastTarget = false;
        hintText.color = new Color(1f, 1f, 1f, 0.9f);
        hintText.text = "";

        // 들고 날 때 쓰는 검은 판 (제일 마지막 자식 = 제일 위)
        var fade = CreateStretchImage(canvasGO.transform, "Fade", Color.black);

        // ── 매니저 ─────────────────────────────────────────────
        var mgrGO = new GameObject("PuzzleManager");
        var mgr = mgrGO.AddComponent<PuzzleManager>();
        mgr.canvas = canvas;
        mgr.backdrop = backdrop;
        mgr.board = board;
        mgr.completedImage = completedImage;
        mgr.hintText = hintText;
        mgr.fadeImage = fade;

        // ── 저장 & 빌드 세팅 등록 ──────────────────────────────
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        RegisterInBuildSettings();

        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("퍼즐 씬 완성",
            "PuzzleScene을 만들고 Build Settings에 등록했습니다.\n\n" +
            "이 씬은 다른 씬 위에 덧씌워 열리므로 카메라가 없는 게 정상입니다.\n\n" +
            "다음 순서로 쓰시면 됩니다.\n" +
            "1. Assets/image/puzzle 에 조각 이미지를 넣기\n" +
            "2. Tools → 퍼즐 → [조각 이미지 재단] 실행\n" +
            "3. 챕터 씬의 Director → 원하는 스텝의 [3.5 퍼즐] 칸에\n" +
            "   만들어진 PuzzleDefinition을 끌어다 넣기",
            "확인");
    }

    // ───────────────────────────────────────────────────────────

    private static Image CreateStretchImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        return img;
    }

    private static TMP_FontAsset FindKoreanFont()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
        if (font != null) return font;

        foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("LiberationSans")) continue;

            var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (f != null) return f;
        }

        Debug.LogWarning("[퍼즐] 한글이 들어간 TMP 폰트를 찾지 못했습니다. 안내 문구가 네모로 보일 수 있습니다.");
        return null;
    }

    private static void RegisterInBuildSettings()
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        foreach (var s in list)
        {
            if (s.path == ScenePath)
            {
                s.enabled = true;
                EditorBuildSettings.scenes = list.ToArray();
                return;
            }
        }

        list.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
