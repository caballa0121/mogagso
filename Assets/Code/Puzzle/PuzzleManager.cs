using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 퍼즐 화면의 두뇌.
///
/// PuzzleRunner가 넘겨준 PuzzleDefinition을 받아 조각을 흩뿌리고,
/// 드래그로 맞추게 하고, 다 맞추면 완성본을 보여준 뒤 클릭을 기다렸다가 끝냅니다.
///
/// 이 씬은 다른 씬 '위에 덧씌워' 열리므로 카메라도 EventSystem도 쓰지 않습니다.
/// (Screen Space Overlay 캔버스는 카메라 없이도 그려집니다)
/// </summary>
public class PuzzleManager : MonoBehaviour
{
    [Header("씬 연결")]
    public Canvas canvas;
    [Tooltip("밑에 깔린 씬을 가리는 불투명 배경")]
    public Image backdrop;
    [Tooltip("조각들이 올라갈 판. 이 판의 한가운데가 좌표 0,0 입니다.")]
    public RectTransform board;
    [Tooltip("다 맞췄을 때 겹쳐지는 완성본")]
    public Image completedImage;
    [Tooltip("'클릭해서 계속' 안내")]
    public TextMeshProUGUI hintText;
    [Tooltip("들고 날 때 쓰는 검은 판")]
    public Image fadeImage;

    [Header("연출 시간")]
    public float fadeInDuration = 0.6f;
    public float fadeOutDuration = 0.6f;

    [Header("집기 판정")]
    [Range(0f, 1f)]
    [Tooltip("이 정도보다 옅은 부분은 통과시켜 밑에 깔린 조각을 집게 합니다.")]
    public float alphaThreshold = 0.1f;

    [Header("안내 문구")]
    public string hintMessage = "클릭해서 계속";

    [Header("테스트용")]
    [Tooltip("이 씬을 혼자 실행해 볼 때 쓸 퍼즐. 실제 진행 때는 무시됩니다.")]
    public PuzzleDefinition testDefinition;

    private PuzzleDefinition definition;
    private readonly List<PuzzlePiece> pieces = new List<PuzzlePiece>();

    private bool interactable;
    private PuzzlePiece dragging;
    private Vector2 grabOffset;
    private int placedCount;

    void Awake()
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        SetFadeAlpha(1f);
        SetCompletedAlpha(0f);
        if (hintText != null) hintText.text = "";
    }

    void Start()
    {
        definition = PuzzleRunner.PendingDefinition != null ? PuzzleRunner.PendingDefinition : testDefinition;

        if (definition == null || definition.pieces == null || definition.pieces.Count == 0)
        {
            Debug.LogError("[Puzzle] 풀 퍼즐이 없습니다. 바로 빠져나갑니다. " +
                           "(Tools → 퍼즐 → [조각 이미지 재단]으로 PuzzleDefinition을 먼저 만들어 주세요)");
            PuzzleRunner.NotifyFinished();
            return;
        }

        EnsureCameraForStandalone();
        BuildBoard();
        StartCoroutine(PlayRoutine());
    }

    // ─────────────────────────── 판 만들기 ───────────────────────────

    private void BuildBoard()
    {
        if (board == null)
        {
            Debug.LogError("[Puzzle] board가 연결되지 않았습니다.");
            return;
        }

        board.sizeDelta = definition.canvasSize;

        // 원본 그림이 화면 비율과 달라도 화면 안에 꽉 들어오도록 맞춰줍니다.
        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        if (canvasRect != null && definition.canvasSize.x > 0f && definition.canvasSize.y > 0f)
        {
            float scale = Mathf.Min(canvasRect.rect.width / definition.canvasSize.x,
                                    canvasRect.rect.height / definition.canvasSize.y);
            // 이 필드가 생기기 전에 만들어진 에셋이면 0으로 읽힐 수 있어 그때는 기본값을 씁니다.
            float fit = definition.boardFitRatio > 0.05f ? Mathf.Clamp01(definition.boardFitRatio) : 0.7f;
            scale *= fit;
            if (scale > 0f) board.localScale = new Vector3(scale, scale, 1f);
        }

        if (completedImage != null)
        {
            completedImage.sprite = definition.completedImage;
            var cRect = completedImage.rectTransform;
            cRect.sizeDelta = definition.canvasSize;
        }

        // 맞출 틀과 흐린 밑그림을 조각보다 먼저(= 뒤에) 깔아둡니다.
        CreateBoardFrame();
        CreateGhostPreview();

        // 조각 만들기
        foreach (var data in definition.pieces)
        {
            if (data == null || data.sprite == null) continue;

            var go = new GameObject("Piece_" + data.sprite.name, typeof(RectTransform));
            go.transform.SetParent(board, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);

            var img = go.AddComponent<Image>();
            img.raycastTarget = false;

            var piece = go.AddComponent<PuzzlePiece>();
            piece.rect = rect;
            piece.image = img;
            piece.Setup(data);

            pieces.Add(piece);
        }

        Scatter();

        // 완성본이 판의 자식이면 조각들보다 위에 오도록 맨 뒤로 보냅니다.
        if (completedImage != null && completedImage.transform.parent == board)
        {
            completedImage.transform.SetAsLastSibling();
        }
    }

    /// <summary>조각들을 판 위에 흩뿌립니다. 우연히 제자리에 놓이지 않도록 최소 거리를 둡니다.</summary>
    private void Scatter()
    {
        // 흩뿌릴 수 있는 범위는 '판'이 아니라 '화면 전체'입니다.
        // 판을 화면보다 작게 놓았기 때문에 그 바깥 여백까지 조각을 늘어놓을 수 있습니다.
        float boardScale = board.localScale.x > 0.001f ? board.localScale.x : 1f;
        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;

        float halfW = (canvasRect != null && canvasRect.rect.width > 1f)
            ? canvasRect.rect.width * 0.5f / boardScale
            : definition.canvasSize.x * 0.5f;

        float halfH = (canvasRect != null && canvasRect.rect.height > 1f)
            ? canvasRect.rect.height * 0.5f / boardScale
            : definition.canvasSize.y * 0.5f;

        float minAway = definition.snapDistance * 3f;

        foreach (var piece in pieces)
        {
            Vector2 size = piece.rect.sizeDelta;

            // 조각이 화면 밖으로 나가지 않는 범위
            float rangeX = Mathf.Max(0f, halfW - size.x * 0.5f - definition.scatterMargin);
            float rangeY = Mathf.Max(0f, halfH - size.y * 0.5f - definition.scatterMargin);

            Vector2 pos = piece.correctPosition;

            // 제자리에서 충분히 떨어진 자리가 나올 때까지 몇 번 굴려봅니다.
            for (int attempt = 0; attempt < 20; attempt++)
            {
                pos = new Vector2(Random.Range(-rangeX, rangeX), Random.Range(-rangeY, rangeY));
                if (Vector2.Distance(pos, piece.correctPosition) >= minAway) break;
            }

            piece.rect.anchoredPosition = pos;
        }

        // 겹침 순서를 섞어서 특정 조각만 계속 위에 오지 않게 합니다.
        for (int i = 0; i < pieces.Count; i++)
        {
            pieces[Random.Range(0, pieces.Count)].transform.SetAsLastSibling();
        }
    }

    // ─────────────────────────── 틀 / 밑그림 / 카메라 ───────────────────────────

    /// <summary>
    /// 원본 그림 크기의 틀을 그립니다.
    /// 이게 없으면 플레이어가 조각끼리만 맞추게 되어,
    /// 그림이 통째로 밀린 채 완성돼도 스냅이 하나도 안 걸립니다.
    /// </summary>
    private void CreateBoardFrame()
    {
        if (!definition.showBoardFrame) return;

        var go = new GameObject("BoardFrame", typeof(RectTransform));
        go.transform.SetParent(board, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = definition.canvasSize;

        var fill = go.AddComponent<Image>();
        fill.color = definition.frameFillColor;
        fill.raycastTarget = false;

        float t = Mathf.Max(1f, definition.frameBorderThickness);
        float w = definition.canvasSize.x;
        float h = definition.canvasSize.y;

        CreateBorderBar(go.transform, new Vector2(0f, h * 0.5f), new Vector2(w + t * 2f, t)); // 위
        CreateBorderBar(go.transform, new Vector2(0f, -h * 0.5f), new Vector2(w + t * 2f, t)); // 아래
        CreateBorderBar(go.transform, new Vector2(-w * 0.5f, 0f), new Vector2(t, h)); // 왼쪽
        CreateBorderBar(go.transform, new Vector2(w * 0.5f, 0f), new Vector2(t, h)); // 오른쪽
    }

    private void CreateBorderBar(Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("Border", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.color = definition.frameBorderColor;
        img.raycastTarget = false;
    }

    /// <summary>완성본을 아주 흐리게 깔아 어느 조각이 어디로 가는지 알려줍니다.</summary>
    private void CreateGhostPreview()
    {
        if (!definition.showGhostPreview || definition.completedImage == null) return;

        var go = new GameObject("GhostPreview", typeof(RectTransform));
        go.transform.SetParent(board, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = definition.canvasSize;

        var img = go.AddComponent<Image>();
        img.sprite = definition.completedImage;
        img.color = new Color(1f, 1f, 1f, definition.ghostAlpha);
        img.raycastTarget = false;
    }

    /// <summary>
    /// 이 씬은 덧씌워 열리는 용도라 카메라가 없습니다.
    /// 단독으로 Play해서 확인할 때만 임시 카메라를 만들어
    /// 'No cameras rendering' 안내가 뜨지 않게 합니다.
    /// </summary>
    private void EnsureCameraForStandalone()
    {
        if (Camera.allCamerasCount > 0) return;

        var camGO = new GameObject("Puzzle Standalone Camera");
        camGO.transform.SetParent(transform, false); // 씬이 걷힐 때 같이 사라집니다.

        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.cullingMask = 0; // UI는 Overlay라 카메라가 그리지 않아도 보입니다.
        cam.depth = -100;
    }

    /// <summary>
    /// 조각들 중 맨 뒤에 해당하는 자리.
    /// 맞춰진 조각을 여기로 보내면 아직 못 맞춘 조각은 가리지 않으면서도
    /// 틀과 밑그림보다는 앞에 남습니다.
    /// </summary>
    private int PieceBaseIndex()
    {
        for (int i = 0; i < board.childCount; i++)
        {
            if (board.GetChild(i).GetComponent<PuzzlePiece>() != null) return i;
        }
        return 0;
    }

    private void UpdateProgressText()
    {
        if (hintText == null || !definition.showProgress) return;
        hintText.text = placedCount + " / " + pieces.Count;
    }

    // ─────────────────────────── 진행 ───────────────────────────

    private IEnumerator PlayRoutine()
    {
        yield return StartCoroutine(FadeTo(0f, fadeInDuration));

        interactable = true;
        UpdateProgressText();

        while (placedCount < pieces.Count) yield return null;

        interactable = false;
        dragging = null;

        // 마지막 조각이 제자리로 미끄러져 들어가는 걸 마저 보여줍니다.
        yield return new WaitForSeconds(definition.snapDuration);

        yield return StartCoroutine(CompleteRoutine());

        yield return StartCoroutine(FadeTo(1f, fadeOutDuration));

        PuzzleRunner.NotifyFinished();
    }

    private IEnumerator CompleteRoutine()
    {
        // 완성본이 있으면 조각 위에 부드럽게 겹칩니다.
        if (completedImage != null && definition.completedImage != null)
        {
            float elapsed = 0f;
            float dur = Mathf.Max(0.01f, definition.completeFadeDuration);

            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                SetCompletedAlpha(Mathf.Clamp01(elapsed / dur));
                yield return null;
            }
            SetCompletedAlpha(1f);
        }

        if (definition.hintDelay > 0f) yield return new WaitForSeconds(definition.hintDelay);

        if (hintText != null) hintText.text = hintMessage;

        // 클릭할 때까지 기다립니다.
        yield return null; // 마지막 조각을 놓은 클릭이 그대로 넘어가지 않도록 한 프레임 흘립니다.
        while (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) yield return null;

        if (hintText != null) hintText.text = "";
    }

    // ─────────────────────────── 드래그 ───────────────────────────

    void Update()
    {
        if (!interactable) return;
        if (Mouse.current == null) return;

        Vector2 mouse = Mouse.current.position.ReadValue();

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            BeginDrag(mouse);
        }
        else if (dragging != null)
        {
            if (Mouse.current.leftButton.isPressed) DragTo(mouse);
            else EndDrag();
        }
    }

    private void BeginDrag(Vector2 mouse)
    {
        // 위에 있는 조각부터 확인합니다.
        for (int i = board.childCount - 1; i >= 0; i--)
        {
            var piece = board.GetChild(i).GetComponent<PuzzlePiece>();
            if (piece == null || piece.isPlaced) continue;
            if (!piece.ContainsScreenPoint(mouse, null, alphaThreshold)) continue;

            dragging = piece;
            dragging.transform.SetAsLastSibling();

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(board, mouse, null, out Vector2 local))
                grabOffset = piece.rect.anchoredPosition - local;
            else
                grabOffset = Vector2.zero;

            return;
        }
    }

    private void DragTo(Vector2 mouse)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(board, mouse, null, out Vector2 local))
            dragging.rect.anchoredPosition = local + grabOffset;
    }

    private void EndDrag()
    {
        PuzzlePiece piece = dragging;
        dragging = null;

        if (piece == null) return;

        if (piece.DistanceToHome <= definition.snapDistance)
        {
            StartCoroutine(SnapRoutine(piece));
        }
    }

    private IEnumerator SnapRoutine(PuzzlePiece piece)
    {
        piece.isPlaced = true;
        placedCount++;

        // 맞춰진 조각은 뒤로 보내서 아직 못 맞춘 조각을 가리지 않게 합니다.
        piece.transform.SetSiblingIndex(PieceBaseIndex());
        UpdateProgressText();

        Vector2 start = piece.rect.anchoredPosition;
        float dur = Mathf.Max(0.01f, definition.snapDuration);
        float elapsed = 0f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            piece.rect.anchoredPosition = Vector2.Lerp(start, piece.correctPosition, 1f - Mathf.Pow(1f - t, 3f));
            yield return null;
        }

        piece.rect.anchoredPosition = piece.correctPosition;
    }

    // ─────────────────────────── 잔심부름 ───────────────────────────

    private void SetFadeAlpha(float a)
    {
        if (fadeImage == null) return;

        Color c = fadeImage.color;
        c.a = a;
        fadeImage.color = c;
        fadeImage.enabled = a > 0.001f;
    }

    private void SetCompletedAlpha(float a)
    {
        if (completedImage == null) return;

        Color c = completedImage.color;
        c.a = a;
        completedImage.color = c;
        completedImage.enabled = a > 0.001f;
    }

    private IEnumerator FadeTo(float target, float duration)
    {
        if (fadeImage == null) yield break;

        float start = fadeImage.color.a;

        if (duration <= 0f)
        {
            SetFadeAlpha(target);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetFadeAlpha(Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetFadeAlpha(target);
    }
}
