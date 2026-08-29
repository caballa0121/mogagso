using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 튜토리얼/보스 공용 연출 담당.
/// 항상 켜져 있는 오브젝트(예: GameSystems)에 붙여주세요.
///
/// ※ 카메라 흔들기는 Cinemachine이 매 프레임 카메라 Transform을 덮어쓰기 때문에
///    여기서는 'UI 패널 흔들기 + 전체화면 플래시'로 대체합니다. (설정 0, 충돌 0)
/// </summary>
public class PianoFX : MonoBehaviour
{
    public static PianoFX Instance;

    [Header("전체화면 플래시용 이미지 (풀스크린 Image, 알파 0으로 시작)")]
    public Image flashImage;

    [Header("중앙 안내 문구 (선택)")]
    public TextMeshProUGUI toastText;
    public CanvasGroup toastGroup;

    private Coroutine flashRoutine;
    private Coroutine toastRoutine;

    private void Awake()
    {
        Instance = this;

        if (flashImage != null)
        {
            Color c = flashImage.color;
            c.a = 0f;
            flashImage.color = c;
            flashImage.raycastTarget = false;
        }

        if (toastGroup != null) toastGroup.alpha = 0f;
    }

    // ---------- 전체화면 플래시 ----------

    /// <summary>화면 전체를 색으로 번쩍이게 합니다. 성공=흰색/노랑, 피격=빨강</summary>
    public void Flash(Color color, float intensity = 0.5f, float duration = 0.3f)
    {
        if (flashImage == null) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine(color, intensity, duration));
    }

    private IEnumerator FlashRoutine(Color color, float intensity, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(intensity, 0f, elapsed / duration);
            flashImage.color = new Color(color.r, color.g, color.b, a);
            yield return null;
        }
        flashImage.color = new Color(color.r, color.g, color.b, 0f);
        flashRoutine = null;
    }

    // ---------- 흔들기 ----------

    /// <summary>RectTransform을 흔듭니다. (피아노 패널, 보스 초상화 등)</summary>
    public void Shake(RectTransform target, float strength = 20f, float duration = 0.25f)
    {
        if (target == null) return;
        StartCoroutine(ShakeRoutine(target, strength, duration));
    }

    private IEnumerator ShakeRoutine(RectTransform target, float strength, float duration)
    {
        Vector2 origin = target.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float damper = 1f - (elapsed / duration);   // 점점 약해짐
            Vector2 offset = Random.insideUnitCircle * strength * damper;
            target.anchoredPosition = origin + offset;
            yield return null;
        }

        target.anchoredPosition = origin;
    }

    // ---------- 건반 파동 ----------

    /// <summary>건반을 좌에서 우로 훑는 성공 연출</summary>
    public IEnumerator KeyRipple(float stepDelay = 0.03f, bool playSound = false)
    {
        PianoManager pm = PianoManager.Instance;
        if (pm == null) yield break;

        for (int i = 0; i < pm.pianoKeys.Count; i++)
        {
            PianoKey k = pm.GetKey(i);
            if (k != null)
            {
                k.FlashDemo(true);
                if (playSound) pm.PlayNote(i, 0.35f);
            }
            yield return new WaitForSeconds(stepDelay);
            if (k != null) k.FlashDemo(false);
        }
    }

    // ---------- 안내 문구 ----------

    public void Toast(string message, float duration = 1.5f)
    {
        if (toastText == null || toastGroup == null) return;
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ToastRoutine(message, duration));
    }

    private IEnumerator ToastRoutine(string message, float duration)
    {
        toastText.text = message;

        float t = 0f;
        while (t < 0.15f) { t += Time.deltaTime; toastGroup.alpha = t / 0.15f; yield return null; }
        toastGroup.alpha = 1f;

        yield return new WaitForSeconds(duration);

        t = 0f;
        while (t < 0.25f) { t += Time.deltaTime; toastGroup.alpha = 1f - (t / 0.25f); yield return null; }
        toastGroup.alpha = 0f;
        toastRoutine = null;
    }

    /// <summary>오답 시 해당 건반을 붉게 깜빡</summary>
    public IEnumerator WrongFeedback(int keyIndex)
    {
        PianoManager pm = PianoManager.Instance;
        if (pm == null) yield break;

        PianoKey k = pm.GetKey(keyIndex);
        if (k != null) k.SetWrong(true);
        Flash(Color.red, 0.25f, 0.2f);

        yield return new WaitForSeconds(0.2f);

        if (k != null) k.SetWrong(false);
    }
}
