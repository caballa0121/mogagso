using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 건반 입력 수신 + 소리 재생 + 가이드 표시 담당.
/// 이 스크립트는 '피아노 UI 패널'에 붙입니다. (패널이 꺼지면 입력도 자동으로 멈춤)
/// </summary>
public class PianoManager : MonoBehaviour
{
    public static PianoManager Instance;

    [Header("2옥타브 건반 리스트 (낮은 음부터 반음 순서대로 24개)")]
    public List<PianoKey> pianoKeys = new List<PianoKey>();

    [Header("오디오 설정")]
    [Tooltip("피아노 샘플 1개. 이 클립의 음정을 아래 baseClipMidi에 적어주세요.")]
    public AudioClip baseClip;
    [Tooltip("baseClip이 실제로 내는 음의 MIDI 번호. 가운데 도(C4) = 60")]
    public int baseClipMidi = 60;
    [Tooltip("pianoKeys[0]번 건반의 MIDI 번호. 가운데 도부터 시작이면 60")]
    public int firstKeyMidi = 60;
    [Tooltip("동시에 울릴 수 있는 음의 개수")]
    public int voiceCount = 10;
    [Range(0f, 1f)] public float volume = 0.8f;

    [Header("시작 시 UI 숨기기")]
    public bool hideOnAwake = true;

    /// <summary>true면 플레이어 입력을 무시합니다. (보스 시연 중 등)</summary>
    [HideInInspector] public bool inputLocked = false;

    private AudioSource[] voices;
    private int nextVoice;
    private readonly HashSet<int> sealedKeys = new HashSet<int>();

    private void Awake()
    {
        Instance = this;
        BuildVoices();

        if (hideOnAwake)
        {
            gameObject.SetActive(false);
        }
    }

    // 오디오는 별도의 루트 오브젝트에 둡니다.
    // (피아노 UI 패널이 꺼지면 그 위의 AudioSource는 소리를 못 냅니다)
    private void BuildVoices()
    {
        if (voices != null) return;

        GameObject host = new GameObject("~PianoVoices");
        host.transform.SetParent(null);

        voices = new AudioSource[Mathf.Max(1, voiceCount)];
        for (int i = 0; i < voices.Length; i++)
        {
            AudioSource src = host.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f; // 2D 사운드
            voices[i] = src;
        }
    }

    private void Update()
    {
        if (inputLocked) return;

        for (int i = 0; i < pianoKeys.Count; i++)
        {
            PianoKey key = pianoKeys[i];
            if (key == null) continue;
            if (sealedKeys.Contains(i)) continue;   // 보스가 봉인한 건반

            if (Input.GetKeyDown(key.triggerKey))
            {
                key.Press();
                PlayNote(i);
                PianoEvents.RaiseNote(i);   // ← 튜토리얼/보스가 여기서 입력을 받아갑니다
            }

            if (Input.GetKeyUp(key.triggerKey))
            {
                key.Release();
            }
        }
    }

    // ---------- 소리 ----------

    public void PlayNote(int keyIndex, float volumeScale = 1f)
    {
        if (baseClip == null || voices == null) return;

        AudioSource src = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;

        int midi = firstKeyMidi + keyIndex;
        src.clip = baseClip;
        src.pitch = Mathf.Pow(2f, (midi - baseClipMidi) / 12f);
        src.volume = volume * volumeScale;
        src.Play();
    }

    /// <summary>전투 시작 전 첫 음이 늦게 나오는 현상 방지용</summary>
    public void WarmUpAudio()
    {
        if (baseClip == null || voices == null) return;
        voices[0].clip = baseClip;
        voices[0].volume = 0f;
        voices[0].pitch = 1f;
        voices[0].Play();
        voices[0].Stop();
    }

    // ---------- 건반 조회 / 가이드 ----------

    public PianoKey GetKey(int index)
    {
        if (index < 0 || index >= pianoKeys.Count) return null;
        return pianoKeys[index];
    }

    public void SetGuide(int index, bool on)
    {
        PianoKey k = GetKey(index);
        if (k != null) k.SetGuide(on);
    }

    public void ClearAllGuides()
    {
        for (int i = 0; i < pianoKeys.Count; i++)
        {
            if (pianoKeys[i] != null)
            {
                pianoKeys[i].SetGuide(false);
                pianoKeys[i].SetWrong(false);
            }
        }
    }

    public void ClearAllHighlights()
    {
        for (int i = 0; i < pianoKeys.Count; i++)
        {
            if (pianoKeys[i] != null) pianoKeys[i].Release();
        }
    }

    // ---------- 건반 봉인 (보스 기믹) ----------

    public void SealKeys(int[] indices)
    {
        ClearSeals();
        if (indices == null) return;

        foreach (int i in indices)
        {
            PianoKey k = GetKey(i);
            if (k == null) continue;
            sealedKeys.Add(i);
            k.SetSealed(true);
        }
    }

    public void ClearSeals()
    {
        foreach (int i in sealedKeys)
        {
            PianoKey k = GetKey(i);
            if (k != null) k.SetSealed(false);
        }
        sealedKeys.Clear();
    }

    public bool IsSealed(int index) => sealedKeys.Contains(index);

    // ---------- 시연(자동 연주) ----------

    /// <summary>
    /// 멜로디를 자동으로 연주합니다.
    /// 이 코루틴은 '항상 켜져 있는' 오브젝트(튜토리얼/보스 컨트롤러)에서 StartCoroutine 하세요.
    /// </summary>
    public IEnumerator PlayMelody(MelodyData melody, bool showGuide = true, float speedMultiplier = 1f)
    {
        if (melody == null) yield break;

        int[] seq = melody.GetSequence();
        float dur = Mathf.Max(0.05f, melody.noteDuration / Mathf.Max(0.1f, speedMultiplier));
        float gap = Mathf.Max(0f, melody.noteGap / Mathf.Max(0.1f, speedMultiplier));

        ClearAllGuides();

        for (int i = 0; i < seq.Length; i++)
        {
            int idx = seq[i];
            PianoKey key = GetKey(idx);

            PlayNote(idx);
            if (key != null)
            {
                key.FlashDemo(true);
                if (showGuide) key.SetGuide(true);
            }

            yield return new WaitForSeconds(dur);

            if (key != null)
            {
                key.FlashDemo(false);
                key.SetGuide(false);
            }

            yield return new WaitForSeconds(gap);
        }
    }
}
