using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 셰이더 없이 동작하는 "치지직" 글리치 오버레이.
/// 전체 화면 RawImage에 절차적 노이즈 텍스처를 반복해서 새로 그려 넣습니다.
/// URP / 빌트인 상관없이 동작하며 별도 머티리얼 세팅이 필요 없습니다.
///
/// 사용법
///  - Play(duration)      : 지정 시간만 재생하고 자동으로 꺼짐
///  - StartContinuous()   : 끌 때까지 계속 유지
///  - StopNow()           : 즉시 끄기
/// </summary>
[RequireComponent(typeof(RawImage))]
public class GlitchEffect : MonoBehaviour
{
    [Header("노이즈 해상도 (낮을수록 입자가 굵고 가볍습니다)")]
    public int noiseWidth = 320;
    public int noiseHeight = 180;

    [Header("강도")]
    [Range(0f, 1f)] public float staticDensity = 0.5f;   // 흰 점 노이즈 밀도
    [Range(0f, 1f)] public float overallAlpha = 0.8f;    // 오버레이 전체 불투명도

    [Header("가로 찢김 밴드")]
    public int bandCount = 7;                            // 한 프레임에 생기는 찢김 줄 수
    public int bandMaxHeight = 14;                       // 밴드 두께(픽셀)
    [Range(0f, 1f)] public float rgbSplitAmount = 0.6f;  // 마젠타/시안 색수차 강도

    [Header("갱신 간격 (초). 작을수록 빠르게 지직거립니다")]
    public float refreshInterval = 0.03f;

    [Header("끝으로 갈수록 잦아들게 할지 (Play 모드 전용)")]
    public bool fadeOutOverTime = true;

    [Header("이 UI 뒤에 깔기")]
    [Tooltip("여기에 대화 패널을 넣으면 글리치가 항상 그 뒤로 내려갑니다.\n" +
             "(글리치와 대화 패널이 같은 부모 아래에 있어야 합니다)")]
    public RectTransform renderBehind;

    private RawImage rawImage;
    private Texture2D noiseTex;
    private Color32[] buffer;
    private Coroutine routine;
    private bool initialized;

    /// <summary>글리치가 현재 켜져 있는지.</summary>
    public bool IsPlaying => rawImage != null && rawImage.enabled;

    // refreshInterval이 0이면 프레임당 부하가 치솟으므로 최소값을 보장합니다.
    private float SafeInterval => Mathf.Max(0.01f, refreshInterval);

    void Awake()
    {
        EnsureInit();
    }

    /// <summary>
    /// 초기화. Awake에서 한 번 돌지만, 오브젝트가 꺼진 채로 시작하면 Awake가 실행되지 않으므로
    /// Play/StartContinuous에서도 안전하게 다시 호출합니다.
    /// </summary>
    void EnsureInit()
    {
        if (initialized) return;

        rawImage = GetComponent<RawImage>();
        if (rawImage == null) return; // RawImage가 없으면 초기화를 보류합니다.

        initialized = true;

        // 💥 대화 패널보다 앞에 있으면 뒤로 내립니다.
        //    UI는 계층에서 아래(늦은 순서)에 있을수록 화면 위에 그려집니다.
        if (renderBehind != null && renderBehind.parent == transform.parent)
        {
            int panelIndex = renderBehind.GetSiblingIndex();
            if (transform.GetSiblingIndex() > panelIndex)
                transform.SetSiblingIndex(panelIndex);
        }

        noiseWidth = Mathf.Max(16, noiseWidth);
        noiseHeight = Mathf.Max(16, noiseHeight);

        noiseTex = new Texture2D(noiseWidth, noiseHeight, TextureFormat.RGBA32, false)
        {
            // 입자를 뭉개면 지직거림이 죽습니다. 반드시 Point.
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        buffer = new Color32[noiseWidth * noiseHeight];

        rawImage.texture = noiseTex;
        rawImage.raycastTarget = false;
        rawImage.enabled = false;
    }

    /// <summary>
    /// 글리치를 켤 준비. 오브젝트가 비활성이면 켜고 초기화까지 마칩니다.
    /// 준비에 실패하면 false를 돌려주고 호출부는 조용히 넘어갑니다.
    /// </summary>
    bool PrepareToShow()
    {
        // 꺼져 있으면 Awake가 돈 적이 없습니다. 켜주면 그때 Awake가 실행됩니다.
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        EnsureInit();

        if (rawImage == null)
        {
            Debug.LogWarning("[GlitchEffect] RawImage가 없어 글리치를 건너뜁니다. " +
                             "이 스크립트는 전체 화면 RawImage에 붙여야 합니다.", this);
            return false;
        }
        return true;
    }

    void OnDestroy()
    {
        if (noiseTex != null) Destroy(noiseTex);
    }

    /// <summary>지정한 시간 동안 재생하고 끝나면 자동으로 꺼집니다.</summary>
    public IEnumerator Play(float duration, float intensity = 1f)
    {
        if (duration <= 0f) yield break;

        StopNow();
        if (!PrepareToShow()) yield break;

        intensity = Mathf.Clamp01(intensity);
        rawImage.enabled = true;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float amount = intensity;
            if (fadeOutOverTime)
            {
                // 뒤로 갈수록 잦아들어 자연스럽게 끊깁니다.
                float t = elapsed / duration;
                amount *= 1f - (t * t);
            }

            RenderNoise(amount);

            yield return new WaitForSeconds(SafeInterval);
            elapsed += SafeInterval;
        }

        rawImage.enabled = false;
    }

    /// <summary>StopNow()로 끌 때까지 글리치를 계속 유지합니다.</summary>
    public void StartContinuous(float intensity = 1f)
    {
        StopNow();
        if (!PrepareToShow()) return;

        rawImage.enabled = true;
        routine = StartCoroutine(ContinuousRoutine(Mathf.Clamp01(intensity)));
    }

    IEnumerator ContinuousRoutine(float intensity)
    {
        while (true)
        {
            RenderNoise(intensity);
            yield return new WaitForSeconds(SafeInterval);
        }
    }

    /// <summary>글리치를 즉시 끕니다.</summary>
    public void StopNow()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        if (rawImage != null) rawImage.enabled = false;
    }

    void RenderNoise(float intensity)
    {
        byte baseAlpha = (byte)(255f * Mathf.Clamp01(overallAlpha * intensity));
        float density = staticDensity * intensity;

        // 1) 바탕 — 성긴 흑백 스노우 노이즈. 투명한 부분으로 아래 화면이 비칩니다.
        for (int i = 0; i < buffer.Length; i++)
        {
            if (Random.value < density)
            {
                byte v = (byte)Random.Range(90, 256);
                buffer[i] = new Color32(v, v, v, baseAlpha);
            }
            else
            {
                buffer[i] = default; // (0,0,0,0) 투명
            }
        }

        // 2) 가로 찢김 밴드 — 촘촘한 노이즈에 마젠타/시안 색수차를 입힙니다.
        int bands = Mathf.Max(1, Mathf.RoundToInt(bandCount * intensity));
        byte bandAlpha = (byte)(baseAlpha * Mathf.Lerp(0.4f, 1f, rgbSplitAmount));

        for (int b = 0; b < bands; b++)
        {
            int h = Random.Range(2, Mathf.Max(3, bandMaxHeight));
            int y0 = Random.Range(0, Mathf.Max(1, noiseHeight - h));

            bool magenta = Random.value < 0.5f;
            byte tintR = magenta ? (byte)255 : (byte)40;
            byte tintG = magenta ? (byte)40 : (byte)230;
            byte tintB = 255;

            for (int y = y0; y < y0 + h; y++)
            {
                int row = y * noiseWidth;
                for (int x = 0; x < noiseWidth; x++)
                {
                    buffer[row + x] = (Random.value < 0.75f)
                        ? new Color32(tintR, tintG, tintB, bandAlpha)
                        : default;
                }
            }
        }

        // 3) 가끔 화면을 쓸고 지나가는 밝은 롤링 바
        if (Random.value < 0.25f * intensity)
        {
            int h = Random.Range(noiseHeight / 12, Mathf.Max(2, noiseHeight / 5));
            int y0 = Random.Range(0, Mathf.Max(1, noiseHeight - h));
            byte flashAlpha = (byte)(baseAlpha * 0.5f);

            for (int y = y0; y < y0 + h; y++)
            {
                int row = y * noiseWidth;
                for (int x = 0; x < noiseWidth; x++)
                    buffer[row + x] = new Color32(255, 255, 255, flashAlpha);
            }
        }

        noiseTex.SetPixels32(buffer);
        noiseTex.Apply(false);
    }
}
