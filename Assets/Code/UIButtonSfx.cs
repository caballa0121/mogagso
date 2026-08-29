using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 버튼(또는 클릭 가능한 UI)에 붙이면 누를 때·올릴 때 소리가 나게 해줍니다.
///
/// Button 컴포넌트가 없어도 됩니다. 레이캐스트를 받는 UI면 다 동작합니다.
/// 소리마다 음량·음높이를 따로 정할 수 있습니다.
///
/// 붙이는 법 : 버튼 선택 → Add Component → "버튼 효과음"
///             또는  Tools → 사운드 → 씬의 모든 버튼에 효과음 붙이기
/// </summary>
[AddComponentMenu("UI/버튼 효과음")]
public class UIButtonSfx : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
{
    [Header("누를 때")]
    public SoundCue click = new SoundCue();

    [Header("마우스를 올릴 때 (비워두면 소리 없음)")]
    public SoundCue hover = new SoundCue();

    [Header("옵션")]
    [Tooltip("체크하면 눌리지 않는 상태(Interactable 꺼짐)의 버튼에서는 소리가 나지 않습니다.")]
    public bool respectInteractable = true;

    private Selectable selectable;

    void Awake()
    {
        selectable = GetComponent<Selectable>();
    }

    /// <summary>지금 소리를 내면 안 되는 상태인지.</summary>
    private bool Blocked
    {
        get
        {
            if (!respectInteractable) return false;
            return selectable != null && !selectable.interactable;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Blocked) return;
        SfxKit.Play(click);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Blocked) return;
        SfxKit.Play(hover);
    }
}
