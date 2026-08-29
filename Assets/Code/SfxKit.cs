using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 효과음 한 개의 설정.
///
/// 인스펙터에서 클립마다 음량·음높이를 따로 정할 수 있습니다.
/// Unity에서 pitch는 음높이이자 재생 속도입니다.
/// (1보다 크면 빠르고 높게, 작으면 느리고 낮게 — 둘은 같이 움직입니다)
/// </summary>
[System.Serializable]
public class SoundCue
{
    public AudioClip clip;

    [Range(0f, 1f)]
    [Tooltip("이 소리만의 음량")]
    public float volume = 1f;

    [Range(0.1f, 3f)]
    [Tooltip("음높이 겸 재생 속도. 1 = 원본, 0.5 = 느리고 낮게, 2 = 빠르고 높게")]
    public float pitch = 1f;

    [Range(0f, 0.5f)]
    [Tooltip("재생할 때마다 음높이를 이 폭 안에서 조금씩 흔듭니다. 같은 소리 반복이 덜 기계적으로 들립니다.")]
    public float pitchJitter = 0f;

    [Tooltip("이 소리만 이만큼 늦게 재생합니다 (초)")]
    public float delay = 0f;

    [Tooltip("체크하면 스텝이 끝나도 이 소리는 끝까지 계속 울립니다.\n" +
             "해제(기본)하면 스텝이 넘어갈 때 같이 꺼집니다.")]
    public bool keepPlayingAfterStep = false;

    public bool HasClip => clip != null;
}

/// <summary>
/// SfxKit이 재생한 소리들을 나중에 한꺼번에 끌 수 있는 손잡이.
/// 연출 스텝이 끝날 때 그 스텝이 켠 소리만 정확히 끄는 데 씁니다.
/// </summary>
public class SfxHandle
{
    // (재생기 번호, 그때의 재생 일련번호)
    internal readonly List<(int voice, int playId)> plays = new List<(int, int)>();
    internal readonly List<Coroutine> pending = new List<Coroutine>();

    /// <summary>이 손잡이가 켠 소리들만 끕니다. 이미 끝났거나 다른 소리로 바뀐 재생기는 건드리지 않습니다.</summary>
    public void Stop()
    {
        SfxKit.StopHandle(this);
    }
}

/// <summary>
/// 효과음 재생기.
///
/// 소리마다 음량·음높이가 다르려면 AudioSource가 따로 있어야 합니다.
/// (한 AudioSource로 PlayOneShot을 여러 번 부르면 pitch가 서로 간섭합니다)
/// 그래서 재생기를 여러 개 두고 돌아가며 씁니다.
///
/// 쓰는 법 :  SfxKit.Play(myCue);
///            var h = SfxKit.PlayAll(list);  ...  h.Stop();
/// 씬에 미리 만들어 둘 필요 없이 처음 부를 때 알아서 생깁니다.
/// </summary>
public class SfxKit : MonoBehaviour
{
    [Header("동시에 겹쳐 낼 수 있는 소리 개수")]
    [Range(1, 24)] public int voiceCount = 8;

    [Header("전체 음량 (모든 소리에 곱해집니다)")]
    [Range(0f, 1f)] public float masterVolume = 1f;

    private AudioSource[] voices;
    private int[] voicePlayId;   // 각 재생기가 지금 몇 번째 재생을 물고 있는지
    private int nextVoice;
    private int serial;

    private static SfxKit instance;
    private static bool quitting;

    // ─────────────────────────── 바깥에서 쓰는 것 ───────────────────────────

    /// <summary>효과음 하나를 재생합니다. 클립이 비어 있으면 조용히 넘어갑니다.</summary>
    public static SfxHandle Play(SoundCue cue)
    {
        if (cue == null || !cue.HasClip) return null;

        var kit = Ensure();
        if (kit == null) return null;

        var handle = new SfxHandle();
        kit.PlayLocal(cue, handle);
        return handle;
    }

    /// <summary>여러 개를 한꺼번에 재생하고, 나중에 함께 끌 수 있는 손잡이를 돌려줍니다.</summary>
    public static SfxHandle PlayAll(IList<SoundCue> cues)
    {
        if (cues == null || cues.Count == 0) return null;

        var kit = Ensure();
        if (kit == null) return null;

        var handle = new SfxHandle();
        for (int i = 0; i < cues.Count; i++)
        {
            var cue = cues[i];
            if (cue == null || !cue.HasClip) continue;

            kit.PlayLocal(cue, handle);
        }
        return handle;
    }

    /// <summary>손잡이가 켠 소리들만 끕니다.</summary>
    public static void StopHandle(SfxHandle handle)
    {
        if (handle == null || instance == null) return;
        instance.StopHandleLocal(handle);
    }

    // ─────────────────────────── 내부 ───────────────────────────

    private static SfxKit Ensure()
    {
        if (quitting) return null;
        if (instance != null) return instance;

        instance = FindAnyObjectByType<SfxKit>();
        if (instance != null) return instance;

        var go = new GameObject("[SfxKit]");
        instance = go.AddComponent<SfxKit>();
        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        BuildVoices();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void OnApplicationQuit()
    {
        quitting = true;
    }

    private void BuildVoices()
    {
        int n = Mathf.Max(1, voiceCount);
        voices = new AudioSource[n];
        voicePlayId = new int[n];

        for (int i = 0; i < n; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f; // 위치에 상관없이 항상 같은 크기로
            voices[i] = src;
            voicePlayId[i] = 0;
        }
    }

    private void PlayLocal(SoundCue cue, SfxHandle handle)
    {
        if (cue.delay > 0f)
        {
            var co = StartCoroutine(PlayAfter(cue, cue.delay, handle));
            if (handle != null && !cue.keepPlayingAfterStep) handle.pending.Add(co);
            return;
        }

        PlayNow(cue, handle);
    }

    private IEnumerator PlayAfter(SoundCue cue, float seconds, SfxHandle handle)
    {
        yield return new WaitForSeconds(seconds);
        PlayNow(cue, handle);
    }

    private void PlayNow(SoundCue cue, SfxHandle handle)
    {
        if (voices == null) BuildVoices();

        int index = NextVoiceIndex();
        if (index < 0) return;

        AudioSource voice = voices[index];
        if (voice == null) return;

        voice.clip = cue.clip;
        voice.volume = Mathf.Clamp01(cue.volume) * masterVolume;

        float jitter = Random.Range(-cue.pitchJitter, cue.pitchJitter);
        voice.pitch = Mathf.Clamp(cue.pitch + jitter, 0.05f, 3f);

        voicePlayId[index] = ++serial;
        voice.Play();

        // 스텝이 끝날 때 꺼야 하는 소리만 손잡이에 기록해 둡니다.
        if (handle != null && !cue.keepPlayingAfterStep)
        {
            handle.plays.Add((index, voicePlayId[index]));
        }
    }

    private void StopHandleLocal(SfxHandle handle)
    {
        // 아직 재생 전(지연 대기 중)인 것들 취소
        foreach (var co in handle.pending)
        {
            if (co != null) StopCoroutine(co);
        }
        handle.pending.Clear();

        // 💥 재생기는 돌려쓰기 때문에, 그 사이 다른 소리가 차지했을 수 있습니다.
        //    일련번호가 그대로일 때만 꺼서 남의 소리를 끊지 않게 합니다.
        foreach (var (index, playId) in handle.plays)
        {
            if (index < 0 || index >= voices.Length) continue;
            if (voicePlayId[index] != playId) continue;

            var voice = voices[index];
            if (voice != null && voice.isPlaying) voice.Stop();
        }
        handle.plays.Clear();
    }

    /// <summary>놀고 있는 재생기를 먼저 쓰고, 전부 바쁘면 순서대로 덮어씁니다.</summary>
    private int NextVoiceIndex()
    {
        if (voices == null || voices.Length == 0) return -1;

        for (int i = 0; i < voices.Length; i++)
        {
            int idx = (nextVoice + i) % voices.Length;
            if (voices[idx] != null && !voices[idx].isPlaying)
            {
                nextVoice = (idx + 1) % voices.Length;
                return idx;
            }
        }

        int fallback = nextVoice;
        nextVoice = (nextVoice + 1) % voices.Length;
        return fallback;
    }
}
