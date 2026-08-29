using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// 가까이서 E를 누르면 대화를 걸고, 대화가 끝나면 플레이어를 다른 곳으로 옮깁니다.
///
/// 이 게임은 CharacterController로 움직이는 3D이므로
/// 이동 처리도 3D 기준(Physics.SyncTransforms, Rigidbody)으로 되어 있어야 합니다.
/// </summary>
public class NPCInteraction : MonoBehaviour
{
    [Header("대화 데이터 설정")]
    public DialogueData dialogueData;

    [Header("대화 후 이동 설정")]
    public bool useTeleport = false;       // 대화 후 이동 여부
    public Transform playerTransform;      // 이동할 플레이어
    public Transform destinationTransform; // 이동할 목표 위치
    public CanvasGroup fadePanel;          // Fade Panel UI의 CanvasGroup

    [Tooltip("체크하면 목적지의 Z를 무시하고 플레이어의 현재 Z를 유지합니다.\n" +
             "2D 게임에서 스프라이트 깊이를 지킬 때만 쓰세요. 3D에서는 반드시 꺼두어야 합니다.")]
    public bool keepPlayerZ = false;

    [Header("플레이어 이동 스크립트 (선택)")]
    [Tooltip("텔레포트 중 플레이어 이동 스크립트를 잠시 꺼서 좌표 덮어쓰기를 방지합니다.\n" +
             "비워두면 플레이어에서 PlayerController를 자동으로 찾습니다.")]
    public MonoBehaviour playerMovementScript;

    [Header("시네머신 카메라 전환 설정")]
    public GameObject currentVCam;         // 꺼줄 현재 버추얼 카메라
    public GameObject targetVCam;          // 켜줄 목표 버추얼 카메라

    [Header("거리 기반 상호작용 설정")]
    public float interactDistance = 2.0f;  // 상호작용 가능 범위

    [Header("암전 연출")]
    public float fadeDuration = 0.4f;
    [Tooltip("완전히 어두워진 뒤 이동하기까지 기다리는 시간")]
    public float blackoutHold = 0.15f;

    [Header("디버그용 (확인 전용)")]
    [SerializeField] private bool isPlayerNearby = false;
    private bool isTeleporting = false;

    void Start()
    {
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        // 💥 이동 스크립트 자동 탐색.
        //    예전에는 GetComponent<MonoBehaviour>() 로 '아무 스크립트나' 잡았습니다.
        //    그러면 PlayerController가 아니라 PlayerHealth 같은 게 잡혀서
        //    이동이 안 꺼진 채 텔레포트가 실행되고, CharacterController가 좌표를 되돌려버립니다.
        if (playerMovementScript == null && playerTransform != null)
        {
            playerMovementScript = playerTransform.GetComponentInChildren<PlayerController>();

            if (playerMovementScript == null)
            {
                Debug.LogWarning($"[{gameObject.name}] 플레이어에서 PlayerController를 찾지 못했습니다. " +
                                 "인스펙터의 Player Movement Script 칸에 이동 스크립트를 직접 넣어주세요.");
            }
        }

        // 연결 누락은 실행 전에 미리 알려줍니다.
        if (useTeleport)
        {
            if (destinationTransform == null)
                Debug.LogError($"[{gameObject.name}] Destination Transform이 비어 있어 텔레포트가 동작하지 않습니다.");

            if (fadePanel == null)
                Debug.LogWarning($"[{gameObject.name}] Fade Panel이 비어 있어 암전 없이 즉시 이동합니다. " +
                                 "화면 전환 연출을 원하시면 Fade Panel의 CanvasGroup을 연결해 주세요.");
        }
    }

    void Update()
    {
        if (playerTransform == null || isTeleporting) return;

        // 💥 3D 게임이므로 Vector3로 재야 합니다.
        //    Vector2.Distance는 Z를 버려서, XZ 평면을 걷는 이 게임에서는 사실상 X 차이만 재게 됩니다.
        float distance = Vector3.Distance(transform.position, playerTransform.position);
        isPlayerNearby = (distance <= interactDistance);

        if (isPlayerNearby && Input.GetKeyDown(KeyCode.E))
        {
            if (DialogueManager.Instance == null || DialogueManager.Instance.IsDialogueActive) return;

            if (dialogueData == null)
            {
                Debug.LogWarning($"[{gameObject.name}] DialogueData가 할당되지 않았습니다.");
                return;
            }

            if (useTeleport)
            {
                DialogueManager.Instance.StartDialogue(dialogueData, OnDialogueEnd);
            }
            else
            {
                DialogueManager.Instance.StartDialogue(dialogueData);
            }
        }
    }

    private void OnDialogueEnd()
    {
        if (useTeleport)
        {
            StartCoroutine(TeleportSequence());
        }
    }

    private IEnumerator TeleportSequence()
    {
        isTeleporting = true;

        // 1. 플레이어 이동 스크립트 잠시 비활성화 (좌표 덮어쓰기 방지)
        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = false;
        }

        // 2. 화면 암전 (Fade Out)
        if (fadePanel != null)
        {
            yield return StartCoroutine(Fade(1f));
            fadePanel.alpha = 1f; // 확실하게 완전 암전으로 고정
        }

        if (blackoutHold > 0f) yield return new WaitForSeconds(blackoutHold);

        // 3. 플레이어 강제 이동
        if (playerTransform != null && destinationTransform != null)
        {
            // 최상위 루트 오브젝트 추출 (자식 오브젝트 변경 방지)
            Transform rootPlayer = playerTransform.root;

            // 💥 목적지를 그대로 씁니다.
            //    예전에는 targetPosition.z 를 플레이어의 현재 z로 덮어썼는데,
            //    XZ 평면을 걷는 3D 게임에서는 앞뒤 축을 지워버리는 셈이라
            //    플레이어가 목표 지점에 영영 도착하지 못했습니다.
            Vector3 targetPosition = destinationTransform.position;
            if (keepPlayerZ) targetPosition.z = rootPlayer.position.z;

            Debug.Log($"[텔레포트 시도] 현재 위치: {rootPlayer.position} -> 목표 위치: {targetPosition}");

            // 💥 CharacterController는 자기 위치를 따로 들고 있어서,
            //    켜둔 채로 transform만 바꾸면 되돌려놓을 수 있습니다. 껐다가 옮기고 다시 켭니다.
            CharacterController cc = rootPlayer.GetComponentInChildren<CharacterController>();
            bool ccWasEnabled = false;
            if (cc != null)
            {
                ccWasEnabled = cc.enabled;
                cc.enabled = false;
            }

            // 물리 속도 리셋 (3D)
            Rigidbody rb = rootPlayer.GetComponentInChildren<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            Vector3 previousPosition = rootPlayer.position;

            rootPlayer.position = targetPosition;
            Physics.SyncTransforms();

            if (cc != null) cc.enabled = ccWasEnabled;

            Debug.Log($"[텔레포트 완료] 실제 이동 후 위치: {rootPlayer.position}");

            // 💥 시네머신 카메라 교체.
            //    씬의 vcam들이 전부 Priority 10으로 켜져 있어서, SetActive만으로는
            //    어느 카메라가 화면을 잡을지 정해지지 않습니다. (이미 켜져 있는 카메라에
            //    SetActive(true)를 불러봐야 아무 일도 안 일어납니다)
            //    CameraSwitcher가 목표 카메라의 우선순위를 확실히 올려줍니다.
            CameraSwitcher.SwitchTo(targetVCam, currentVCam);

            // 💥 카메라에게 "이건 달린 게 아니라 순간이동이다"라고 알려줍니다.
            //    이 알림이 없으면 Damping 때문에 카메라가 옛 위치에서 새 위치까지
            //    미끄러져 오고, 암전이 걷힌 뒤에도 아직 날아오는 중이라
            //    화면이 안 바뀐 것처럼 보입니다.
            CameraSwitcher.NotifyWarp(rootPlayer, targetPosition - previousPosition);

            // 💥 새 카메라 구도가 실제로 그려진 뒤에 화면을 밝힙니다.
            //    이걸 안 하면 밝아지는 도중에 이전 구도가 한 프레임 비칩니다.
            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(0.15f);
        }
        else
        {
            if (playerTransform == null) Debug.LogError($"[{gameObject.name}] PlayerTransform이 연결되지 않았습니다.");
            if (destinationTransform == null) Debug.LogError($"[{gameObject.name}] DestinationTransform이 연결되지 않았습니다.");
        }

        // 4. 화면 밝아짐 (Fade In)
        if (fadePanel != null)
        {
            yield return StartCoroutine(Fade(0f));
            fadePanel.alpha = 0f;
            fadePanel.blocksRaycasts = false;
        }

        // 5. 플레이어 이동 스크립트 다시 활성화
        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = true;
        }

        isTeleporting = false;
    }

    private IEnumerator Fade(float targetAlpha)
    {
        if (fadePanel == null) yield break;

        float startAlpha = fadePanel.alpha;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, fadeDuration);

        fadePanel.blocksRaycasts = (targetAlpha > 0f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            fadePanel.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        fadePanel.alpha = targetAlpha;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}