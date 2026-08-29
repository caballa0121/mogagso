using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 노드맵 씬을 자동으로 만들어 주는 에디터 도구.
///
/// Unity 상단 메뉴 → Tools → 노드맵 → [노드맵 씬 만들기] 를 누르면
/// Assets/Scenes/NodeMap.unity 가 통째로 만들어지고 배선까지 끝난 상태가 됩니다.
/// 만들어진 뒤에는 평범한 씬이라 인스펙터에서 자유롭게 고칠 수 있습니다.
/// </summary>
public static class NodeMapSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/NodeMap.unity";
    private const string TileFolder = "Assets/image/node";
    private const string TileSpritePath = TileFolder + "/node_tile.png";
    private const string KoreanFontPath = "Assets/2.asset";
    private const string HeroSheetPath = "Assets/image/character/KakaoTalk_20260818_141414438_02.png";

    // 기본으로 깔아둘 격자 크기 (나중에 블럭을 지우거나 복제해서 모양을 바꾸면 됩니다)
    private const int GridWidth = 7;
    private const int GridHeight = 5;
    private static readonly Vector2 CellSize = new Vector2(2f, 2f);

    [MenuItem("Tools/노드맵/노드맵 씬 만들기", false, 10)]
    public static void BuildScene()
    {
        if (File.Exists(ScenePath))
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "노드맵 씬 다시 만들기",
                ScenePath + " 가 이미 있습니다.\n\n덮어쓰면 그 씬에 직접 해둔 수정은 전부 사라집니다.\n덮어쓸까요?",
                "덮어쓰기", "취소");
            if (!overwrite) return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Sprite tileSprite = EnsureTileSprite();
        TMP_FontAsset font = FindKoreanFont();
        Sprite heroSprite = FindHeroSprite();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── 카메라 ─────────────────────────────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.position = new Vector3(0f, 0f, -10f);
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5.8f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.09f, 0.10f, 0.14f, 1f);
        camGO.AddComponent<AudioListener>();

        // ── 블럭(노드) 격자 ────────────────────────────────────
        var gridGO = new GameObject("NodeGrid");
        var nodeList = new List<MapNode>();

        float originX = -(GridWidth - 1) * CellSize.x * 0.5f;
        float originY = -(GridHeight - 1) * CellSize.y * 0.5f;

        for (int y = 0; y < GridHeight; y++)
        {
            for (int x = 0; x < GridWidth; x++)
            {
                Vector3 pos = new Vector3(originX + x * CellSize.x, originY + y * CellSize.y, 0f);
                MapNode node = CreateNode(gridGO.transform, new Vector2Int(x, y), pos, tileSprite, font);
                node.nodeType = DefaultTypeFor(x, y);
                nodeList.Add(node);
            }
        }

        // ── 주인공 말 ──────────────────────────────────────────
        var tokenGO = new GameObject("PlayerToken");
        var tokenSR = tokenGO.AddComponent<SpriteRenderer>();
        tokenSR.sprite = heroSprite != null ? heroSprite : tileSprite;
        tokenSR.sortingOrder = 20;
        if (heroSprite != null && heroSprite.bounds.size.y > 0.001f)
        {
            float s = 1.5f / heroSprite.bounds.size.y;
            tokenGO.transform.localScale = new Vector3(s, s, 1f);
        }

        // ── UI 캔버스 ──────────────────────────────────────────
        var canvasGO = new GameObject("UI Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // 파티 체력 (좌상단)
        TextMeshProUGUI partyHp = CreateUIText(canvasGO.transform, "PartyHpText", font, 34f,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(40f, -32f), new Vector2(1200f, 60f), TextAlignmentOptions.TopLeft);
        partyHp.color = new Color(0.92f, 0.92f, 0.95f, 1f);
        partyHp.text = "";

        // 안내 문구 (하단 중앙)
        TextMeshProUGUI hint = CreateUIText(canvasGO.transform, "HintText", font, 34f,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 60f), new Vector2(1400f, 60f), TextAlignmentOptions.Center);
        hint.color = new Color(0.85f, 0.85f, 0.9f, 1f);
        hint.text = "";

        // 로고 묶음 (화면 정중앙)
        var logoGO = new GameObject("LogoGroup", typeof(RectTransform));
        logoGO.transform.SetParent(canvasGO.transform, false);
        var logoRect = logoGO.GetComponent<RectTransform>();
        logoRect.anchorMin = logoRect.anchorMax = logoRect.pivot = new Vector2(0.5f, 0.5f);
        logoRect.anchoredPosition = Vector2.zero;
        logoRect.sizeDelta = new Vector2(1000f, 420f);
        var logoGroup = logoGO.AddComponent<CanvasGroup>();
        logoGroup.alpha = 0f;
        logoGroup.interactable = false;
        logoGroup.blocksRaycasts = false;

        var logoImgGO = new GameObject("LogoImage", typeof(RectTransform));
        logoImgGO.transform.SetParent(logoGO.transform, false);
        var logoImgRect = logoImgGO.GetComponent<RectTransform>();
        logoImgRect.anchorMin = logoImgRect.anchorMax = logoImgRect.pivot = new Vector2(0.5f, 0.5f);
        logoImgRect.sizeDelta = new Vector2(420f, 420f);
        var logoImg = logoImgGO.AddComponent<Image>();
        logoImg.raycastTarget = false;
        logoImg.enabled = false; // 로고 스프라이트를 넣기 전까지는 글자만 씁니다.

        TextMeshProUGUI logoText = CreateUIText(logoGO.transform, "LogoText", font, 170f,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 40f), new Vector2(1000f, 240f), TextAlignmentOptions.Center);
        logoText.fontStyle = FontStyles.Bold;

        TextMeshProUGUI subText = CreateUIText(logoGO.transform, "SubText", font, 48f,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -110f), new Vector2(1000f, 80f), TextAlignmentOptions.Center);
        subText.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        subText.text = "";

        // 페이드 (제일 마지막 자식 = 제일 위에 그려짐)
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

        // ── 매니저 ─────────────────────────────────────────────
        var mgrGO = new GameObject("NodeMapManager");
        var presenter = mgrGO.AddComponent<NodeMapPresenter>();
        presenter.logoGroup = logoGroup;
        presenter.logoRect = logoRect;
        presenter.logoImage = logoImg;
        presenter.logoText = logoText;
        presenter.subText = subText;

        // 함정 연출용 암전 + 커다란 주인공
        EnsureTrapUI(canvasGO.transform, presenter, heroSprite);

        var mgr = mgrGO.AddComponent<NodeMapManager>();
        mgr.nodeRoot = gridGO.transform;
        mgr.playerToken = tokenGO.transform;
        mgr.playerTokenSprite = tokenSR;
        mgr.presenter = presenter;
        mgr.mapCamera = cam;
        mgr.cellSize = CellSize;
        mgr.partyHpText = partyHp;
        mgr.hintText = hint;
        mgr.battleSceneName = "Battle Scene";
        mgr.goalSceneName = "CHAPTER 2";
        mgr.trapDamageRatio = 0.2f;

        // 주인공을 시작 칸 위에 미리 올려둡니다.
        MapNode start = nodeList.Find(n => n.nodeType == MapNodeType.Start);
        if (start != null) tokenGO.transform.position = start.transform.position + mgr.tokenOffset;

        // ── 저장 & 빌드 세팅 등록 ──────────────────────────────
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        RegisterInBuildSettings();

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("노드맵 씬 완성",
            "NodeMap 씬을 만들고 Build Settings에 등록했습니다.\n\n" +
            "- 블럭을 지우거나 복제해서 맵 모양을 바꾸세요.\n" +
            "- 블럭마다 인스펙터의 Node Type을 전투 / 함정 / 빈칸으로 정하세요.\n" +
            "- 위치를 옮겼다면 NodeMapManager의 톱니 메뉴에서\n" +
            "  [월드 위치로 좌표 다시 계산]을 꼭 눌러주세요.",
            "확인");
    }

    /// <summary>
    /// 이미 만들어 둔 NodeMap 씬에 함정 연출용 오브젝트(암전 + 커다란 주인공)만 더 넣어줍니다.
    /// 씬을 통째로 다시 만들지 않아도 되므로 손으로 해둔 수정이 그대로 남습니다.
    /// </summary>
    [MenuItem("Tools/노드맵/열려 있는 씬에 함정 연출 UI 보강", false, 21)]
    public static void PatchTrapUIInOpenScene()
    {
        var presenter = Object.FindAnyObjectByType<NodeMapPresenter>();
        if (presenter == null)
        {
            EditorUtility.DisplayDialog("노드맵", "열려 있는 씬에서 NodeMapPresenter를 찾지 못했습니다.\nNodeMap 씬을 열고 다시 눌러주세요.", "확인");
            return;
        }

        Transform canvasTf = presenter.logoGroup != null ? presenter.logoGroup.transform.parent : null;
        if (canvasTf == null)
        {
            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("노드맵", "씬에서 Canvas를 찾지 못했습니다.", "확인");
                return;
            }
            canvasTf = canvas.transform;
        }

        EnsureTrapUI(canvasTf, presenter, FindHeroSprite());

        EditorUtility.SetDirty(presenter);
        EditorSceneManager.MarkSceneDirty(presenter.gameObject.scene);

        EditorUtility.DisplayDialog("노드맵",
            "함정 연출용 TrapDim(암전)과 TrapHero(커다란 주인공)를 넣고 연결했습니다.\n\n" +
            "Ctrl+S로 씬을 저장해 주세요.", "확인");
    }

    [MenuItem("Tools/노드맵/열려 있는 씬의 노드 좌표 다시 계산", false, 20)]
    public static void RecalcCoordsInOpenScene()
    {
        var mgr = Object.FindAnyObjectByType<NodeMapManager>();
        if (mgr == null)
        {
            EditorUtility.DisplayDialog("노드맵", "열려 있는 씬에서 NodeMapManager를 찾지 못했습니다.", "확인");
            return;
        }

        mgr.RecalculateCoords();
        EditorSceneManager.MarkSceneDirty(mgr.gameObject.scene);
    }

    // ───────────────────────────────────────────────────────────

    private static MapNodeType DefaultTypeFor(int x, int y)
    {
        // 바로 굴려볼 수 있게 만든 기본 배치입니다. 마음대로 바꾸세요.
        if (x == 0 && y == 2) return MapNodeType.Start;
        if (x == GridWidth - 1 && y == 2) return MapNodeType.Goal;

        if ((x == 2 && y == 2) || (x == 3 && y == 0) || (x == 3 && y == 4) || (x == 5 && y == 2))
            return MapNodeType.Battle;

        if ((x == 1 && y == 1) || (x == 1 && y == 3) || (x == 4 && y == 2) || (x == 2 && y == 4))
            return MapNodeType.Trap;

        return MapNodeType.Empty;
    }

    /// <summary>
    /// 함정 연출에 쓰는 화면 암전(TrapDim)과 커다란 주인공(TrapHero)을 만들고 연결합니다.
    /// 이미 연결돼 있으면 건드리지 않고 그리는 순서만 바로잡습니다.
    /// </summary>
    private static void EnsureTrapUI(Transform canvasTf, NodeMapPresenter presenter, Sprite heroSprite)
    {
        // 화면 전체를 덮는 검은 판
        if (presenter.dimImage == null)
        {
            var dimGO = new GameObject("TrapDim", typeof(RectTransform));
            dimGO.transform.SetParent(canvasTf, false);

            var rect = dimGO.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = dimGO.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = false;
            img.enabled = false;

            presenter.dimImage = img;
        }

        // 암전 위에 크게 뜨는 주인공
        if (presenter.trapHeroImage == null)
        {
            var heroGO = new GameObject("TrapHero", typeof(RectTransform));
            heroGO.transform.SetParent(canvasTf, false);

            var rect = heroGO.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -40f);
            rect.sizeDelta = new Vector2(620f, 620f);

            var img = heroGO.AddComponent<Image>();
            img.sprite = heroSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, 0f);
            img.enabled = false;

            presenter.trapHeroImage = img;
            presenter.trapHeroRect = rect;
        }

        if (presenter.trapHeroRect == null && presenter.trapHeroImage != null)
        {
            presenter.trapHeroRect = presenter.trapHeroImage.rectTransform;
        }

        // 그리는 순서: 암전 → 커다란 주인공 → 로고 → 씬 전환 페이드(항상 맨 위)
        if (presenter.logoGroup != null)
        {
            presenter.dimImage.transform.SetSiblingIndex(presenter.logoGroup.transform.GetSiblingIndex());
            presenter.trapHeroImage.transform.SetSiblingIndex(presenter.logoGroup.transform.GetSiblingIndex());
        }

        var fader = canvasTf.GetComponentInChildren<ScreenFader>(true);
        if (fader != null) fader.transform.SetAsLastSibling();
    }

    private static MapNode CreateNode(Transform parent, Vector2Int coord, Vector3 pos, Sprite tile, TMP_FontAsset font)
    {
        var go = new GameObject($"Node_{coord.x}_{coord.y}");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = tile;
        sr.sortingOrder = 0;

        var col = go.AddComponent<BoxCollider2D>();
        if (tile != null) col.size = tile.bounds.size;

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        labelGO.transform.localPosition = new Vector3(0f, 0f, -0.1f);
        var label = labelGO.AddComponent<TextMeshPro>();
        if (font != null) label.font = font;
        label.text = "?";
        label.fontSize = 5f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.12f, 0.12f, 0.14f, 1f);
        label.sortingOrder = 1;
        var labelRect = labelGO.GetComponent<RectTransform>();
        if (labelRect != null) labelRect.sizeDelta = new Vector2(1.6f, 1.6f);

        var node = go.AddComponent<MapNode>();
        node.coord = coord;
        node.label = label;

        return node;
    }

    private static TextMeshProUGUI CreateUIText(Transform parent, string name, TMP_FontAsset font, float size,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta,
        TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = sizeDelta;

        var text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.alignment = align;
        text.raycastTarget = false;
        text.text = "";

        return text;
    }

    private static TMP_FontAsset FindKoreanFont()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
        if (font != null) return font;

        // 프로젝트 안에서 LiberationSans가 아닌 첫 번째 TMP 폰트를 찾아 씁니다.
        foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("LiberationSans")) continue;

            var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (f != null) return f;
        }

        Debug.LogWarning("[NodeMap] 한글이 들어간 TMP 폰트를 찾지 못했습니다. 칸 글씨가 네모로 보일 수 있습니다.");
        return null;
    }

    private static Sprite FindHeroSprite()
    {
        Object[] all = AssetDatabase.LoadAllAssetRepresentationsAtPath(HeroSheetPath);
        foreach (var o in all)
        {
            if (o is Sprite s) return s;
        }

        var single = AssetDatabase.LoadAssetAtPath<Sprite>(HeroSheetPath);
        if (single != null) return single;

        Debug.LogWarning("[NodeMap] 주인공 스프라이트를 찾지 못해 임시 이미지를 씁니다.");
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

    // ── 임시 블럭 스프라이트 만들기 ────────────────────────────

    /// <summary>
    /// 보내주신 블럭 이미지 대신 쓸 둥근 사각형 스프라이트를 만들어 둡니다.
    /// 나중에 진짜 png를 같은 자리에 넣거나, 블럭들의 Sprite를 바꿔 끼우면 됩니다.
    /// </summary>
    private static Sprite EnsureTileSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(TileSpritePath);
        if (existing != null) return existing;

        Directory.CreateDirectory(TileFolder);

        const int size = 170;          // 100 PPU 기준 1.7 유닛
        const float radius = 34f;
        const float border = 7f;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var fill = new Color(1f, 1f, 1f, 1f);
        var edge = new Color(0.22f, 0.22f, 0.25f, 1f);
        var pixels = new Color[size * size];

        float half = size * 0.5f;
        float inner = half - radius;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f - half;
                float py = y + 0.5f - half;

                float dx = Mathf.Max(Mathf.Abs(px) - inner, 0f);
                float dy = Mathf.Max(Mathf.Abs(py) - inner, 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy) - radius; // 0보다 작으면 안쪽

                float alpha = Mathf.Clamp01(0.5f - dist);            // 가장자리 부드럽게
                Color c = dist > -border ? edge : fill;
                c.a = alpha;

                pixels[y * size + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        File.WriteAllBytes(TileSpritePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(TileSpritePath, ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(TileSpritePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(TileSpritePath);
    }
}
