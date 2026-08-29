using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 노드맵의 연출 담당.
///
/// - 전투 노드: '전투' 로고가 확 떴다가, 그대로 켜둔 채 ScreenFader가 어두워지며 전투 씬으로 넘어갑니다.
/// - 함정 노드: 화면이 검게 흐려지고, 커다란 주인공이 나타난 뒤
///              '함정' 로고가 튀어나오며 주인공을 왼쪽으로 밀어냅니다.
///              (맵 위의 주인공 말은 제자리에 그대로 있습니다)
/// </summary>
public class NodeMapPresenter : MonoBehaviour
{
    [Header("로고 UI (화면 가운데)")]
    public CanvasGroup logoGroup;
    public RectTransform logoRect;
    public Image logoImage;
    public TextMeshProUGUI logoText;
    [Tooltip("로고 밑에 작게 뜨는 설명 (예: 체력 -20%)")]
    public TextMeshProUGUI subText;

    [Header("전투 로고")]
    public Sprite battleLogoSprite;
    public string battleLogoLabel = "전 투";
    public Color battleLogoColor = new Color(1f, 0.85f, 0.3f, 1f);
    [Tooltip("로고가 떠 있다가 페이드아웃이 시작될 때까지의 시간")]
    public float battleLogoHold = 0.9f;

    [Header("함정 로고")]
    public Sprite trapLogoSprite;
    public string trapLogoLabel = "함 정";
    public string trapSubLabel = "파티 전원 체력 -20%";
    public Color trapLogoColor = new Color(1f, 0.35f, 0.35f, 1f);
    public float trapLogoHold = 0.9f;

    [Header("로고 등장 연출")]
    public float popDuration = 0.25f;
    public float popFromScale = 1.7f;
    public float fadeOutDuration = 0.3f;

    [Header("함정 - 화면 암전")]
    [Tooltip("화면 전체를 덮는 검은 Image")]
    public Image dimImage;
    [Range(0f, 1f)] public float dimAlpha = 0.8f;
    public float dimFadeInDuration = 0.35f;
    public float dimFadeOutDuration = 0.35f;

    [Header("함정 - 커다란 주인공")]
    [Tooltip("암전 위에 크게 뜨는 주인공 이미지")]
    public Image trapHeroImage;
    public RectTransform trapHeroRect;
    [Tooltip("암전이 끝난 뒤 주인공이 나타나는 데 걸리는 시간")]
    public float heroEnterDuration = 0.3f;
    [Tooltip("가운데에서 왼쪽으로 밀려나는 거리 (UI 픽셀)")]
    public float pushDistance = 300f;
    public float pushDuration = 0.18f;
    public float shakeDuration = 0.25f;
    public float shakeStrength = 14f;

    // 인스펙터에서 옮겨둔 주인공의 원래 자리를 기억해 둡니다.
    private Vector2 heroHome;

    void Awake()
    {
        if (trapHeroRect != null) heroHome = trapHeroRect.anchoredPosition;
        HideAllImmediate();
    }

    public void HideAllImmediate()
    {
        if (logoGroup != null) logoGroup.alpha = 0f;
        if (subText != null) subText.text = "";
        SetDimAlpha(0f);
        SetHeroAlpha(0f);
    }

    // ─────────────────────────── 전투 ───────────────────────────

    /// <summary>'전투' 로고를 띄웁니다. 끝나도 로고는 켜둔 채로 둡니다(페이드아웃 위에서 사라지도록).</summary>
    public IEnumerator PlayBattleIntro()
    {
        SetLogo(battleLogoSprite, battleLogoLabel, battleLogoColor, "");
        yield return StartCoroutine(PopIn());
        yield return new WaitForSeconds(battleLogoHold);
    }

    // ─────────────────────────── 함정 ───────────────────────────

    /// <summary>
    /// 화면이 검게 흐려진 뒤 커다란 주인공이 나타나고,
    /// '함정' 로고가 그 주인공을 왼쪽으로 밀어내는 연출.
    /// </summary>
    public IEnumerator PlayTrapEffect()
    {
        // 1. 화면 암전
        yield return StartCoroutine(FadeDim(0f, dimAlpha, dimFadeInDuration));

        // 2. 커다란 주인공 등장
        if (trapHeroRect != null) trapHeroRect.anchoredPosition = heroHome;
        yield return StartCoroutine(FadeHero(0f, 1f, heroEnterDuration));

        // 3. 함정 로고가 튀어나오며 주인공을 왼쪽으로 밀어냄
        SetLogo(trapLogoSprite, trapLogoLabel, trapLogoColor, trapSubLabel);
        StartCoroutine(PopIn());

        if (trapHeroRect != null)
        {
            yield return StartCoroutine(MoveHero(heroHome + Vector2.left * pushDistance, pushDuration));
            yield return StartCoroutine(ShakeHero(shakeDuration, shakeStrength));
        }

        yield return new WaitForSeconds(trapLogoHold);

        // 4. 정리 (로고 / 주인공 / 암전 동시에 사라짐)
        StartCoroutine(FadeOutLogo());
        StartCoroutine(FadeHero(1f, 0f, dimFadeOutDuration));
        yield return StartCoroutine(FadeDim(dimAlpha, 0f, dimFadeOutDuration));

        if (trapHeroRect != null) trapHeroRect.anchoredPosition = heroHome;
        SetHeroAlpha(0f);
        if (logoGroup != null) logoGroup.alpha = 0f;
    }

    // ─────────────────────────── 로고 ───────────────────────────

    private void SetLogo(Sprite sprite, string text, Color color, string sub)
    {
        if (logoImage != null)
        {
            logoImage.sprite = sprite;
            logoImage.enabled = sprite != null;
            if (sprite != null) logoImage.color = Color.white;
        }

        if (logoText != null)
        {
            logoText.text = text;
            logoText.color = color;
            logoText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        if (subText != null)
        {
            subText.text = sub;
            subText.gameObject.SetActive(!string.IsNullOrEmpty(sub));
        }
    }

    private IEnumerator PopIn()
    {
        if (logoGroup == null) yield break;

        float elapsed = 0f;
        logoGroup.alpha = 0f;

        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / popDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f); // ease-out

            logoGroup.alpha = eased;
            if (logoRect != null)
            {
                float s = Mathf.Lerp(popFromScale, 1f, eased);
                logoRect.localScale = new Vector3(s, s, 1f);
            }
            yield return null;
        }

        logoGroup.alpha = 1f;
        if (logoRect != null) logoRect.localScale = Vector3.one;
    }

    private IEnumerator FadeOutLogo()
    {
        if (logoGroup == null) yield break;

        float start = logoGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            logoGroup.alpha = Mathf.Lerp(start, 0f, Mathf.Clamp01(elapsed / fadeOutDuration));
            yield return null;
        }

        logoGroup.alpha = 0f;
    }

    // ─────────────────────────── 암전 ───────────────────────────

    private void SetDimAlpha(float a)
    {
        if (dimImage == null) return;

        Color c = dimImage.color;
        c.a = a;
        dimImage.color = c;
        dimImage.enabled = a > 0.001f;
    }

    private IEnumerator FadeDim(float from, float to, float duration)
    {
        if (dimImage == null) yield break;

        if (duration <= 0f)
        {
            SetDimAlpha(to);
            yield break;
        }

        float elapsed = 0f;
        SetDimAlpha(from);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetDimAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetDimAlpha(to);
    }

    // ─────────────────────────── 커다란 주인공 ───────────────────────────

    private void SetHeroAlpha(float a)
    {
        if (trapHeroImage == null) return;

        Color c = trapHeroImage.color;
        c.a = a;
        trapHeroImage.color = c;
        trapHeroImage.enabled = a > 0.001f;
    }

    private IEnumerator FadeHero(float from, float to, float duration)
    {
        if (trapHeroImage == null) yield break;

        if (duration <= 0f)
        {
            SetHeroAlpha(to);
            yield break;
        }

        float elapsed = 0f;
        SetHeroAlpha(from);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetHeroAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetHeroAlpha(to);
    }

    private IEnumerator MoveHero(Vector2 target, float duration)
    {
        if (trapHeroRect == null) yield break;

        if (duration <= 0f)
        {
            trapHeroRect.anchoredPosition = target;
            yield break;
        }

        Vector2 start = trapHeroRect.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            trapHeroRect.anchoredPosition = Vector2.Lerp(start, target, 1f - Mathf.Pow(1f - t, 3f));
            yield return null;
        }

        trapHeroRect.anchoredPosition = target;
    }

    private IEnumerator ShakeHero(float duration, float strength)
    {
        if (trapHeroRect == null || duration <= 0f) yield break;

        Vector2 origin = trapHeroRect.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float damp = 1f - Mathf.Clamp01(elapsed / duration);
            trapHeroRect.anchoredPosition = origin + new Vector2(
                Random.Range(-strength, strength) * damp,
                Random.Range(-strength, strength) * damp);
            yield return null;
        }

        trapHeroRect.anchoredPosition = origin;
    }
}
