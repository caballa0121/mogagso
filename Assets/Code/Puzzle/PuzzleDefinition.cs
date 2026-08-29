using System.Collections.Generic;
using UnityEngine;

/// <summary>퍼즐 조각 한 개의 정보. 재단 도구가 자동으로 채워줍니다.</summary>
[System.Serializable]
public class PuzzlePieceData
{
    public Sprite sprite;

    [Tooltip("원본 그림 안에서 이 조각이 있어야 할 자리 (그림 한가운데가 0,0 / 픽셀)")]
    public Vector2 correctPosition;

    [Tooltip("잘라낸 조각의 크기 (픽셀)")]
    public Vector2 size;
}

/// <summary>
/// 퍼즐 한 판의 정의.
///
/// 직접 손으로 채우지 마시고 Tools → 퍼즐 → [조각 이미지 재단]을 쓰세요.
/// 조각 PNG들을 읽어서 이 에셋을 통째로 만들어 줍니다.
/// </summary>
[CreateAssetMenu(fileName = "NewPuzzle", menuName = "퍼즐/퍼즐 정의")]
public class PuzzleDefinition : ScriptableObject
{
    [Header("원본 그림 크기 (픽셀)")]
    public Vector2 canvasSize = new Vector2(1920f, 1080f);

    [Header("완성본 이미지 (비워두면 조각 그대로 둡니다)")]
    public Sprite completedImage;

    [Header("조각들")]
    public List<PuzzlePieceData> pieces = new List<PuzzlePieceData>();

    [Header("판 크기")]
    [Range(0.3f, 1f)]
    [Tooltip("완성 그림을 화면의 몇 배 크기로 놓을지.\n" +
             "1이면 화면을 꽉 채워서 조각을 흩뿌릴 여백이 사라집니다.\n" +
             "0.7 정도면 그림 둘레에 여백이 생겨 조각을 넓게 늘어놓을 수 있습니다.")]
    public float boardFitRatio = 0.7f;

    [Header("맞출 틀 (원본 그림 크기)")]
    [Tooltip("원본 그림 크기의 틀을 화면에 그려서 '여기에 맞추면 된다'를 알려줍니다.\n" +
             "틀이 없으면 조각끼리만 맞추게 되어, 그림 전체가 밀린 채 완성돼도 판정이 안 됩니다.")]
    public bool showBoardFrame = true;
    public Color frameFillColor = new Color(1f, 1f, 1f, 0.06f);
    public Color frameBorderColor = new Color(1f, 1f, 1f, 0.55f);
    public float frameBorderThickness = 6f;

    [Header("흐린 밑그림")]
    [Tooltip("완성본을 아주 흐리게 깔아서 어느 조각이 어디 가는지 알려줍니다.")]
    public bool showGhostPreview = true;
    [Range(0f, 0.6f)] public float ghostAlpha = 0.18f;

    [Header("진행 표시")]
    [Tooltip("화면 아래에 '3 / 7' 처럼 몇 조각을 맞췄는지 보여줍니다.")]
    public bool showProgress = true;

    [Header("난이도 / 연출")]
    [Tooltip("조각을 흩뿌릴 때 화면 가장자리에서 최소한 이만큼 안쪽에 둡니다.")]
    public float scatterMargin = 140f;

    [Tooltip("제자리에서 이 거리 안에 놓으면 딸깍 붙습니다. (픽셀)")]
    public float snapDistance = 70f;

    [Tooltip("조각이 제자리로 딸깍 붙는 데 걸리는 시간")]
    public float snapDuration = 0.12f;

    [Tooltip("완성본이 겹쳐지는 데 걸리는 시간")]
    public float completeFadeDuration = 0.8f;

    [Tooltip("완성된 뒤 '클릭해서 계속' 안내가 뜨기까지 기다리는 시간")]
    public float hintDelay = 0.6f;
}
