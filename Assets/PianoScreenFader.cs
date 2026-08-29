using System.Collections;
using UnityEngine;

public class PianoScreenFader : MonoBehaviour
{
    public static PianoScreenFader Instance;
    public CanvasGroup fadeCanvasGroup;

    void Awake()
    {
        if (Instance == null) Instance = this;
        if (fadeCanvasGroup == null) fadeCanvasGroup = GetComponent<CanvasGroup>();

        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
    }

    public IEnumerator FadeOut(float duration = 0.5f)
    {
        fadeCanvasGroup.blocksRaycasts = true;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
            yield return null;
        }
        fadeCanvasGroup.alpha = 1f;
    }

    public IEnumerator FadeIn(float duration = 0.5f)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            yield return null;
        }
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
    }
}