using UnityEngine;
using TMPro;

/// <summary>
/// 노드맵의 칸 한 개.
///
/// 씬에 블럭 스프라이트를 여러 개 배치해서 맵을 그리고,
/// 인스펙터에서 칸마다 nodeType(전투 / 함정 / 빈칸)을 찍어주면 됩니다.
///
/// coord(좌표)는 NodeMapManager가 "월드 위치로 좌표 다시 계산" 메뉴로 자동으로 채워줍니다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class MapNode : MonoBehaviour
{
    [Header("좌표 (NodeMapManager가 자동으로 채워줍니다)")]
    public Vector2Int coord;

    [Header("이 칸의 내용")]
    public MapNodeType nodeType = MapNodeType.Empty;

    [Header("표시용 자식")]
    [Tooltip("칸 위에 띄울 글자(?, 전투, 함정 등). 없어도 동작합니다.")]
    public TextMeshPro label;

    [Tooltip("글자 대신 쓸 아이콘. 없어도 동작합니다.")]
    public SpriteRenderer icon;

    [HideInInspector] public bool isCleared;

    private SpriteRenderer body;

    public SpriteRenderer Body
    {
        get
        {
            if (body == null) body = GetComponent<SpriteRenderer>();
            return body;
        }
    }

    public bool IsStart => nodeType == MapNodeType.Start;
    public bool IsGoal => nodeType == MapNodeType.Goal;

    /// <summary>시작/목적지/빈칸처럼 밟기만 하면 바로 안전해지는 칸인지.</summary>
    public bool IsHarmless => nodeType == MapNodeType.Empty || nodeType == MapNodeType.Start || nodeType == MapNodeType.Goal;

    /// <summary>NodeMapManager가 매번 상태를 다시 그릴 때 부릅니다.</summary>
    public void ApplyVisual(Color bodyColor, string text, Color textColor, Sprite iconSprite)
    {
        if (Body != null) Body.color = bodyColor;

        if (label != null)
        {
            label.text = text;
            label.color = textColor;
            label.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        if (icon != null)
        {
            icon.sprite = iconSprite;
            icon.gameObject.SetActive(iconSprite != null);
        }
    }
}
