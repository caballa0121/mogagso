using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// 씬 전환용 페이드 인/아웃.
/// 전체 화면을 덮는 UI Image에 붙여서 씁니다. (보통 검은색 Image)
///
/// 씬이 시작되면 검은 화면에서 서서히 밝아지고(FadeIn),
/// LoadScene(...)을 부르면 어두워진 뒤에 씬을 넘깁니다(FadeOut).
/// </summary>
[RequireComponent(typeof(Image))]
public class ScreenFader : MonoBehaviour
{
    [Header("페이드 색 (보통 검은색)")]
    public Color fadeColor = Color.black;

    [Header("걸리는 시간 (초)")]
    public float fadeInDuration = 0.6f;
    public float fadeOutDuration = 0.6f;

    [Header("씬 시작 시 자동으로 밝아지게 할지")]
    public bool fadeInOnStart = true;

    private Image image;
    private bool initialized;

    /// <summary>씬 안에 있는 ScreenFader를 아무 데서나 가져다 쓸 수 있게 해둡니다.</summary>
    public static ScreenFader Instance { get; private set; }

    void Awake()
    {
        Instance = this;
        EnsureInit();

        // 페이드 인을 할 거면 시작부터 화면을 덮어둬야 첫 프레임이 새어나오지 않습니다.
        if (fadeInOnStart) SetAlpha(1f);
        else SetAlpha(0f);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (fadeInOnStart) StartCoroutine(FadeIn());
    }

    void EnsureInit()
    {
        if (initialized) return;

        image = GetComponent<Image>();
        if (image == null) return;

        initialized = true;
        image.raycastTarget = false;
    }

    void SetAlpha(float a)
    {
        EnsureInit();
        if (image == null) return;

        image.enabled = a > 0.001f;
        image.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, a);
    }

    /// <summary>검은 화면 → 밝아짐.</summary>
    public IEnumerator FadeIn()
    {
        yield return Fade(1f, 0f, fadeInDuration);
    }

    /// <summary>밝은 화면 → 어두워짐.</summary>
    public IEnumerator FadeOut()
    {
        yield return Fade(0f, 1f, fadeOutDuration);
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        EnsureInit();
        if (image == null) yield break;

        if (duration <= 0f)
        {
            SetAlpha(to);
            yield break;
        }

        float elapsed = 0f;
        SetAlpha(from);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetAlpha(to);
    }

    /// <summary>어두워진 뒤에 씬을 넘깁니다.</summary>
    public IEnumerator FadeOutAndLoad(string sceneName)
    {
        yield return FadeOut();
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// 씬 어디서든 부를 수 있는 전환 헬퍼.
    /// ScreenFader가 씬에 없으면 페이드 없이 그냥 씬을 넘깁니다.
    /// </summary>
    public static IEnumerator TransitionTo(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) yield break;

        if (Instance != null)
        {
            yield return Instance.FadeOutAndLoad(sceneName);
        }
        else
        {
            Debug.LogWarning("[ScreenFader] 씬에 ScreenFader가 없어 페이드 없이 전환합니다.");
            SceneManager.LoadScene(sceneName);
        }
    }
}
