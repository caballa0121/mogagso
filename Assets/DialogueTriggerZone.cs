using UnityEngine;

public class DialogueTriggerZone : MonoBehaviour
{
    [Header("대화 데이터 설정")]
    public DialogueData dialogueData;

    [Header("트리거 옵션")]
    [Tooltip("체크 시 씬에서 한 번만 대화가 실행됩니다.")]
    public bool triggerOnce = true;

    private bool hasTriggered = false;

    // 3D 콜라이더 충돌 감지 이벤트
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[{gameObject.name}] 3D 충돌 감지됨: {other.name} (태그: {other.tag})");

        if (hasTriggered && triggerOnce) return;

        if (other.CompareTag("Player"))
        {
            if (DialogueManager.Instance == null)
            {
                Debug.LogError("[DialogueTriggerZone] DialogueManager가 씬에 존재하지 않습니다!");
                return;
            }

            if (DialogueManager.Instance.IsDialogueActive)
            {
                Debug.LogWarning("[DialogueTriggerZone] 이미 대화가 진행 중입니다.");
                return;
            }

            if (dialogueData == null)
            {
                Debug.LogError($"[{gameObject.name}] DialogueData가 인스펙터에 연결되지 않았습니다!");
                return;
            }

            Debug.Log($"[{gameObject.name}] 조건 충족 -> 대화 시작!");
            hasTriggered = true;
            DialogueManager.Instance.StartDialogue(dialogueData);
        }
    }

    public void ResetTrigger()
    {
        hasTriggered = false;
    }
}