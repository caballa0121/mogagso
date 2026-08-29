using UnityEngine;

// 대화 한 줄에 대한 데이터
[System.Serializable]
public struct DialogueLine
{
    public string speakerName;     // 화자 이름
    public Sprite portraitSprite;  // 좌측 반신 일러스트
    [TextArea(3, 5)]
    public string dialogueText;    // 대화 본문 (인스펙터에서 여러 줄 작성 가능)

    // ────────────────────────────────────────────────
    //  컷씬 (선택 사항)
    //  cutsceneSprite를 비워두면 컷씬 없이 기존과 동일하게 동작합니다.
    // ────────────────────────────────────────────────
    [Header("컷씬 (선택)")]
    [Tooltip("이 줄과 함께 띄울 컷씬 이미지. 비우면 컷씬 없음")]
    public Sprite cutsceneSprite;

    [Tooltip("체크하면 이 줄의 대사가 '나오기 전'에 컷씬을 띄웁니다. 해제하면 이 줄을 다 읽은 '후' 띄웁니다")]
    public bool cutsceneBeforeLine;
}

// 대화 전체 데이터 (ScriptableObject)
[CreateAssetMenu(fileName = "NewDialogue", menuName = "Dialogue/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    public DialogueLine[] lines; // 대화 흐름 배열
}
