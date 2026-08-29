using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 퍼즐 조각 한 개.
///
/// 끌고 다니는 판정은 PuzzleManager가 직접 합니다.
/// (EventSystem을 쓰지 않습니다 — 덧씌운 씬에서 밑 씬의 EventSystem과 부딪히지 않게 하려고요)
/// 이 컴포넌트는 자기 상태와 겉모습만 챙깁니다.
/// </summary>
public class PuzzlePiece : MonoBehaviour
{
    public RectTransform rect;
    public Image image;

    [Tooltip("이 조각이 있어야 할 자리 (판 한가운데가 0,0)")]
    public Vector2 correctPosition;

    [HideInInspector] public bool isPlaced;

    private Texture2D cachedTexture;
    private bool alphaTestable;
    private bool alphaChecked;

    public void Setup(PuzzlePieceData data)
    {
        if (rect == null) rect = GetComponent<RectTransform>();
        if (image == null) image = GetComponent<Image>();

        correctPosition = data.correctPosition;
        isPlaced = false;

        if (image != null)
        {
            image.sprite = data.sprite;
            image.raycastTarget = false; // 판정은 PuzzleManager가 직접 합니다.
        }

        if (rect != null && data.size.x > 0f && data.size.y > 0f)
        {
            rect.sizeDelta = data.size;
        }
    }

    /// <summary>제자리에서 얼마나 떨어져 있는지.</summary>
    public float DistanceToHome => rect != null ? Vector2.Distance(rect.anchoredPosition, correctPosition) : float.MaxValue;

    /// <summary>화면의 이 점이 이 조각의 '그림이 있는 부분'인지 확인합니다.</summary>
    public bool ContainsScreenPoint(Vector2 screenPoint, Camera uiCamera, float alphaThreshold)
    {
        if (rect == null) return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, uiCamera, out Vector2 local))
            return false;

        Rect r = rect.rect;
        if (!r.Contains(local)) return false;

        // 투명한 부분은 통과시켜서 밑에 깔린 조각을 집을 수 있게 합니다.
        if (!EnsureAlphaTestable()) return true;

        Sprite sp = image.sprite;
        Rect texRect = sp.textureRect;

        float u = (local.x - r.x) / r.width;
        float v = (local.y - r.y) / r.height;

        int px = Mathf.Clamp(Mathf.FloorToInt(texRect.x + u * texRect.width), 0, cachedTexture.width - 1);
        int py = Mathf.Clamp(Mathf.FloorToInt(texRect.y + v * texRect.height), 0, cachedTexture.height - 1);

        return cachedTexture.GetPixel(px, py).a >= alphaThreshold;
    }

    private bool EnsureAlphaTestable()
    {
        if (alphaChecked) return alphaTestable;
        alphaChecked = true;

        if (image == null || image.sprite == null || image.sprite.texture == null)
        {
            alphaTestable = false;
            return false;
        }

        cachedTexture = image.sprite.texture;

        // Read/Write Enabled가 꺼져 있으면 픽셀을 읽을 수 없어 사각형 판정으로 물러섭니다.
        alphaTestable = cachedTexture.isReadable;

        if (!alphaTestable)
        {
            Debug.LogWarning($"[Puzzle] '{cachedTexture.name}' 텍스처의 Read/Write Enabled가 꺼져 있어 " +
                             "투명한 부분까지 집히게 됩니다. Tools → 퍼즐 → [조각 이미지 재단]을 다시 돌려주세요.");
        }

        return alphaTestable;
    }
}
