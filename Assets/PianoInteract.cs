using UnityEngine;

/// <summary>
/// 자유 연주용. 피아노 근처에서 키를 눌러 건반 UI를 여닫습니다.
///
/// ※ 기존에 A키를 쓰면 '도' 건반과 충돌해서 피아노를 여는 순간 소리가 났습니다.
///    이제 기본값은 E입니다. 건반에 매핑하지 않은 키를 쓰세요.
/// </summary>
public class PianoInteract : MonoBehaviour
{
    [Header("연동할 건반 UI")]
    public GameObject pianoUI;

    [Header("토글 키 (건반에 쓰지 않는 키로!)")]
    public KeyCode toggleKey = KeyCode.E;

    [Header("플레이어 이동 스크립트 (PlayerController를 드래그)")]
    public MonoBehaviour playerMovementScript;

    private bool isPlayerNearby = false;

    private void Update()
    {
        // 튜토리얼/보스가 피아노를 쓰는 중이면 개입하지 않습니다
        if (PianoSession.Busy) return;

        // 대화 중에는 E가 대사 넘기기로 쓰이므로 무시
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive) return;

        if (isPlayerNearby && Input.GetKeyDown(toggleKey))
        {
            if (pianoUI == null) return;

            bool isOpening = !pianoUI.activeSelf;
            pianoUI.SetActive(isOpening);
            TogglePlayerMovement(!isOpening);
        }
    }

    private void TogglePlayerMovement(bool enable)
    {
        if (playerMovementScript != null) playerMovementScript.enabled = enable;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;

            if (playerMovementScript == null)
            {
                // 아무 MonoBehaviour나 잡지 않도록 타입을 명시합니다
                playerMovementScript = other.GetComponentInParent<PlayerController>();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;

            if (PianoSession.Busy) return;

            if (pianoUI != null) pianoUI.SetActive(false);
            TogglePlayerMovement(true);
        }
    }
}
