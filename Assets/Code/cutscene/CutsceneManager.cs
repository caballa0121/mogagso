using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic; 
using UnityEngine.InputSystem; 

[System.Serializable]
public class StoryLine 
{
    public string speaker;      
    [TextArea(2, 5)] public string text;         
}

public enum AnimType { Slide, Fade }

// 화면에서 초상화가 서는 자리. 같은 자리를 쓰는 인물끼리는 겹치므로 서로 밀어냅니다.
public enum PortraitSlot { Auto, Left, Right }

[System.Serializable]
public class CharacterUI
{
    public string nameKeyword;
    public AnimType animType = AnimType.Slide;
    public GameObject portraitObj;
    public RectTransform rect;
    public Image image;
    public float animSpeed = 10f;
    public Vector2 visiblePos;
    public Vector2 hiddenPos;

    [Header("자리 배치")]
    [Tooltip("같은 자리에 있는 인물끼리는 동시에 서지 않고, 말하는 사람만 남습니다.\n" +
             "Auto면 visiblePos의 X 부호로 좌/우를 자동 판별합니다.")]
    public PortraitSlot slot = PortraitSlot.Auto;

    [Tooltip("체크하면 같은 자리에 다른 인물이 나와도 사라지지 않습니다. (주인공 고정용)")]
    public bool alwaysVisible = false;

    [System.NonSerialized] public bool isVisible = false;
    [System.NonSerialized] public Coroutine coroutine;
}

public class CutsceneManager : MonoBehaviour
{
    [HideInInspector] public bool isCutsceneDone = false; 

    [Header("UI 연결")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI dialogueText;
    
    [Header("등장 캐릭터 명단")]
    public List<CharacterUI> characters; 

    [Header("시스템 퀘스트 팝업")]
    public GameObject questUI;
    public TextMeshProUGUI questText;

    [Header("같은 자리 인물 교체")]
    [Tooltip("같은 자리의 인물이 바뀔 때 이전 인물을 즉시 감춥니다. 끄면 서서히 사라지지만 잠깐 겹쳐 보입니다.")]
    public bool instantSwapOnSameSlot = true;
    
    public StoryLine[] lines;
    
    private int currentIndex = 0;
    private bool isEnding = false;

    private Color brightColor = Color.white;
    private Color dimColor = new Color(0.4f, 0.4f, 0.4f, 1f); 

    void OnEnable()
    {
        isCutsceneDone = false;
        isEnding = false;
        currentIndex = 0;

        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (questUI != null) questUI.SetActive(false);

        foreach (var c in characters)
        {
            if (c.portraitObj != null && c.rect != null && c.image != null)
            {
                if (c.coroutine != null) { StopCoroutine(c.coroutine); c.coroutine = null; }

                c.portraitObj.SetActive(true);

                // 💥 Fade든 Slide든 항상 화면 밖(hiddenPos)에서 시작합니다.
                //    Fade를 visiblePos에 두면 알파만으로 숨기게 되어, 알파가 조금이라도
                //    남아있을 때 이전 컷씬의 인물이 화면에 비쳐 보입니다.
                c.rect.anchoredPosition = c.hiddenPos;

                c.image.color = new Color(1, 1, 1, (c.animType == AnimType.Slide) ? 1f : 0f);
                c.isVisible = false;
            }
        }

        if (lines != null && lines.Length > 0) ShowLine();
    }

    void Update()
    {
        // 💥 종료 처리가 시작된 뒤의 클릭은 무시합니다.
        //    (안 막으면 정리 도중 NextLine이 또 불려 퇴장 연출이 겹칩니다)
        if (isEnding || isCutsceneDone) return;

        bool isMouseClick = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool isSpacePress = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

        if (isMouseClick || isSpacePress) NextLine();
    }

    void NextLine()
    {
        currentIndex++;
        if (lines != null && currentIndex < lines.Length)
        {
            ShowLine();
        }
        else
        {
            isEnding = true;
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            if (questUI != null) questUI.SetActive(false);

            foreach (var c in characters)
            {
                SetPortraitState(c, false, false);
            }

            StartCoroutine(EndCutsceneDelay());
        }
    }

    IEnumerator EndCutsceneDelay()
    {
        yield return new WaitForSeconds(0.5f);

        // 💥 퇴장 애니메이션이 다 끝나기 전에 잘리면 반투명 상태로 남습니다.
        //    초상화는 컷씬끼리 공유하므로, 잔상을 확실히 지운 뒤 끕니다.
        foreach (var c in characters)
        {
            ForceHidePortrait(c);
            if (c.portraitObj != null) c.portraitObj.SetActive(false);
        }

        // 💥 정리를 마친 뒤에 완료를 알립니다.
        //    먼저 알리면 AutoDirector가 다음 컷씬을 켜버려서, 공유 초상화를 서로 뺏습니다.
        isCutsceneDone = true;
        gameObject.SetActive(false);
    }

    // 애니메이션을 끊고 화면 밖 + 완전 투명으로 즉시 되돌립니다.
    void ForceHidePortrait(CharacterUI c)
    {
        if (c.rect == null || c.image == null) return;

        if (c.coroutine != null) { StopCoroutine(c.coroutine); c.coroutine = null; }

        c.rect.anchoredPosition = c.hiddenPos;
        c.image.color = new Color(dimColor.r, dimColor.g, dimColor.b, 0f);
        c.isVisible = false;
    }

    void ShowLine()
    {
        if (lines == null || currentIndex >= lines.Length) return;
        var line = lines[currentIndex];

        if (line.speaker.Contains("시스템"))
        {
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            foreach (var c in characters) SetPortraitState(c, false, false); 
            
            if (questUI != null) questUI.SetActive(true);
            if (questText != null) questText.text = line.text;
            return; 
        }

        if (questUI != null) questUI.SetActive(false);
        if (dialoguePanel != null) dialoguePanel.SetActive(true); 
        if (nameText != null) nameText.text = line.speaker;
        if (dialogueText != null) dialogueText.text = line.text;

        // 💥 이번 대사의 화자를 먼저 찾습니다. (완전 일치로 비교하여 이름 충돌 방지)
        CharacterUI speaker = null;
        foreach (var c in characters)
        {
            if (line.speaker.Trim().Equals(c.nameKeyword.Trim()))
            {
                speaker = c;
                break;
            }
        }

        foreach (var c in characters)
        {
            if (c == speaker)
            {
                // 화자: 등장 + 밝게
                SetPortraitState(c, true, true);
            }
            else if (speaker != null && !c.alwaysVisible && GetSlot(c) == GetSlot(speaker))
            {
                // 💥 화자와 같은 자리에 선 인물은 겹치므로 완전히 퇴장시킵니다.
                //    교체는 즉시 처리해야 슬라이드 도중 두 초상화가 겹쳐 보이지 않습니다.
                if (c.isVisible) SetPortraitState(c, false, false, instantSwapOnSameSlot);
            }
            else if (c.isVisible)
            {
                // 반대쪽 자리의 인물: 남겨두고 어둡게
                SetPortraitState(c, true, false);
            }
        }
    }

    // 자리를 Auto로 두면 visiblePos의 X 부호로 좌/우를 판별합니다.
    PortraitSlot GetSlot(CharacterUI c)
    {
        if (c.slot != PortraitSlot.Auto) return c.slot;
        return (c.visiblePos.x < 0f) ? PortraitSlot.Left : PortraitSlot.Right;
    }

    void SetPortraitState(CharacterUI c, bool show, bool isSpeaking, bool instant = false)
    {
        if (c.rect == null || c.image == null) return;
        c.isVisible = show;

        if (c.coroutine != null) StopCoroutine(c.coroutine);

        if (instant)
        {
            // 애니메이션 없이 즉시 반영 — 같은 자리 교체 시 겹침을 확실히 없앱니다.
            Color target = isSpeaking ? brightColor : dimColor;
            c.rect.anchoredPosition = show ? c.visiblePos : c.hiddenPos;
            c.image.color = new Color(target.r, target.g, target.b, show ? 1f : 0f);
            c.coroutine = null;
            return;
        }

        c.coroutine = StartCoroutine(AnimatePortrait(c, show, isSpeaking));
    }

    IEnumerator AnimatePortrait(CharacterUI c, bool show, bool isSpeaking)
    {
        Vector2 targetPos = show ? c.visiblePos : c.hiddenPos;
        float targetAlpha = show ? 1f : 0f;
        Color targetColor = isSpeaking ? brightColor : dimColor;

        // 💥 등장할 때만 제자리로 옮깁니다.
        //    숨길 때도 visiblePos로 옮기면, 화면 밖에 있던 인물이 다시 화면으로
        //    끌려와 한 번 번쩍 보였다가 사라집니다.
        if (c.animType == AnimType.Fade && show) c.rect.anchoredPosition = c.visiblePos;

        while (true)
        {
            bool isPosDone = true;
            bool isColorDone = true;

            if (c.animType == AnimType.Slide)
            {
                if (Vector2.Distance(c.rect.anchoredPosition, targetPos) > 1f)
                {
                    c.rect.anchoredPosition = Vector2.Lerp(c.rect.anchoredPosition, targetPos, Time.deltaTime * c.animSpeed);
                    isPosDone = false;
                }
                else c.rect.anchoredPosition = targetPos;
            }

            float currentAlpha = c.image.color.a;
            Color nextColor = Color.Lerp(c.image.color, targetColor, Time.deltaTime * c.animSpeed);
            
            float nextAlpha = (c.animType == AnimType.Fade || !show) ? Mathf.Lerp(currentAlpha, targetAlpha, Time.deltaTime * c.animSpeed) : 1f;
            
            if (Mathf.Abs(currentAlpha - nextAlpha) > 0.02f || Mathf.Abs(c.image.color.r - targetColor.r) > 0.02f)
            {
                c.image.color = new Color(nextColor.r, nextColor.g, nextColor.b, nextAlpha);
                isColorDone = false;
            }
            else
            {
                c.image.color = new Color(targetColor.r, targetColor.g, targetColor.b, (c.animType == AnimType.Fade || !show) ? targetAlpha : 1f);
            }

            if (isPosDone && isColorDone) break;
            yield return null;
        }

        // 💥 다 사라진 인물은 화면 밖으로 확실히 빼둡니다.
        //    알파만 0으로 두면 다음 컷씬에서 알파가 되살아날 때 그 자리에 그대로 나타납니다.
        if (!show)
        {
            c.rect.anchoredPosition = c.hiddenPos;
            c.image.color = new Color(targetColor.r, targetColor.g, targetColor.b, 0f);
        }

        c.coroutine = null;
    }
}