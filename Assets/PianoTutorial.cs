using System.Collections;
using UnityEngine;
using TMPro;

[System.Serializable]
public class TutorialStep
{
    [Tooltip("이 단계 시작 시 나올 설명 대사")]
    public DialogueData introDialogue;

    [Tooltip("따라 쳐야 할 멜로디")]
    public MelodyData targetMelody;

    [Tooltip("체크 시 먼저 자동으로 들려줍니다")]
    public bool demonstrateFirst = true;

    [Tooltip("체크 시 눌러야 할 건반에 가이드 불빛을 켜줍니다")]
    public bool showGuide = true;

    [Tooltip("성공 후 나올 대사 (없으면 건너뜀)")]
    public DialogueData successDialogue;
}

/// <summary>
/// 튜토리얼 진행 컨트롤러.
/// '항상 켜져 있는' 오브젝트(예: TutorialNPC)에 붙입니다.
/// </summary>
public class PianoTutorial : MonoBehaviour
{
    [Header("튜토리얼 단계")]
    public TutorialStep[] steps;

    [Header("연결")]
    [Tooltip("피아노 건반 UI 패널 (PianoManager가 붙어있는 오브젝트)")]
    public GameObject pianoUI;
    [Tooltip("튜토리얼 중 끌 플레이어 이동 스크립트 (PlayerController를 드래그)")]
    public MonoBehaviour playerMovementScript;
    [Tooltip("흔들기 연출용. 피아노 패널의 RectTransform")]
    public RectTransform pianoPanelRect;

    [Header("진행 안내 UI (선택)")]
    public TextMeshProUGUI progressText;   // "3 / 5"
    public TextMeshProUGUI hintText;       // 멜로디의 hint 문구

    [Header("시작 조건")]
    [Tooltip("체크 시 플레이어가 가까이서 E를 누르면 시작합니다")]
    public bool startByInteract = true;
    public Transform playerTransform;
    public float interactDistance = 2.5f;
    [Tooltip("체크 시 한 번만 진행됩니다")]
    public bool onlyOnce = true;

    [Header("친절 설정")]
    [Tooltip("이 횟수만큼 틀리면 자동으로 멜로디를 다시 들려줍니다")]
    public int mistakesBeforeReplay = 3;

    private bool isRunning = false;
    private bool hasCompleted = false;
    private MelodyChecker activeChecker;

    /// <summary>튜토리얼을 모두 끝냈는지 (보스 입장 조건 등에 사용)</summary>
    public bool HasCompleted => hasCompleted;

    private void Start()
    {
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }
    }

    private void Update()
    {
        if (!startByInteract || isRunning) return;
        if (onlyOnce && hasCompleted) return;
        if (playerTransform == null) return;

        // 대화 중이면 무시
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive) return;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist <= interactDistance && Input.GetKeyDown(KeyCode.E))
        {
            StartTutorial();
        }
    }

    /// <summary>외부(NPC 대화 종료 콜백 등)에서 직접 호출해도 됩니다.</summary>
    public void StartTutorial()
    {
        if (isRunning) return;
        if (steps == null || steps.Length == 0)
        {
            Debug.LogWarning("[PianoTutorial] steps가 비어있습니다.");
            return;
        }
        StartCoroutine(RunTutorial());
    }

    private IEnumerator RunTutorial()
    {
        isRunning = true;
        PianoSession.Busy = true;
        SetPlayerMovement(false);

        if (PianoManager.Instance != null) PianoManager.Instance.WarmUpAudio();

        for (int i = 0; i < steps.Length; i++)
        {
            yield return RunStep(steps[i], i);
        }

        // 마무리
        ClosePiano();
        SetPlayerMovement(true);

        if (PianoFX.Instance != null)
            PianoFX.Instance.Toast("튜토리얼 완료!", 1.8f);

        hasCompleted = true;
        isRunning = false;
        PianoSession.Busy = false;
    }

    private void OnDisable()
    {
        // 씬 전환/오브젝트 파괴 시 구독이 남는 것을 방지
        if (activeChecker != null) activeChecker.Stop();
        PianoSession.Busy = false;
        if (PianoManager.Instance != null) PianoManager.Instance.inputLocked = false;
    }

    private IEnumerator RunStep(TutorialStep step, int stepNumber)
    {
        // ---------- 1. 설명 대사 ----------
        if (step.introDialogue != null)
        {
            ClosePiano();  // 대화 중에는 피아노를 닫아 입력 충돌 방지
            yield return RunDialogue(step.introDialogue);
        }

        if (step.targetMelody == null)
        {
            Debug.LogWarning($"[PianoTutorial] {stepNumber + 1}단계에 멜로디가 없습니다.");
            yield break;
        }

        // ---------- 2. 피아노 열기 ----------
        OpenPiano();
        yield return null; // UI 활성화 1프레임 대기

        if (hintText != null) hintText.text = step.targetMelody.hint;
        UpdateProgress(0, step.targetMelody.NoteCount);

        // ---------- 3. 시연 ----------
        if (step.demonstrateFirst)
        {
            yield return Demonstrate(step);
        }

        // ---------- 4. 따라치기 ----------
        bool cleared = false;
        int mistakes = 0;
        bool needReplay = false;

        MelodyChecker checker = new MelodyChecker();
        activeChecker = checker;
        int total = step.targetMelody.NoteCount;

        checker.OnCorrect += (idx) =>
        {
            UpdateProgress(idx + 1, total);
            RefreshGuide(checker, step.showGuide);
        };

        checker.OnWrong += (idx) =>
        {
            mistakes++;
            if (PianoFX.Instance != null)
            {
                StartCoroutine(PianoFX.Instance.WrongFeedback(checker.ExpectedKey()));
                PianoFX.Instance.Shake(pianoPanelRect, 12f, 0.2f);
            }
            if (mistakes >= mistakesBeforeReplay)
            {
                mistakes = 0;
                needReplay = true;
            }
        };

        checker.OnComplete += () => cleared = true;

        checker.Begin(step.targetMelody, false); // 튜토리얼: 틀려도 진행도 유지
        RefreshGuide(checker, step.showGuide);

        while (!cleared)
        {
            if (needReplay)
            {
                needReplay = false;
                checker.Stop();

                if (PianoFX.Instance != null) PianoFX.Instance.Toast("다시 들려드릴게요", 1.2f);
                yield return new WaitForSeconds(0.4f);
                yield return Demonstrate(step);

                checker.Begin(step.targetMelody, false);
                UpdateProgress(0, total);
                RefreshGuide(checker, step.showGuide);
            }
            yield return null;
        }

        checker.Stop();

        // ---------- 5. 성공 연출 ----------
        if (PianoManager.Instance != null) PianoManager.Instance.ClearAllGuides();

        if (PianoFX.Instance != null)
        {
            PianoFX.Instance.Flash(new Color(1f, 0.95f, 0.6f), 0.45f, 0.4f);
            PianoFX.Instance.Toast("좋아요!", 1.0f);
            yield return PianoFX.Instance.KeyRipple(0.025f, true);
        }
        yield return new WaitForSeconds(0.3f);

        // ---------- 6. 성공 대사 ----------
        if (step.successDialogue != null)
        {
            ClosePiano();
            yield return RunDialogue(step.successDialogue);
        }
    }

    private IEnumerator Demonstrate(TutorialStep step)
    {
        if (PianoManager.Instance == null) yield break;

        if (PianoFX.Instance != null) PianoFX.Instance.Toast("잘 들어보세요", 0.8f);
        yield return new WaitForSeconds(0.5f);

        PianoManager.Instance.inputLocked = true;
        yield return PianoManager.Instance.PlayMelody(step.targetMelody, true, 1f);
        PianoManager.Instance.inputLocked = false;

        if (PianoFX.Instance != null) PianoFX.Instance.Toast("따라 쳐보세요!", 1.0f);
        yield return new WaitForSeconds(0.3f);
    }

    // ---------- 헬퍼 ----------

    private IEnumerator RunDialogue(DialogueData data)
    {
        if (DialogueManager.Instance == null || data == null) yield break;

        bool done = false;
        DialogueManager.Instance.StartDialogue(data, () => done = true);
        yield return new WaitUntil(() => done);
        yield return new WaitForSeconds(0.2f); // 대화 종료 후 E키가 피아노에 튀는 것 방지
    }

    private void RefreshGuide(MelodyChecker checker, bool show)
    {
        if (PianoManager.Instance == null) return;
        PianoManager.Instance.ClearAllGuides();

        if (!show) return;
        int next = checker.ExpectedKey();
        if (next >= 0) PianoManager.Instance.SetGuide(next, true);
    }

    private void UpdateProgress(int current, int total)
    {
        if (progressText != null) progressText.text = $"{current} / {total}";
    }

    private void OpenPiano()
    {
        if (pianoUI != null && !pianoUI.activeSelf) pianoUI.SetActive(true);
    }

    private void ClosePiano()
    {
        if (PianoManager.Instance != null)
        {
            PianoManager.Instance.ClearAllGuides();
            PianoManager.Instance.ClearAllHighlights();
        }
        if (pianoUI != null && pianoUI.activeSelf) pianoUI.SetActive(false);
    }

    private void SetPlayerMovement(bool enable)
    {
        if (playerMovementScript != null) playerMovementScript.enabled = enable;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}
