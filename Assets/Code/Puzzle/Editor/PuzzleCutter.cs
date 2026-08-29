using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 조각 PNG 자동 재단 도구.
///
/// Assets/image/puzzle 폴더에 조각 이미지들을 넣고 메뉴를 누르면
///   1. 각 PNG에서 그림이 있는 부분만 잘라내 cut 폴더에 작은 조각으로 저장하고
///   2. 원래 있던 자리(정답 좌표)를 자동으로 계산해서
///   3. PuzzleDefinition 에셋을 만들어 전부 연결합니다.
///
/// 조각들이 그림을 빈틈없이 덮는지도 검사해서 알려줍니다.
/// </summary>
public static class PuzzleCutter
{
    // 프로젝트 창에서 폴더를 따로 고르지 않았을 때 뒤질 기본 폴더
    private const string DefaultSourceFolder = "Assets/image/puzzle";

    // 이 값보다 옅은 픽셀은 '없는 것'으로 봅니다.
    private const float AlphaCut = 10f / 255f;

    // 불투명 비율이 이보다 높으면 조각이 아니라 완성본으로 봅니다.
    private const float CompleteCoverage = 0.95f;

    [MenuItem("Tools/퍼즐/조각 이미지 재단", false, 10)]
    public static void Cut()
    {
        string sourceFolder = ResolveSourceFolder();
        string cutFolder = sourceFolder + "/cut";
        string definitionPath = sourceFolder + "/PuzzleDefinition.asset";

        if (!Directory.Exists(sourceFolder))
        {
            Directory.CreateDirectory(sourceFolder);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("퍼즐 재단",
                sourceFolder + " 폴더를 만들었습니다.\n\n여기에 조각 이미지와 완성본을 넣고 다시 눌러주세요.", "확인");
            return;
        }

        List<string> sources = CollectSourceImages(sourceFolder);
        if (sources.Count == 0)
        {
            EditorUtility.DisplayDialog("퍼즐 재단",
                "다음 폴더에서 이미지를 찾지 못했습니다.\n\n" + sourceFolder + "\n\n" +
                "조각 PNG를 이 폴더에 넣거나,\n" +
                "프로젝트 창에서 이미지가 든 폴더를 클릭해 고른 뒤\n다시 눌러주세요.", "확인");
            return;
        }

        Directory.CreateDirectory(cutFolder);

        // ── 1단계 : 원본을 모두 읽어 크기와 불투명 영역을 조사합니다 ──
        var infos = new List<SourceInfo>();
        int canvasW = 0, canvasH = 0;

        try
        {
            for (int i = 0; i < sources.Count; i++)
            {
                string path = sources[i];
                EditorUtility.DisplayProgressBar("퍼즐 재단", "읽는 중 : " + Path.GetFileName(path), (float)i / sources.Count);

                SourceInfo info = Analyze(path, cutFolder);
                if (info == null) continue;

                infos.Add(info);
                canvasW = Mathf.Max(canvasW, info.width);
                canvasH = Mathf.Max(canvasH, info.height);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (infos.Count == 0)
        {
            EditorUtility.DisplayDialog("퍼즐 재단", "읽을 수 있는 이미지가 없습니다.", "확인");
            return;
        }

        // ── 2단계 : 완성본과 조각을 갈라냅니다 ──
        SourceInfo complete = null;
        var pieceInfos = new List<SourceInfo>();

        foreach (var info in infos)
        {
            bool looksComplete = info.coverage >= CompleteCoverage || IsCompleteName(info.fileName);

            if (looksComplete && complete == null) complete = info;
            else pieceInfos.Add(info);
        }

        if (pieceInfos.Count == 0)
        {
            EditorUtility.DisplayDialog("퍼즐 재단",
                "조각으로 쓸 이미지가 없습니다.\n\n완성본 한 장만 있는 것 같습니다. 조각 이미지들도 함께 넣어주세요.", "확인");
            return;
        }

        // ── 3단계 : 조각을 잘라 저장합니다 ──
        var pieceDataList = new List<PuzzlePieceData>();
        var cutPaths = new List<string>();
        var coverCount = new int[canvasW * canvasH];

        try
        {
            for (int i = 0; i < pieceInfos.Count; i++)
            {
                SourceInfo info = pieceInfos[i];
                EditorUtility.DisplayProgressBar("퍼즐 재단", "자르는 중 : " + info.fileName, (float)i / pieceInfos.Count);

                PuzzlePieceData data = CutOne(info, canvasW, canvasH, coverCount);
                if (data == null) continue;

                pieceDataList.Add(data);
                cutPaths.Add(info.cutPath);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();

        // 잘라 만든 파일들을 스프라이트로 연결
        for (int i = 0; i < pieceDataList.Count; i++)
        {
            pieceDataList[i].sprite = AssetDatabase.LoadAssetAtPath<Sprite>(cutPaths[i]);

            if (pieceDataList[i].sprite == null)
                Debug.LogWarning($"[퍼즐 재단] {cutPaths[i]} 를 스프라이트로 불러오지 못했습니다.");
        }

        // ── 4단계 : PuzzleDefinition 만들기 (이미 있으면 내용만 갈아끼워 연결이 안 끊기게) ──
        var definition = AssetDatabase.LoadAssetAtPath<PuzzleDefinition>(definitionPath);
        bool isNew = definition == null;

        if (isNew)
        {
            definition = ScriptableObject.CreateInstance<PuzzleDefinition>();
            AssetDatabase.CreateAsset(definition, definitionPath);
        }

        definition.canvasSize = new Vector2(canvasW, canvasH);
        definition.pieces = pieceDataList;
        definition.completedImage = complete != null ? EnsureSprite(complete.assetPath) : null;

        EditorUtility.SetDirty(definition);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ── 5단계 : 빈틈 / 겹침 검사 ──
        int empty = 0, overlap = 0;
        for (int i = 0; i < coverCount.Length; i++)
        {
            if (coverCount[i] == 0) empty++;
            else if (coverCount[i] > 1) overlap++;
        }

        float total = canvasW * (float)canvasH;
        string report =
            $"조각 {pieceDataList.Count}개를 잘라 냈습니다.\n" +
            $"원본 크기 : {canvasW} x {canvasH}\n" +
            $"완성본 : {(complete != null ? complete.fileName : "없음")}\n\n" +
            $"빈틈 : {empty / total:P1}\n" +
            $"겹침 : {overlap / total:P1}\n\n" +
            $"만든 곳 : {definitionPath}";

        Debug.Log("[퍼즐 재단] " + report.Replace("\n", " / "));
        EditorUtility.DisplayDialog("퍼즐 재단 완료", report, "확인");

        Selection.activeObject = definition;
        EditorGUIUtility.PingObject(definition);
    }

    // ───────────────────────────────────────────────────────────

    private class SourceInfo
    {
        public string assetPath;
        public string cutPath;
        public string fileName;
        public int width, height;
        public int xMin, yMin, xMax, yMax;
        public float coverage;
        public bool[] opaque;
    }

    /// <summary>
    /// 어느 폴더를 뒤질지 정합니다.
    /// 프로젝트 창에서 폴더를 골라 뒀으면 그 폴더를, 아니면 기본 폴더를 씁니다.
    /// </summary>
    private static string ResolveSourceFolder()
    {
        foreach (Object selected in Selection.GetFiltered(typeof(Object), SelectionMode.Assets))
        {
            string path = AssetDatabase.GetAssetPath(selected);
            if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path)) return path;
        }

        return DefaultSourceFolder;
    }

    private static List<string> CollectSourceImages(string sourceFolder)
    {
        var list = new List<string>();

        foreach (string file in Directory.GetFiles(sourceFolder))
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") continue;

            list.Add(file.Replace('\\', '/'));
        }

        list.Sort(string.CompareOrdinal);
        return list;
    }

    private static bool IsCompleteName(string fileName)
    {
        string n = fileName.ToLowerInvariant();
        return n.StartsWith("complete") || n.StartsWith("full") || n.StartsWith("완성") || n.StartsWith("origin");
    }

    /// <summary>
    /// 임포트 설정과 상관없이 파일을 직접 읽어 픽셀을 조사합니다.
    /// (프로젝트의 임포트 설정을 건드리지 않으려고 이렇게 합니다)
    /// </summary>
    private static SourceInfo Analyze(string assetPath, string cutFolder)
    {
        byte[] bytes;
        try { bytes = File.ReadAllBytes(assetPath); }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[퍼즐 재단] {assetPath} 를 읽지 못했습니다 : {e.Message}");
            return null;
        }

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(bytes))
        {
            Object.DestroyImmediate(tex);
            Debug.LogWarning($"[퍼즐 재단] {assetPath} 는 이미지로 읽히지 않습니다.");
            return null;
        }

        int w = tex.width, h = tex.height;
        Color32[] px = tex.GetPixels32();
        Object.DestroyImmediate(tex);

        var info = new SourceInfo
        {
            assetPath = assetPath,
            fileName = Path.GetFileNameWithoutExtension(assetPath),
            width = w,
            height = h,
            xMin = int.MaxValue,
            yMin = int.MaxValue,
            xMax = int.MinValue,
            yMax = int.MinValue,
            opaque = new bool[w * h]
        };

        byte cut = (byte)(AlphaCut * 255f);
        int opaqueCount = 0;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                if (px[idx].a <= cut) continue;

                info.opaque[idx] = true;
                opaqueCount++;

                if (x < info.xMin) info.xMin = x;
                if (x > info.xMax) info.xMax = x;
                if (y < info.yMin) info.yMin = y;
                if (y > info.yMax) info.yMax = y;
            }
        }

        if (opaqueCount == 0)
        {
            Debug.LogWarning($"[퍼즐 재단] {info.fileName} 은 전부 투명해서 건너뜁니다.");
            return null;
        }

        info.coverage = opaqueCount / (float)(w * h);
        info.cutPath = cutFolder + "/" + info.fileName + "_cut.png";

        return info;
    }

    private static PuzzlePieceData CutOne(SourceInfo info, int canvasW, int canvasH, int[] coverCount)
    {
        int w = info.xMax - info.xMin + 1;
        int h = info.yMax - info.yMin + 1;

        byte[] bytes = File.ReadAllBytes(info.assetPath);
        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        src.LoadImage(bytes);

        Color[] region = src.GetPixels(info.xMin, info.yMin, w, h);
        Object.DestroyImmediate(src);

        var cropped = new Texture2D(w, h, TextureFormat.RGBA32, false);
        cropped.SetPixels(region);
        cropped.Apply();

        File.WriteAllBytes(info.cutPath, cropped.EncodeToPNG());
        Object.DestroyImmediate(cropped);

        AssetDatabase.ImportAsset(info.cutPath, ImportAssetOptions.ForceUpdate);
        ApplyPieceImportSettings(info.cutPath);

        // 덮인 넓이 기록 (원본 캔버스 기준)
        for (int y = 0; y < info.height && y < canvasH; y++)
        {
            for (int x = 0; x < info.width && x < canvasW; x++)
            {
                if (info.opaque[y * info.width + x]) coverCount[y * canvasW + x]++;
            }
        }

        // 정답 좌표 : 캔버스 한가운데를 0,0 으로 본 조각 중심
        float centerX = info.xMin + w * 0.5f;
        float centerY = info.yMin + h * 0.5f;

        return new PuzzlePieceData
        {
            sprite = null, // 임포트가 끝난 뒤에 연결합니다.
            correctPosition = new Vector2(centerX - canvasW * 0.5f, centerY - canvasH * 0.5f),
            size = new Vector2(w, h)
        };
    }

    private static void ApplyPieceImportSettings(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;

        // 투명한 부분을 통과시키는 클릭 판정을 위해 픽셀을 읽을 수 있어야 하고,
        // 압축이 걸리면 알파값이 뭉개져서 판정이 어긋나므로 무압축으로 둡니다.
        importer.isReadable = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        importer.SaveAndReimport();
    }

    private static Sprite EnsureSprite(string assetPath)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null) return sprite;

        // 완성본이 아직 Sprite로 설정돼 있지 않으면 바꿔줍니다.
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }
}
