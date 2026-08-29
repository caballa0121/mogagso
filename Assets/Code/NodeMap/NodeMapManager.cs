using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 노드맵의 두뇌.
///
/// 씬에 배치된 MapNode(블럭)들을 모아서 격자로 취급하고,
/// 클릭 → 이동 → 칸 내용 처리(전투 / 함정 / 빈칸) → 목적지 도달 판정을 담당합니다.
///
/// 진행 상황은 NodeMapContext(static)에 적어두기 때문에
/// 전투 씬에 갔다가 돌아와도 그대로 이어집니다.
/// </summary>
public class NodeMapManager : MonoBehaviour
{
    [Header("씬 연결")]
    [Tooltip("MapNode(블럭)들이 들어 있는 부모 오브젝트. 비우면 씬 전체에서 찾습니다.")]
    public Transform nodeRoot;

    [Tooltip("맵 위를 돌아다니는 주인공 스프라이트")]
    public Transform playerToken;

    [Tooltip("주인공 스프라이트 렌더러 (방향 전환용, 없어도 됩니다)")]
    public SpriteRenderer playerTokenSprite;

    public NodeMapPresenter presenter;

    [Tooltip("비우면 Camera.main을 씁니다.")]
    public Camera mapCamera;

    [Header("주인공 배치")]
    [Tooltip("블럭 중심에서 얼마나 띄워서 세울지")]
    public Vector3 tokenOffset = new Vector3(0f, 0.35f, -1f);
    public float tokenMoveSpeed = 9f;

    [Header("이동 규칙")]
    [Tooltip("체크하면 대각선으로도 이동할 수 있습니다.")]
    public bool allowDiagonal = false;

    [Tooltip("체크하면 이미 깬 칸은 거리에 상관없이 바로 건너뛸 수 있습니다.")]
    public bool allowJumpToClearedNodes = false;

    [Header("칸 내용 감추기")]
    [Tooltip("체크하면 밟기 전까지 칸 내용을 물음표로 감춥니다.")]
    public bool hideTypeUntilVisited = true;

    [Header("씬 이름")]
    public string battleSceneName = "Battle Scene";
    [Tooltip("목적지 노드에 도달했을 때 넘어갈 씬")]
    public string goalSceneName = "CHAPTER 2";

    [Header("함정 설정")]
    [Range(0f, 1f)]
    [Tooltip("파티 전원이 최대 체력의 몇 퍼센트를 잃는지")]
    public float trapDamageRatio = 0.2f;

    [Header("체력 처리")]
    [Tooltip("목적지에 도달해 노드맵을 떠날 때 파티를 풀피로 되돌립니다.\n" +
             "끄면 닳은 체력 그대로 다음 챕터의 전투로 넘어갑니다. " +
             "(체력이 너무 적으면 다음 전투를 못 이길 수 있으니 주의하세요)")]
    public bool healToFullOnGoal = true;

    [Header("칸 색깔 — 클리어 / 이동 가능 / 미클리어 3가지")]
    [Tooltip("이미 깬 안전한 칸. 주인공이 밟고 있는 칸은 항상 여기에 들어갑니다.")]
    public Color colorCleared = new Color(0.62f, 0.85f, 0.62f, 1f);

    [Tooltip("지금 갈 수 있는 칸")]
    public Color colorReachable = new Color(0.95f, 0.95f, 0.95f, 1f);

    [Tooltip("아직 못 가는 칸")]
    public Color colorLocked = new Color(0.45f, 0.45f, 0.48f, 1f);

    public Color labelColor = new Color(0.12f, 0.12f, 0.14f, 1f);

    [Header("칸 아이콘 (선택)")]
    public Sprite unknownIcon;
    public Sprite battleIcon;
    public Sprite trapIcon;
    public Sprite clearedIcon;
    public Sprite goalIcon;

    [Header("좌표 자동 계산용 칸 간격")]
    public Vector2 cellSize = new Vector2(2f, 2f);

    [Header("UI (선택)")]
    public TextMeshProUGUI partyHpText;
    public TextMeshProUGUI hintText;

    private readonly Dictionary<Vector2Int, MapNode> nodes = new Dictionary<Vector2Int, MapNode>();
    private MapNode startNode;
    private bool busy;

    private Camera Cam => mapCamera != null ? mapCamera : Camera.main;

    // ─────────────────────────── 초기화 ───────────────────────────

    void Awake()
    {
        CollectNodes();
    }

    void Start()
    {
        StartCoroutine(BeginMap());
    }

    private void CollectNodes()
    {
        nodes.Clear();
        startNode = null;

        MapNode[] found = nodeRoot != null
            ? nodeRoot.GetComponentsInChildren<MapNode>(true)
            : FindObjectsByType<MapNode>(FindObjectsInactive.Include);

        foreach (var n in found)
        {
            if (n == null) continue;

            if (nodes.ContainsKey(n.coord))
            {
                Debug.LogWarning("[NodeMap] 좌표 " + n.coord + "가 겹칩니다: " + n.name +
                                 " — NodeMapManager 톱니 메뉴의 [월드 위치로 좌표 다시 계산]을 눌러보세요.");
                continue;
            }

            nodes[n.coord] = n;
            if (n.IsStart) startNode = n;
        }

        if (startNode == null && nodes.Count > 0)
        {
            Debug.LogWarning("[NodeMap] 시작 노드(Start)가 없어서 첫 번째 칸을 시작점으로 씁니다.");
            foreach (var kv in nodes) { startNode = kv.Value; break; }
        }
    }

    private IEnumerator BeginMap()
    {
        if (nodes.Count == 0)
        {
            Debug.LogError("[NodeMap] 씬에서 MapNode를 하나도 찾지 못했습니다.");
            yield break;
        }

        // 전투를 마치고 막 돌아온 참인지 확인합니다.
        if (NodeMapContext.returningFromBattle)
        {
            bool won = NodeMapContext.lastBattleWon;
            Vector2Int battleCoord = NodeMapContext.battleNodeCoord;
            NodeMapContext.returningFromBattle = false;

            if (won)
            {
                NodeMapContext.clearedCoords.Add(battleCoord);
                NodeMapContext.currentCoord = battleCoord;
                NodeMapContext.hasProgress = true;
                ShowHint("전투에서 승리했습니다.");
            }
            else
            {
                // 패배 → 진행 초기화, 시작점으로, 체력도 회복해서 처음부터 다시.
                NodeMapContext.ResetProgress();
                PartyState.ResetToFull();
                NodeMapContext.currentCoord = startNode.coord;
                NodeMapContext.clearedCoords.Add(startNode.coord);
                NodeMapContext.hasProgress = true;
                ShowHint("패배했습니다. 시작 지점부터 다시 시작합니다.");
            }
        }
        else if (!NodeMapContext.hasProgress)
        {
            NodeMapContext.ResetProgress();
            NodeMapContext.currentCoord = startNode.coord;
            NodeMapContext.clearedCoords.Add(startNode.coord);
            NodeMapContext.hasProgress = true;
        }

        RestoreProgress();
        SnapTokenTo(NodeMapContext.currentCoord);
        RefreshAll();

        yield return null;
    }

    /// <summary>static 진행 상황을 씬의 노드들에 반영합니다.</summary>
    private void RestoreProgress()
    {
        foreach (var kv in nodes)
        {
            kv.Value.isCleared = NodeMapContext.clearedCoords.Contains(kv.Key);
        }
    }

    private void MarkCleared(Vector2Int coord)
    {
        NodeMapContext.clearedCoords.Add(coord);
        if (nodes.TryGetValue(coord, out MapNode n)) n.isCleared = true;
    }

    // ─────────────────────────── 클릭 처리 ───────────────────────────

    void Update()
    {
        if (busy) return;
        if (Mouse.current == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        Camera cam = Cam;
        if (cam == null) return;

        Vector2 screen = Mouse.current.position.ReadValue();
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(cam.transform.position.z)));
        world.z = 0f;

        Collider2D hit = Physics2D.OverlapPoint(world);
        if (hit == null) return;

        MapNode node = hit.GetComponentInParent<MapNode>();
        if (node == null) return;

        TryEnter(node);
    }

    private void TryEnter(MapNode node)
    {
        if (node.coord == NodeMapContext.currentCoord) return;

        if (!IsMovable(node))
        {
            ShowHint("옆 칸으로 한 칸씩만 이동할 수 있습니다.");
            return;
        }

        StartCoroutine(EnterNodeRoutine(node));
    }

    private bool IsMovable(MapNode node)
    {
        if (allowJumpToClearedNodes && node.isCleared) return true;

        Vector2Int d = node.coord - NodeMapContext.currentCoord;
        int ax = Mathf.Abs(d.x);
        int ay = Mathf.Abs(d.y);

        if (allowDiagonal) return Mathf.Max(ax, ay) == 1;
        return ax + ay == 1;
    }

    // ─────────────────────────── 칸 진입 ───────────────────────────

    private IEnumerator EnterNodeRoutine(MapNode node)
    {
        busy = true;
        ShowHint("");

        yield return StartCoroutine(MoveTokenTo(node));

        NodeMapContext.currentCoord = node.coord;

        // 이미 깬 칸이면 그냥 지나가기만 합니다.
        if (node.isCleared)
        {
            RefreshAll();
            busy = false;
            yield break;
        }

        switch (node.nodeType)
        {
            case MapNodeType.Goal:
                MarkCleared(node.coord);
                RefreshAll();
                yield return StartCoroutine(GoToGoalScene());
                yield break;

            case MapNodeType.Battle:
                RefreshAll();
                yield return StartCoroutine(GoToBattle(node));
                yield break;

            case MapNodeType.Trap:
                yield return StartCoroutine(SpringTrap(node));
                break;

            default: // Empty, Start
                MarkCleared(node.coord);
                break;
        }

        RefreshAll();
        busy = false;
    }

    private IEnumerator SpringTrap(MapNode node)
    {
        if (presenter != null)
        {
            presenter.trapSubLabel = "파티 전원 체력 -" + Mathf.RoundToInt(trapDamageRatio * 100f) + "%";

            // 💥 연출은 화면 위(UI)에서만 벌어집니다. 맵 위의 주인공 말은 밟은 칸에 그대로 서 있습니다.
            yield return StartCoroutine(presenter.PlayTrapEffect());
        }

        PartyState.DamageAllByMaxRatio(trapDamageRatio);
        MarkCleared(node.coord);
        yield return null;
    }

    private IEnumerator GoToBattle(MapNode node)
    {
        NodeMapContext.EnterBattle(SceneManager.GetActiveScene().name, node.coord);

        if (presenter != null)
        {
            yield return StartCoroutine(presenter.PlayBattleIntro());
        }

        yield return StartCoroutine(ScreenFader.TransitionTo(battleSceneName));
    }

    private IEnumerator GoToGoalScene()
    {
        NodeMapContext.LeaveMap();
        if (healToFullOnGoal) PartyState.ResetToFull();

        ShowHint("목적지 도달!");
        yield return new WaitForSeconds(0.6f);
        yield return StartCoroutine(ScreenFader.TransitionTo(goalSceneName));
    }

    // ─────────────────────────── 주인공 이동 ───────────────────────────

    private Vector3 TokenPositionFor(Vector2Int coord)
    {
        if (nodes.TryGetValue(coord, out MapNode n)) return n.transform.position + tokenOffset;
        return playerToken != null ? playerToken.position : Vector3.zero;
    }

    private void SnapTokenTo(Vector2Int coord)
    {
        if (playerToken == null) return;
        playerToken.position = TokenPositionFor(coord);
    }

    private IEnumerator MoveTokenTo(MapNode node)
    {
        if (playerToken == null) yield break;

        Vector3 target = node.transform.position + tokenOffset;

        if (playerTokenSprite != null)
        {
            // AutoDirector와 같은 규칙: 오른쪽으로 갈 때 flipX = true
            if (target.x > playerToken.position.x) playerTokenSprite.flipX = true;
            else if (target.x < playerToken.position.x) playerTokenSprite.flipX = false;
        }

        while (Vector2.Distance(playerToken.position, target) > 0.02f)
        {
            playerToken.position = Vector3.MoveTowards(playerToken.position, target, tokenMoveSpeed * Time.deltaTime);
            yield return null;
        }

        playerToken.position = target;
    }

    // ─────────────────────────── 표시 갱신 ───────────────────────────

    public void RefreshAll()
    {
        foreach (var kv in nodes)
        {
            MapNode n = kv.Value;
            bool isCurrent = n.coord == NodeMapContext.currentCoord;

            // 💥 현재 위치는 주인공 스프라이트가 알려주므로 따로 색을 쓰지 않습니다.
            //    밟고 있는 칸은 언제나 깬 칸이라 클리어 색으로 보입니다.
            Color body;
            if (n.isCleared || isCurrent) body = colorCleared;
            else if (IsMovable(n)) body = colorReachable;
            else body = colorLocked;

            n.ApplyVisual(body, LabelFor(n), labelColor, IconFor(n));
        }

        if (partyHpText != null)
        {
            string s = PartyState.Summary();
            partyHpText.text = string.IsNullOrEmpty(s) ? "" : "체력   " + s;
        }
    }

    private string LabelFor(MapNode n)
    {
        if (n.IsStart) return "시작";
        if (n.IsGoal) return "목적지";
        if (n.isCleared) return "○";
        if (hideTypeUntilVisited) return "?";

        switch (n.nodeType)
        {
            case MapNodeType.Battle: return "전투";
            case MapNodeType.Trap: return "함정";
            default: return "";
        }
    }

    private Sprite IconFor(MapNode n)
    {
        if (n.IsGoal) return goalIcon;
        if (n.isCleared) return clearedIcon;
        if (hideTypeUntilVisited) return unknownIcon;

        switch (n.nodeType)
        {
            case MapNodeType.Battle: return battleIcon;
            case MapNodeType.Trap: return trapIcon;
            default: return null;
        }
    }

    private void ShowHint(string text)
    {
        if (hintText != null) hintText.text = text;
    }

    // ─────────────────────────── 에디터 편의 기능 ───────────────────────────

    /// <summary>
    /// 씬 뷰에서 블럭을 손으로 옮겨 맵을 그린 뒤 이걸 누르면
    /// 각 블럭의 월드 위치를 보고 coord를 자동으로 다시 매겨줍니다.
    /// </summary>
    [ContextMenu("월드 위치로 좌표 다시 계산")]
    public void RecalculateCoords()
    {
        MapNode[] found = nodeRoot != null
            ? nodeRoot.GetComponentsInChildren<MapNode>(true)
            : FindObjectsByType<MapNode>(FindObjectsInactive.Include);

        if (found.Length == 0)
        {
            Debug.LogWarning("[NodeMap] 좌표를 매길 MapNode가 없습니다.");
            return;
        }

        if (cellSize.x <= 0.001f || cellSize.y <= 0.001f)
        {
            Debug.LogError("[NodeMap] cellSize(칸 간격)가 0입니다. 블럭 사이 간격을 넣어주세요.");
            return;
        }

        float minX = float.MaxValue, minY = float.MaxValue;
        foreach (var n in found)
        {
            minX = Mathf.Min(minX, n.transform.position.x);
            minY = Mathf.Min(minY, n.transform.position.y);
        }

        foreach (var n in found)
        {
            int cx = Mathf.RoundToInt((n.transform.position.x - minX) / cellSize.x);
            int cy = Mathf.RoundToInt((n.transform.position.y - minY) / cellSize.y);
            n.coord = new Vector2Int(cx, cy);

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(n);
#endif
        }

        Debug.Log("[NodeMap] " + found.Length + "개 블럭의 좌표를 다시 매겼습니다.");
    }

    /// <summary>플레이 중이 아닐 때 인스펙터에서 색을 미리 확인하고 싶을 때.</summary>
    [ContextMenu("칸 색 미리보기 갱신")]
    public void PreviewRefresh()
    {
        CollectNodes();
        RestoreProgress();
        RefreshAll();
    }
}
