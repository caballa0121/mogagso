using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;

    [Header("UI 연결")]
    public CanvasGroup dialogueBoxCanvasGroup; // 말풍선 전용 CanvasGroup
    public RectTransform portraitRect;         // 캐릭터 일러스트 RectTransform
    public CanvasGroup portraitCanvasGroup;    // 캐릭터 일러스트 CanvasGroup
    public Image portraitImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI dialogueText;

    [Header("컷씬 연결")]
    [Tooltip("컷씬 패널의 CanvasGroup (알파 0으로 시작)")]
    public CanvasGroup cutsceneCanvasGroup;
    [Tooltip("컷씬 이미지를 표시할 Image")]
    public Image cutsceneImage;
    [Tooltip("컷씬이 나타나고 사라지는 속도(초)")]
    public float cutsceneFadeDuration = 0.4f;
    [Tooltip("컷씬을 닫는 키")]
    public KeyCode cutsceneCloseKey = KeyCode.E;
    [Tooltip("체크하면 Space로도 컷씬을 닫을 수 있습니다")]
    public bool cutsceneAllowSpace = true;

    [Header("애니메이션 설정")]
    public float animDuration = 0.35f;   // 등장/퇴장 연출 시간
    public float portraitHiddenX = -700f; // 왼쪽 화면 밖 X 위치
    public float portraitShownX = 50f;    // 등장 시 화면 안 X 위치
    public float typingSpeed = 0.04f;    // 타이핑 속도

    private DialogueData currentDialogue;
    private Coroutine typingCoroutine;
    private Action onDialogueEndCallback; // 대화 종료 시 실행될 콜백 이벤트
    private int currentLineIndex = 0;
    private bool isTyping = false;
    private bool isDialogueActive = false;

    private Sprite lastPortraitSprite = null; // 이전 화자 추적용
    private Coroutine portraitAnimCoroutine;
    private Coroutine fadeAnimCoroutine;

    // --- 컷씬 상태 ---
    private bool isCutscenePlaying = false;      // 컷씬 중 대사 입력 차단용
    private bool beforeCutsceneConsumed = false; // 선행 컷씬 중복 재생 방지용
    private Coroutine cutsceneCoroutine;

    public bool IsDialogueActive => isDialogueActive;

    /// <summary>컷씬이 재생 중인지 (외부에서 플레이어 조작 차단 등에 활용)</summary>
    public bool IsCutscenePlaying => isCutscenePlaying;

    private bool isJustStarted = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 필수 UI 컴포넌트 안전성 검증
        if (dialogueBoxCanvasGroup != null)
        {
            dialogueBoxCanvasGroup.alpha = 0f;
            dialogueBoxCanvasGroup.blocksRaycasts = false;
        }
        else
        {
            Debug.LogError("[DialogueManager] DialogueBox CanvasGroup이 인스펙터에 연결되지 않았습니다!");
        }

        if (portraitCanvasGroup != null)
        {
            portraitCanvasGroup.alpha = 0f;
            portraitCanvasGroup.blocksRaycasts = false;
        }
        else
        {
            Debug.LogError("[DialogueManager] Portrait CanvasGroup이 인스펙터에 연결되지 않았습니다!");
        }

        if (portraitRect != null)
        {
            SetPortraitX(portraitHiddenX);
        }
        else
        {
            Debug.LogError("[DialogueManager] Portrait RectTransform이 인스펙터에 연결되지 않았습니다!");
        }

        // 컷씬 패널 초기화 (연결하지 않아도 대화는 정상 동작합니다)
        if (cutsceneCanvasGroup != null)
        {
            cutsceneCanvasGroup.alpha = 0f;
            cutsceneCanvasGroup.blocksRaycasts = false;
        }

        isDialogueActive = false;
    }

    void Update()
    {
        if (!isDialogueActive) return;

        // 컷씬 재생 중에는 대사 넘기기 입력을 차단합니다 (컷씬 코루틴이 입력을 직접 처리)
        if (isCutscenePlaying) return;

        // 대화가 시작된 첫 프레임에는 키 입력을 무시하여 즉시 넘어가는 현상 방지
        if (isJustStarted)
        {
            isJustStarted = false;
            return;
        }

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
        {
            if (isTyping)
            {
                if (typingCoroutine != null) StopCoroutine(typingCoroutine);

                if (currentDialogue != null && currentDialogue.lines != null && currentLineIndex < currentDialogue.lines.Length)
                {
                    dialogueText.text = currentDialogue.lines[currentLineIndex].dialogueText;
                }
                isTyping = false;
                typingCoroutine = null;
            }
            else
            {
                // 방금 읽은 줄에 '후행 컷씬'이 있으면 먼저 재생한 뒤 다음 줄로
                if (HasTrailingCutscene(currentLineIndex))
                {
                    StartCutscene(PlayCutsceneThenAdvance());
                    return;
                }

                AdvanceLine();
            }
        }
    }

    public void StartDialogue(DialogueData data, Action onEnd = null)
    {
        if (data == null || data.lines == null || data.lines.Length == 0)
        {
            Debug.LogError("[DialogueManager] 넘겨받은 DialogueData가 없거나 Lines 데이터가 비어있습니다!");
            return;
        }

        currentDialogue = data;
        currentLineIndex = 0;
        isDialogueActive = true;
        isJustStarted = true;
        lastPortraitSprite = null;
        onDialogueEndCallback = onEnd;

        // 이전 대화의 컷씬이 남아있을 경우 정리
        StopCutsceneImmediate();

        StartDialogueBoxFade(true);
        ShowNextLine();
    }

    private void ShowNextLine()
    {
        if (currentDialogue == null || currentDialogue.lines == null || currentLineIndex >= currentDialogue.lines.Length)
        {
            EndDialogue();
            return;
        }

        DialogueLine line = currentDialogue.lines[currentLineIndex];

        // --- 선행 컷씬: 대사를 띄우기 전에 컷씬부터 재생 ---
        if (HasLeadingCutscene(currentLineIndex) && !beforeCutsceneConsumed)
        {
            StartCutscene(PlayCutsceneThenShowLine());
            return;
        }

        if (nameText != null) nameText.text = line.speakerName;

        // --- 캐릭터 등장 / 유지 / 전환 처리 ---
        if (line.portraitSprite != null)
        {
            if (lastPortraitSprite != line.portraitSprite)
            {
                if (portraitAnimCoroutine != null) StopCoroutine(portraitAnimCoroutine);
                portraitAnimCoroutine = StartCoroutine(AnimatePortrait(line.portraitSprite));
            }
        }
        else
        {
            if (lastPortraitSprite != null)
            {
                if (portraitAnimCoroutine != null) StopCoroutine(portraitAnimCoroutine);
                portraitAnimCoroutine = StartCoroutine(SlideOutPortrait());
            }
        }

        lastPortraitSprite = line.portraitSprite;

        // --- 타이핑 연출 ---
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }
        typingCoroutine = StartCoroutine(TypeSentence(line.dialogueText));
    }

    /// <summary>다음 줄로 넘어가거나, 마지막 줄이면 대화를 종료합니다.</summary>
    private void AdvanceLine()
    {
        currentLineIndex++;
        if (currentDialogue != null && currentDialogue.lines != null
            && currentLineIndex < currentDialogue.lines.Length)
        {
            ShowNextLine();
        }
        else
        {
            EndDialogue();
        }
    }

    public void EndDialogue()
    {
        isDialogueActive = false;
        lastPortraitSprite = null;

        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        isTyping = false;

        if (portraitAnimCoroutine != null) StopCoroutine(portraitAnimCoroutine);
        portraitAnimCoroutine = StartCoroutine(SlideOutPortrait());

        StartDialogueBoxFade(false);

        // 컷씬이 떠 있는 상태로 대화가 끝나는 경우를 대비한 강제 정리
        StopCutsceneImmediate();

        onDialogueEndCallback?.Invoke();
        onDialogueEndCallback = null;
    }

    // ==================================================================
    //  컷씬
    // ==================================================================

    /// <summary>해당 줄이 '대사를 읽은 후' 컷씬을 띄우는 줄인지</summary>
    private bool HasTrailingCutscene(int index)
    {
        if (currentDialogue == null || currentDialogue.lines == null) return false;
        if (index < 0 || index >= currentDialogue.lines.Length) return false;

        DialogueLine line = currentDialogue.lines[index];
        return line.cutsceneSprite != null && !line.cutsceneBeforeLine;
    }

    /// <summary>해당 줄이 '대사를 띄우기 전' 컷씬을 띄우는 줄인지</summary>
    private bool HasLeadingCutscene(int index)
    {
        if (currentDialogue == null || currentDialogue.lines == null) return false;
        if (index < 0 || index >= currentDialogue.lines.Length) return false;

        DialogueLine line = currentDialogue.lines[index];
        return line.cutsceneSprite != null && line.cutsceneBeforeLine;
    }

    /// <summary>
    /// 컷씬 코루틴을 시작합니다.
    /// 플래그를 '여기서' 켜기 때문에, StartCoroutine 직후 프레임부터 입력이 차단됩니다.
    /// </summary>
    private void StartCutscene(IEnumerator routine)
    {
        isCutscenePlaying = true;
        if (cutsceneCoroutine != null) StopCoroutine(cutsceneCoroutine);
        cutsceneCoroutine = StartCoroutine(routine);
    }

    /// <summary>컷씬을 즉시 닫고 상태를 초기화합니다.</summary>
    private void StopCutsceneImmediate()
    {
        if (cutsceneCoroutine != null)
        {
            StopCoroutine(cutsceneCoroutine);
            cutsceneCoroutine = null;
        }

        if (cutsceneCanvasGroup != null)
        {
            cutsceneCanvasGroup.alpha = 0f;
            cutsceneCanvasGroup.blocksRaycasts = false;
        }

        isCutscenePlaying = false;
        beforeCutsceneConsumed = false;
    }

    /// <summary>후행 컷씬: 재생이 끝나면 다음 줄로 넘어갑니다.</summary>
    private IEnumerator PlayCutsceneThenAdvance()
    {
        yield return PlayCutscene(GetCutsceneSprite(currentLineIndex));

        isCutscenePlaying = false;
        cutsceneCoroutine = null;

        AdvanceLine();
    }

    /// <summary>선행 컷씬: 재생이 끝나면 같은 줄의 대사로 이어집니다.</summary>
    private IEnumerator PlayCutsceneThenShowLine()
    {
        yield return PlayCutscene(GetCutsceneSprite(currentLineIndex));

        isCutscenePlaying = false;
        cutsceneCoroutine = null;

        // 컷씬을 이미 소비했음을 표시 → 재진입 시 대사로 바로 이어짐
        beforeCutsceneConsumed = true;
        ShowNextLine();
        beforeCutsceneConsumed = false;
    }

    private Sprite GetCutsceneSprite(int index)
    {
        if (currentDialogue == null || currentDialogue.lines == null) return null;
        if (index < 0 || index >= currentDialogue.lines.Length) return null;
        return currentDialogue.lines[index].cutsceneSprite;
    }

    /// <summary>컷씬 페이드 인 → 입력 대기 → 페이드 아웃</summary>
    private IEnumerator PlayCutscene(Sprite sprite)
    {
        // 컷씬 UI가 연결되지 않았으면 조용히 건너뜁니다 (대화는 그대로 진행)
        if (sprite == null || cutsceneCanvasGroup == null || cutsceneImage == null)
        {
            if (sprite != null)
            {
                Debug.LogWarning("[DialogueManager] 컷씬 이미지가 지정됐지만 " +
                                 "Cutscene Canvas Group / Cutscene Image가 연결되지 않았습니다.");
            }
            yield break;
        }

        // 1) 이미지 세팅 후 페이드 인
        cutsceneImage.sprite = sprite;
        cutsceneImage.enabled = true;
        cutsceneCanvasGroup.blocksRaycasts = true;
        yield return FadeCutscene(1f);

        // 2) 입력 대기 (컷씬이 뜬 그 프레임의 입력은 흘려보냅니다)
        yield return null;
        while (!IsCutsceneCloseInput())
        {
            yield return null;
        }

        // 3) 페이드 아웃
        yield return FadeCutscene(0f);
        cutsceneCanvasGroup.blocksRaycasts = false;
    }

    private bool IsCutsceneCloseInput()
    {
        if (Input.GetKeyDown(cutsceneCloseKey)) return true;
        if (cutsceneAllowSpace && Input.GetKeyDown(KeyCode.Space)) return true;
        return false;
    }

    private IEnumerator FadeCutscene(float targetAlpha)
    {
        if (cutsceneCanvasGroup == null) yield break;

        float start = cutsceneCanvasGroup.alpha;
        float elapsed = 0f;
        float dur = Mathf.Max(0.05f, cutsceneFadeDuration);

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            cutsceneCanvasGroup.alpha = Mathf.Lerp(start, targetAlpha, elapsed / dur);
            yield return null;
        }

        cutsceneCanvasGroup.alpha = targetAlpha;
    }

    // ==================================================================
    //  애니메이션 Coroutines
    // ==================================================================

    private IEnumerator AnimatePortrait(Sprite newSprite)
    {
        if (portraitCanvasGroup == null || portraitImage == null) yield break;

        if (portraitCanvasGroup.alpha > 0f)
        {
            yield return SlideOutPortrait();
        }

        portraitImage.sprite = newSprite;
        SetPortraitX(portraitHiddenX);

        float duration = Mathf.Max(0.01f, animDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            SetPortraitX(Mathf.Lerp(portraitHiddenX, portraitShownX, t));
            portraitCanvasGroup.alpha = t;
            yield return null;
        }

        SetPortraitX(portraitShownX);
        portraitCanvasGroup.alpha = 1f;
    }

    private IEnumerator SlideOutPortrait()
    {
        if (portraitCanvasGroup == null || portraitRect == null) yield break;

        float duration = Mathf.Max(0.01f, animDuration);
        float elapsed = 0f;
        float startX = portraitRect.anchoredPosition.x;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            SetPortraitX(Mathf.Lerp(startX, portraitHiddenX, t));
            portraitCanvasGroup.alpha = 1f - t;
            yield return null;
        }

        SetPortraitX(portraitHiddenX);
        portraitCanvasGroup.alpha = 0f;
    }

    private void StartDialogueBoxFade(bool fadeIn)
    {
        if (dialogueBoxCanvasGroup == null) return;
        if (fadeAnimCoroutine != null) StopCoroutine(fadeAnimCoroutine);
        fadeAnimCoroutine = StartCoroutine(FadeCanvasGroup(dialogueBoxCanvasGroup, fadeIn ? 1f : 0f));
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float targetAlpha)
    {
        if (cg == null) yield break;

        float duration = Mathf.Max(0.01f, animDuration);
        float startAlpha = cg.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        cg.alpha = targetAlpha;
        cg.blocksRaycasts = (targetAlpha > 0f);
    }

    private IEnumerator TypeSentence(string sentence)
    {
        if (dialogueText == null) yield break;

        dialogueText.text = "";
        isTyping = true;

        if (!string.IsNullOrEmpty(sentence))
        {
            foreach (char letter in sentence.ToCharArray())
            {
                dialogueText.text += letter;
                yield return new WaitForSeconds(typingSpeed);
            }
        }

        isTyping = false;
        typingCoroutine = null;
    }

    private void SetPortraitX(float x)
    {
        if (portraitRect == null) return;
        Vector2 pos = portraitRect.anchoredPosition;
        pos.x = x;
        portraitRect.anchoredPosition = pos;
    }
}
