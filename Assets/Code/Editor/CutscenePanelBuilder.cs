using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// DialogueManager의 컷씬 UI를 만들어서 연결해 주는 도구.
///
/// DialogueManager에는 컷씬 기능(DialogueLine.cutsceneSprite)이 이미 구현돼 있지만,
/// 화면에 띄울 패널이 씬에 없으면
/// "Cutscene Canvas Group / Cutscene Image가 연결되지 않았습니다" 경고만 남기고 그냥 넘어갑니다.
///
/// 이 도구는 대화창이 들어있는 캔버스 안에 컷씬 패널을 만들고
/// DialogueManager의 빈 칸 두 개를 채워줍니다.
/// </summary>
public static class CutscenePanelBuilder
{
    private const string KoreanFontPath = "Assets/2.asset";
    private const string PanelName = "CutscenePanel";

    [MenuItem("Tools/대화/컷씬 UI 만들기", false, 10)]
    public static void BuildCutscenePanel()
    {
        var dm = Object.FindAnyObjectByType<DialogueManager>();
        if (dm == null)
        {
            EditorUtility.DisplayDialog("컷씬 UI",
                "열려 있는 씬에서 DialogueManager를 찾지 못했습니다.\n\n" +
                "대화가 들어있는 씬(SampleScene 등)을 열고 다시 눌러주세요.", "확인");
            return;
        }

        // 이미 연결돼 있으면 건드리지 않습니다.
        if (dm.cutsceneCanvasGroup != null && dm.cutsceneImage != null)
        {
            Selection.activeGameObject = dm.cutsceneCanvasGroup.gameObject;
            EditorGUIUtility.PingObject(dm.cutsceneCanvasGroup.gameObject);

            EditorUtility.DisplayDialog("컷씬 UI",
                "이미 컷씬 UI가 연결돼 있습니다.\n\n'" + dm.cutsceneCanvasGroup.gameObject.name + "' 을(를) 골라 뒀습니다.", "확인");
            return;
        }

        // 대화창이 올라가 있는 캔버스를 찾습니다.
        Canvas canvas = FindDialogueCanvas(dm);
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("컷씬 UI",
                "대화 UI가 들어있는 Canvas를 찾지 못했습니다.\n\n" +
                "DialogueManager의 Dialogue Box Canvas Group이 연결돼 있는지 확인해 주세요.", "확인");
            return;
        }

        TMP_FontAsset font = FindKoreanFont();

        // ── 컷씬 패널 ──────────────────────────────────────────
        var panel = new GameObject(PanelName, typeof(RectTransform));
        panel.transform.SetParent(canvas.transform, false);
        Stretch(panel.GetComponent<RectTransform>());

        var group = panel.AddComponent<CanvasGroup>();
        group.alpha = 0f;              // 평소에는 완전히 투명
        group.blocksRaycasts = false;
        group.interactable = false;

        // 뒤를 가리는 어두운 막
        var dim = new GameObject("Dim", typeof(RectTransform));
        dim.transform.SetParent(panel.transform, false);
        Stretch(dim.GetComponent<RectTransform>());

        var dimImage = dim.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.88f);
        dimImage.raycastTarget = false;

        // 컷씬 그림
        var art = new GameObject("CutsceneImage", typeof(RectTransform));
        art.transform.SetParent(panel.transform, false);

        var artRect = art.GetComponent<RectTransform>();
        artRect.anchorMin = new Vector2(0.5f, 0.5f);
        artRect.anchorMax = new Vector2(0.5f, 0.5f);
        artRect.pivot = new Vector2(0.5f, 0.5f);
        artRect.anchoredPosition = new Vector2(0f, 40f);
        artRect.sizeDelta = new Vector2(1500f, 850f);

        var artImage = art.AddComponent<Image>();
        artImage.preserveAspect = true;   // 그림 비율이 찌그러지지 않게
        artImage.raycastTarget = false;
        artImage.color = Color.white;

        // 안내 문구
        var hint = new GameObject("Hint", typeof(RectTransform));
        hint.transform.SetParent(panel.transform, false);

        var hintRect = hint.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.5f, 0f);
        hintRect.anchorMax = new Vector2(0.5f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.anchoredPosition = new Vector2(0f, 50f);
        hintRect.sizeDelta = new Vector2(1200f, 60f);

        var hintText = hint.AddComponent<TextMeshProUGUI>();
        if (font != null) hintText.font = font;
        hintText.fontSize = 36f;
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.raycastTarget = false;
        hintText.color = new Color(1f, 1f, 1f, 0.75f);
        hintText.text = DescribeCloseKey(dm);

        // 대화창보다 위에 그려지도록 맨 뒤 자식으로 둡니다.
        panel.transform.SetAsLastSibling();

        // ── 배선 ───────────────────────────────────────────────
        Undo.RegisterCreatedObjectUndo(panel, "컷씬 UI 만들기");

        dm.cutsceneCanvasGroup = group;
        dm.cutsceneImage = artImage;

        EditorUtility.SetDirty(dm);
        EditorSceneManager.MarkSceneDirty(dm.gameObject.scene);

        Selection.activeGameObject = panel;
        EditorGUIUtility.PingObject(panel);

        EditorUtility.DisplayDialog("컷씬 UI 완성",
            "'" + canvas.gameObject.name + "' 안에 " + PanelName + "을 만들고\n" +
            "DialogueManager의 Cutscene Canvas Group / Cutscene Image에 연결했습니다.\n\n" +
            "Ctrl+S로 저장한 뒤 대화를 다시 실행해 보세요.\n\n" +
            "그림 크기나 위치는 CutsceneImage의 RectTransform에서 조절하시면 됩니다.",
            "확인");
    }

    // ───────────────────────────────────────────────────────────

    /// <summary>대화 UI가 올라가 있는 캔버스를 찾습니다.</summary>
    private static Canvas FindDialogueCanvas(DialogueManager dm)
    {
        // 1순위 : 대화창이 속한 캔버스
        if (dm.dialogueBoxCanvasGroup != null)
        {
            var c = dm.dialogueBoxCanvasGroup.GetComponentInParent<Canvas>();
            if (c != null) return c.rootCanvas != null ? c.rootCanvas : c;
        }

        // 2순위 : 초상화가 속한 캔버스
        if (dm.portraitRect != null)
        {
            var c = dm.portraitRect.GetComponentInParent<Canvas>();
            if (c != null) return c.rootCanvas != null ? c.rootCanvas : c;
        }

        // 3순위 : DialogueManager 자신이 캔버스 안에 있는 경우
        var own = dm.GetComponentInParent<Canvas>();
        if (own != null) return own.rootCanvas != null ? own.rootCanvas : own;

        // 마지막 : 씬에서 화면 전체를 덮는 캔버스 아무거나
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c.transform.parent == null) return c;
        }

        return null;
    }

    private static string DescribeCloseKey(DialogueManager dm)
    {
        string key = dm.cutsceneCloseKey.ToString();
        return dm.cutsceneAllowSpace
            ? key + " 또는 Space 를 눌러 계속"
            : key + " 를 눌러 계속";
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
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

        return null;
    }
}
