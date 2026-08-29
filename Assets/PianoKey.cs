using UnityEngine;
using UnityEngine.UI;

public class PianoKey : MonoBehaviour
{
    [Header("건반 설정")]
    public KeyCode triggerKey;  // 매핑할 키보드 자판
    public string noteName;     // 음계 명칭 (예: 도1, 도#1) - 표시용

    [Header("시각 연출")]
    [Tooltip("플레이어가 직접 눌렀을 때 켜지는 하이라이트")]
    public Image highlightImage;
    [Tooltip("튜토리얼/보스가 '여기 눌러' 라고 알려줄 때 켜지는 가이드 (없으면 highlight를 대신 사용)")]
    public Image guideImage;
    [Tooltip("오답일 때 잠깐 켜지는 붉은 표시 (선택)")]
    public Image wrongImage;
    [Tooltip("보스가 건반을 봉인했을 때 켜지는 표시 (선택, 없으면 어둡게 처리)")]
    public Image sealImage;

    [Header("봉인 시 색상")]
    public Image keyBodyImage;              // 건반 본체 이미지 (선택)
    public Color sealedColor = new Color(0.35f, 0.35f, 0.45f, 1f);

    private Color originalBodyColor = Color.white;

    private void Awake()
    {
        if (highlightImage != null) highlightImage.gameObject.SetActive(false);
        if (guideImage != null) guideImage.gameObject.SetActive(false);
        if (wrongImage != null) wrongImage.gameObject.SetActive(false);
        if (sealImage != null) sealImage.gameObject.SetActive(false);
        if (keyBodyImage != null) originalBodyColor = keyBodyImage.color;
    }

    /// <summary>보스 기믹: 이 건반을 못 쓰게 만듭니다.</summary>
    public void SetSealed(bool on)
    {
        if (sealImage != null) sealImage.gameObject.SetActive(on);
        if (keyBodyImage != null) keyBodyImage.color = on ? sealedColor : originalBodyColor;
        if (on)
        {
            if (highlightImage != null) highlightImage.gameObject.SetActive(false);
            if (guideImage != null) guideImage.gameObject.SetActive(false);
        }
    }

    public void Press()
    {
        if (highlightImage != null) highlightImage.gameObject.SetActive(true);
    }

    public void Release()
    {
        if (highlightImage != null) highlightImage.gameObject.SetActive(false);
    }

    /// <summary>튜토리얼/보스가 다음에 눌러야 할 건반을 알려줄 때 사용</summary>
    public void SetGuide(bool on)
    {
        if (guideImage != null)
        {
            guideImage.gameObject.SetActive(on);
        }
        else if (highlightImage != null)
        {
            // 가이드용 이미지가 없으면 하이라이트를 대신 씁니다
            highlightImage.gameObject.SetActive(on);
        }
    }

    /// <summary>보스/시연이 자동으로 이 건반을 연주할 때의 시각 표현</summary>
    public void FlashDemo(bool on)
    {
        if (highlightImage != null) highlightImage.gameObject.SetActive(on);
    }

    public void SetWrong(bool on)
    {
        if (wrongImage != null) wrongImage.gameObject.SetActive(on);
    }
}
