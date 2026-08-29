using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// 게임 도입부 영상 재생기.
/// 전체 화면 RawImage에 영상을 그린 뒤, 끝나면(또는 건너뛰면) 다음 씬으로 넘어갑니다.
///
/// 영상은 두 가지 방식 중 하나로 넣습니다.
///  1) VideoClip  : mp4를 Assets에 넣고 그대로 드래그 (간단, 빌드에 포함됨)
///  2) 파일 이름  : StreamingAssets 폴더에 두고 파일명만 적기 (용량 큰 영상에 적합)
/// </summary>
[RequireComponent(typeof(RawImage))]
public class IntroVideoPlayer : MonoBehaviour
{
    [Header("재생할 영상")]
    [Tooltip("Assets에 넣은 영상 파일을 드래그하세요. 아래 streamingFileName을 쓸 거면 비워둡니다.")]
    public VideoClip videoClip;

    [Tooltip("StreamingAssets 폴더에 둔 파일 이름 (예: intro.mp4). videoClip이 비어있을 때만 사용합니다.")]
    public string streamingFileName = "";

    [Header("영상이 끝나면 넘어갈 씬")]
    public string nextSceneName = "CHAPTER 1";

    [Header("건너뛰기")]
    [Tooltip("아무 키나 마우스 클릭으로 건너뛸 수 있게 합니다.")]
    public bool allowSkip = true;
    [Tooltip("시작 직후 오조작을 막기 위해 이 시간 동안은 건너뛰기를 막습니다.")]
    public float skipLockSeconds = 0.5f;

    [Header("소리 (비워두면 기본 출력으로 재생)")]
    public AudioSource audioSource;

    [Header("영상 로딩이 이 시간을 넘기면 그냥 다음 씬으로 넘어갑니다 (초)")]
    public float prepareTimeout = 10f;

    private RawImage rawImage;
    private VideoPlayer player;
    private RenderTexture renderTexture;
    private bool finished;

    void Awake()
    {
        rawImage = GetComponent<RawImage>();
        rawImage.raycastTarget = false;

        // 텍스처가 붙기 전의 RawImage는 흰 사각형으로 그려집니다.
        // 시작할 때 흰 화면이 번쩍이지 않도록 검게 두고, 영상이 준비되면 흰색으로 되돌립니다.
        rawImage.color = Color.black;

        player = GetComponent<VideoPlayer>();
        if (player == null) player = gameObject.AddComponent<VideoPlayer>();

        player.playOnAwake = false;
        player.isLooping = false;
        player.waitForFirstFrame = true;
        player.renderMode = VideoRenderMode.RenderTexture;
    }

    void OnDestroy()
    {
        ReleaseTexture();
    }

    void ReleaseTexture()
    {
        if (renderTexture == null) return;

        if (player != null && player.targetTexture == renderTexture) player.targetTexture = null;
        if (rawImage != null && rawImage.texture == renderTexture) rawImage.texture = null;

        renderTexture.Release();
        Destroy(renderTexture);
        renderTexture = null;
    }

    IEnumerator Start()
    {
        // 재생할 영상이 아예 없으면 붙잡아두지 않고 바로 넘어갑니다.
        if (videoClip == null && string.IsNullOrEmpty(streamingFileName))
        {
            Debug.LogWarning("[IntroVideoPlayer] 재생할 영상이 지정되지 않아 바로 다음 씬으로 넘어갑니다.");
            yield return Finish();
            yield break;
        }

        SetupSource();

        player.errorReceived += OnVideoError;
        player.Prepare();

        // 준비가 끝날 때까지 대기. 너무 오래 걸리면 포기하고 넘어갑니다.
        float waited = 0f;
        while (!player.isPrepared && waited < prepareTimeout)
        {
            waited += Time.unscaledDeltaTime;
            if (finished) yield break;
            yield return null;
        }

        if (!player.isPrepared)
        {
            Debug.LogWarning("[IntroVideoPlayer] 영상 준비가 지연되어 다음 씬으로 넘어갑니다.");
            yield return Finish();
            yield break;
        }

        AttachTexture();
        player.Play();

        float elapsed = 0f;
        // 영상이 끝날 때까지 대기하면서 건너뛰기 입력을 봅니다.
        // isPlaying은 첫 프레임 전에 잠깐 false일 수 있어, 재생 시작 후에만 종료로 판정합니다.
        while (!finished)
        {
            elapsed += Time.unscaledDeltaTime;

            if (allowSkip && elapsed > skipLockSeconds && SkipPressed())
            {
                Debug.Log("[IntroVideoPlayer] 도입부 영상을 건너뜁니다.");
                break;
            }

            if (elapsed > skipLockSeconds && !player.isPlaying) break; // 재생 완료
            yield return null;
        }

        yield return Finish();
    }

    void SetupSource()
    {
        if (videoClip != null)
        {
            player.source = VideoSource.VideoClip;
            player.clip = videoClip;
        }
        else
        {
            player.source = VideoSource.Url;
            player.url = System.IO.Path.Combine(Application.streamingAssetsPath, streamingFileName);
        }

        if (audioSource != null)
        {
            player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            player.EnableAudioTrack(0, true);
            player.SetTargetAudioSource(0, audioSource);
        }
        else
        {
            player.audioOutputMode = VideoAudioOutputMode.Direct;
            player.EnableAudioTrack(0, true);
        }
    }

    void AttachTexture()
    {
        int w = (int)player.width;
        int h = (int)player.height;
        if (w <= 0 || h <= 0) { w = Screen.width; h = Screen.height; }

        ReleaseTexture();
        renderTexture = new RenderTexture(w, h, 0);
        renderTexture.Create();

        player.targetTexture = renderTexture;
        rawImage.texture = renderTexture;
        rawImage.color = Color.white; // 영상이 원래 색으로 보이도록 되돌립니다.

        // 화면 비율이 다를 때 찌그러지지 않도록, AspectRatioFitter가 붙어 있으면 비율을 맞춰줍니다.
        var fitter = GetComponent<AspectRatioFitter>();
        if (fitter != null) fitter.aspectRatio = (float)w / h;
    }

    bool SkipPressed()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
        return false;
    }

    void OnVideoError(VideoPlayer source, string message)
    {
        Debug.LogError($"[IntroVideoPlayer] 영상 재생 실패: {message}");
        StartCoroutine(Finish());
    }

    IEnumerator Finish()
    {
        if (finished) yield break;
        finished = true;

        if (player != null)
        {
            player.errorReceived -= OnVideoError;
            player.Stop();
        }

        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogWarning("[IntroVideoPlayer] 넘어갈 씬 이름이 비어 있습니다.");
            yield break;
        }

        // 페이드 아웃 후 씬 전환 (ScreenFader가 없으면 즉시 전환)
        yield return StartCoroutine(ScreenFader.TransitionTo(nextSceneName));
    }
}
