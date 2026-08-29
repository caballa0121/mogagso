using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>덩쿨이 사라질 때 이동하는 방향</summary>
public enum VineExitDirection
{
    Left,   // 좌측으로 밀려나며 사라짐
    Right,  // 우측으로 밀려나며 사라짐
    Up,     // 위로 걷히며 사라짐
    Down    // 아래로 가라앉으며 사라짐
}

/// <summary>
/// 복도를 막는 덩쿨 벽.
/// 플레이어가 덩쿨의 '어느 지점이든' 가까이 와서 E를 길게 누르면 해체됩니다.
///
/// 오브젝트 구성:
///   Vine01
///     ├ VineBarrier
///     ├ BoxCollider (여러 개여도 OK — 전부 자동으로 꺼집니다)
///     ├ Sprite      (SpriteRenderer + Billboard.cs)
///     └ Prompt      (E 아이콘. 평소엔 꺼둠 → Prompt Object에 연결)
/// </summary>
public class VineBarrier : MonoBehaviour
{
    // ==================================================================
    //  길막 콜라이더 (여러 개 지원)
    // ==================================================================
    [Header("── 길막 콜라이더 (isTrigger 해제 필수) ──")]
    [Tooltip("비워두면 자식의 모든 Collider를 자동으로 수집합니다")]
    public Collider[] blockColliders;

    [Header("── 연출 대상 스프라이트 ──")]
    [Tooltip("비워두면 자식 SpriteRenderer를 전부 사용합니다")]
    public SpriteRenderer[] vineRenderers;

    // ==================================================================
    //  사라지는 방향
    // ==================================================================
    [Header("── 해제 연출 ──")]
    [Tooltip("덩쿨이 어느 쪽으로 밀려나며 사라질지")]
    public VineExitDirection exitDirection = VineExitDirection.Left;

    [Tooltip("체크하면 오브젝트 자신의 축(빨강=Right) 기준, 해제하면 월드 축 기준")]
    public bool useLocalAxis = false;

    [Tooltip("사라지면서 이동하는 거리")]
    public float moveDistance = 3f;

    [Tooltip("사라지면서 줄어드는 비율 (1이면 크기 유지)")]
    public float shrinkTo = 0.8f;

    public float dissolveDuration = 1.0f;

    // ==================================================================
    //  E키 해체 상호작용
    // ==================================================================
    [Header("── 해체 상호작용 ──")]
    [Tooltip("BossSequence가 웨이브 시작 시 자동으로 켜줍니다")]
    public bool interactable = false;

    [Tooltip("비워두면 Player 태그로 자동 탐색")]
    public Transform playerTransform;

    [Tooltip("덩쿨의 가로 길이. 0이면 스프라이트 크기에서 자동 계산")]
    public float interactWidth = 0f;

    [Tooltip("덩쿨 표면에서 이 거리 안으로 들어오면 해체 가능")]
    public float interactDistance = 2.5f;

    public KeyCode dismantleKey = KeyCode.E;

    [Tooltip("키를 누르고 있어야 하는 시간(초)")]
    public float dismantleTime = 0.5f;

    [Tooltip("체크하면 손을 떼는 순간 진행도가 되돌아갑니다")]
    public bool resetOnRelease = true;

    // ==================================================================
    //  E 아이콘
    // ==================================================================
    [Header("── E 아이콘 / 게이지 ──")]
    [Tooltip("가까이 갔을 때 켜질 'E' 아이콘 오브젝트")]
    public GameObject promptObject;

    [Tooltip("체크하면 아이콘이 플레이어의 좌우 움직임을 따라다닙니다")]
    public bool promptFollowPlayer = true;

    [Tooltip("아이콘이 덩쿨 위로 떠 있는 높이")]
    public float promptHeight = 1.5f;

    [Tooltip("아이콘이 따라오는 부드러움 (클수록 빠름)")]
    public float promptFollowSpeed = 12f;

    [Tooltip("해체 진행도 게이지 (Image Type = Filled). 선택 사항")]
    public Image progressFill;

    [Tooltip("해체 중 아이콘이 커지는 연출")]
    public bool pulsePrompt = true;

    // ==================================================================
    [Header("── 디버그 ──")]
    public bool debugLog = false;
    [SerializeField] private float dismantleProgress = 0f;   // 인스펙터 실시간 확인용
    [SerializeField] private bool playerInRange = false;     // 인스펙터 실시간 확인용

    private bool isUnlocked = false;
    private bool isDissolving = false;
    private Vector3 originalPos;
    private Vector3 originalScale;
    private Vector3 promptOriginalScale = Vector3.one;
    private float halfWidth = 1f;

    public bool DismantleCompleted => isUnlocked;
    public bool IsAnimating => isDissolving;

    // ==================================================================
    //  초기화
    // ==================================================================

    private void Awake()
    {
        // --- 콜라이더 전부 수집 (문제 2 해결) ---
        if (blockColliders == null || blockColliders.Length == 0)
        {
            blockColliders = GetComponentsInChildren<Collider>(true);
        }

        if (vineRenderers == null || vineRenderers.Length == 0)
            vineRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        originalPos = transform.position;
        originalScale = transform.localScale;

        // --- 덩쿨 가로 길이 계산 (문제 4 해결) ---
        halfWidth = ComputeHalfWidth();

        if (promptObject != null)
        {
            promptOriginalScale = promptObject.transform.localScale;
            promptObject.SetActive(false);
        }
        if (progressFill != null) progressFill.fillAmount = 0f;

        if (debugLog)
        {
            Debug.Log($"[{name}] 콜라이더 {blockColliders.Length}개 수집, 반폭 {halfWidth:F2}", this);
        }
    }

    private void Start()
    {
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) playerTransform = p.transform;
            else Debug.LogError($"[{name}] Player 태그 오브젝트를 찾지 못했습니다!", this);
        }
    }

    /// <summary>덩쿨의 가로 반경을 구합니다. interactWidth가 0이면 스프라이트/콜라이더에서 자동 계산.</summary>
    private float ComputeHalfWidth()
    {
        if (interactWidth > 0f) return interactWidth * 0.5f;

        Vector3 axis = GetWidthAxis();
        float maxExtent = 0f;

        // 콜라이더 bounds 우선
        if (blockColliders != null)
        {
            foreach (Collider c in blockColliders)
            {
                if (c == null) continue;
                float e = Mathf.Abs(Vector3.Dot(c.bounds.extents, axis));
                float offset = Mathf.Abs(Vector3.Dot(c.bounds.center - transform.position, axis));
                maxExtent = Mathf.Max(maxExtent, e + offset);
            }
        }

        // 콜라이더가 없으면 스프라이트에서
        if (maxExtent <= 0.01f && vineRenderers != null)
        {
            foreach (SpriteRenderer sr in vineRenderers)
            {
                if (sr == null) continue;
                float e = Mathf.Abs(Vector3.Dot(sr.bounds.extents, axis));
                float offset = Mathf.Abs(Vector3.Dot(sr.bounds.center - transform.position, axis));
                maxExtent = Mathf.Max(maxExtent, e + offset);
            }
        }

        return Mathf.Max(0.5f, maxExtent);
    }

    // ==================================================================
    //  상태 제어 (BossSequence가 호출)
    // ==================================================================

    /// <summary>전투 시작 / 웨이브 재시작 시 다시 막아둡니다.</summary>
    public void Lock()
    {
        StopAllCoroutines();

        isUnlocked = false;
        isDissolving = false;
        interactable = false;
        dismantleProgress = 0f;
        playerInRange = false;

        gameObject.SetActive(true);
        transform.position = originalPos;
        transform.localScale = originalScale;

        SetCollidersEnabled(true);
        SetRenderersEnabled(true);
        SetAlpha(1f);
        ShowPrompt(false);
        if (progressFill != null) progressFill.fillAmount = 0f;
    }

    public void EnableDismantle(Transform player = null)
    {
        if (player != null) playerTransform = player;
        interactable = true;
        dismantleProgress = 0f;

        if (debugLog) Debug.Log($"[{name}] 해체 가능 상태로 전환", this);
    }

    public void DisableDismantle()
    {
        interactable = false;
        dismantleProgress = 0f;
        playerInRange = false;
        ShowPrompt(false);
        if (progressFill != null) progressFill.fillAmount = 0f;
    }

    // ==================================================================
    //  E키 해체 처리
    // ==================================================================

    private void Update()
    {
        if (isUnlocked || !interactable || playerTransform == null) return;

        // 대화 중에는 E가 대사 넘기기로 쓰이므로 차단
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
        {
            ShowPrompt(false);
            return;
        }

        // --- 덩쿨 전체를 선분으로 보고 가장 가까운 지점을 구함 (문제 3, 4 해결) ---
        Vector3 nearestPoint = GetNearestPointOnVine(playerTransform.position);
        float dist = Vector3.Distance(nearestPoint, playerTransform.position);
        playerInRange = dist <= interactDistance;

        ShowPrompt(playerInRange, nearestPoint);

        if (!playerInRange)
        {
            if (resetOnRelease) dismantleProgress = 0f;
            UpdateProgressUI();
            return;
        }

        if (Input.GetKey(dismantleKey))
        {
            dismantleProgress += Time.deltaTime;

            if (dismantleProgress >= dismantleTime)
            {
                CompleteDismantle();
                return;
            }
        }
        else if (resetOnRelease)
        {
            dismantleProgress = Mathf.MoveTowards(dismantleProgress, 0f, Time.deltaTime * 2f);
        }

        UpdateProgressUI();
    }

    /// <summary>덩쿨 가로 선분 위에서 플레이어와 가장 가까운 점</summary>
    private Vector3 GetNearestPointOnVine(Vector3 playerPos)
    {
        Vector3 axis = GetWidthAxis();
        Vector3 center = transform.position;

        float along = Vector3.Dot(playerPos - center, axis);
        along = Mathf.Clamp(along, -halfWidth, halfWidth);

        return center + axis * along;
    }

    private void CompleteDismantle()
    {
        if (isUnlocked) return;

        isUnlocked = true;
        interactable = false;
        dismantleProgress = dismantleTime;

        // 콜라이더 '전부' 즉시 해제 (문제 2 해결)
        SetCollidersEnabled(false);

        ShowPrompt(false);
        if (progressFill != null) progressFill.fillAmount = 1f;

        Debug.Log($"[{name}] 해체 완료 — 콜라이더 {CountColliders()}개 비활성화", this);

        StartCoroutine(DissolveRoutine());
    }

    // ==================================================================
    //  사라지는 연출
    // ==================================================================

    private IEnumerator DissolveRoutine()
    {
        isDissolving = true;

        if (PianoFX.Instance != null)
            PianoFX.Instance.Flash(new Color(0.5f, 1f, 0.6f), 0.3f, 0.4f);

        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + GetExitVector() * moveDistance;
        Vector3 startScale = transform.localScale;
        Vector3 endScale = startScale * shrinkTo;

        float elapsed = 0f;
        float dur = Mathf.Max(0.05f, dissolveDuration);

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float eased = 1f - (1f - t) * (1f - t);

            transform.position = Vector3.Lerp(startPos, endPos, eased);
            transform.localScale = Vector3.Lerp(startScale, endScale, t);
            SetAlpha(1f - t);

            yield return null;
        }

        SetAlpha(0f);
        SetRenderersEnabled(false);   // 오브젝트는 켜둔 채 렌더러만 끔

        isDissolving = false;
    }

    // ==================================================================
    //  축 계산
    // ==================================================================

    /// <summary>덩쿨의 가로 방향 (좌우로 뻗은 축)</summary>
    private Vector3 GetWidthAxis()
    {
        if (useLocalAxis) return transform.right;
        return Vector3.right;   // 월드 X축
    }

    /// <summary>exitDirection을 실제 방향 벡터로 변환</summary>
    private Vector3 GetExitVector()
    {
        if (useLocalAxis)
        {
            switch (exitDirection)
            {
                case VineExitDirection.Left:  return -transform.right;
                case VineExitDirection.Right: return transform.right;
                case VineExitDirection.Up:    return transform.up;
                default:                      return -transform.up;
            }
        }

        switch (exitDirection)
        {
            case VineExitDirection.Left:  return Vector3.left;
            case VineExitDirection.Right: return Vector3.right;
            case VineExitDirection.Up:    return Vector3.up;
            default:                      return Vector3.down;
        }
    }

    // ==================================================================
    //  표시 헬퍼
    // ==================================================================

    private void ShowPrompt(bool on, Vector3 anchorPoint = default)
    {
        if (promptObject == null) return;

        if (promptObject.activeSelf != on) promptObject.SetActive(on);
        if (!on)
        {
            promptObject.transform.localScale = promptOriginalScale;
            return;
        }

        // 플레이어의 좌우 위치를 따라 이동 (문제 3 해결)
        if (promptFollowPlayer)
        {
            Vector3 target = anchorPoint + Vector3.up * promptHeight;

            promptObject.transform.position = Vector3.Lerp(
                promptObject.transform.position,
                target,
                Time.deltaTime * promptFollowSpeed);
        }

        if (pulsePrompt)
        {
            float t = Mathf.Clamp01(dismantleProgress / Mathf.Max(0.01f, dismantleTime));
            promptObject.transform.localScale = promptOriginalScale * (1f + t * 0.4f);
        }
    }

    private void UpdateProgressUI()
    {
        if (progressFill != null)
            progressFill.fillAmount = Mathf.Clamp01(dismantleProgress / Mathf.Max(0.01f, dismantleTime));
    }

    private void SetAlpha(float a)
    {
        if (vineRenderers == null) return;
        foreach (SpriteRenderer sr in vineRenderers)
        {
            if (sr == null) continue;
            Color c = sr.color;
            c.a = a;
            sr.color = c;
        }
    }

    private void SetRenderersEnabled(bool on)
    {
        if (vineRenderers == null) return;
        foreach (SpriteRenderer sr in vineRenderers)
        {
            if (sr != null) sr.enabled = on;
        }
    }

    private void SetCollidersEnabled(bool on)
    {
        if (blockColliders == null) return;
        foreach (Collider c in blockColliders)
        {
            if (c != null) c.enabled = on;
        }
    }

    private int CountColliders()
    {
        if (blockColliders == null) return 0;
        int n = 0;
        foreach (Collider c in blockColliders) if (c != null) n++;
        return n;
    }

    // ==================================================================

    private void OnDrawGizmosSelected()
    {
        Vector3 axis = GetWidthAxis();
        float hw = Application.isPlaying ? halfWidth
                 : (interactWidth > 0f ? interactWidth * 0.5f : 1f);

        Vector3 a = transform.position - axis * hw;
        Vector3 b = transform.position + axis * hw;

        // 상호작용 가능 구역 (캡슐 형태)
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawWireSphere(a, interactDistance);
        Gizmos.DrawWireSphere(b, interactDistance);

        // 사라질 방향
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, GetExitVector() * moveDistance);
    }
}
