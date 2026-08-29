using UnityEngine;

/// <summary>어떤 상황에서 나는 소리인지.</summary>
public enum BattleSfxType
{
    Hit,          // 타격 : 피해가 실제로 들어갔을 때
    Block,        // 완전 방어 : 방어력이 공격력을 다 막아 피해가 0일 때
    Evade,        // 회피 : 공격이 통째로 빗나갔을 때
    Counter,      // 반격 / 공격 반사
    Die,          // 쓰러짐
    ButtonClick,  // 버튼을 눌렀을 때
    ButtonHover   // 버튼에 마우스를 올렸을 때
}

/// <summary>
/// 전투 효과음 재생기.
///
/// 전투 씬에 이 컴포넌트가 붙은 오브젝트를 하나 두고 클립을 넣어두면,
/// 전투 코드 곳곳에서 BattleSfx.Play(...)로 소리를 냅니다.
///
/// 씬에 없거나 클립을 안 넣어두면 아무 소리도 안 날 뿐,
/// 전투 진행에는 전혀 영향을 주지 않습니다.
///
/// 만들기 : Tools → 전투 → [효과음 재생기 추가]
/// </summary>
public class BattleSfx : MonoBehaviour
{
    public static BattleSfx Instance { get; private set; }

    [Header("소리 재생기 (비워두면 알아서 붙입니다)")]
    public AudioSource source;

    [Header("전체 음량")]
    [Range(0f, 1f)] public float masterVolume = 1f;

    [Header("소리 종류별 설정")]
    [Tooltip("클립을 여러 개 넣으면 매번 무작위로 하나를 골라 재생합니다. (같은 소리 반복을 줄여줍니다)")]
    public SfxEntry hit = new SfxEntry();
    public SfxEntry block = new SfxEntry();
    public SfxEntry evade = new SfxEntry();
    public SfxEntry counter = new SfxEntry();
    public SfxEntry die = new SfxEntry();
    public SfxEntry buttonClick = new SfxEntry();
    public SfxEntry buttonHover = new SfxEntry();

    [System.Serializable]
    public class SfxEntry
    {
        public AudioClip[] clips;

        [Range(0f, 1f)] public float volume = 1f;

        [Tooltip("재생할 때마다 음높이를 이 폭 안에서 조금씩 흔듭니다. 0이면 항상 같은 소리가 납니다.")]
        [Range(0f, 0.5f)] public float pitchJitter = 0.08f;

        [Tooltip("이 시간 안에 같은 소리가 또 나오면 건너뜁니다. (한 프레임에 여러 번 겹쳐 터지는 걸 막습니다)")]
        [Range(0f, 0.5f)] public float minInterval = 0.04f;

        [System.NonSerialized] public float lastPlayTime = -999f;

        public AudioClip Pick()
        {
            if (clips == null || clips.Length == 0) return null;
            if (clips.Length == 1) return clips[0];
            return clips[Random.Range(0, clips.Length)];
        }
    }

    [Header("동시에 겹쳐 낼 수 있는 소리 개수")]
    [Range(1, 16)] public int voiceCount = 6;

    private AudioSource[] voices;
    private int nextVoice;

    void Awake()
    {
        Instance = this;

        if (source == null) source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = false;

        // 💥 PlayOneShot으로 나가는 소리도 AudioSource의 pitch를 계속 따라갑니다.
        //    그래서 하나로 돌려쓰면 음높이를 흔드는 순간 먼저 울리던 소리까지 같이 흔들립니다.
        //    소리마다 제 재생기를 주려고 여러 개를 만들어 돌아가며 씁니다.
        voices = new AudioSource[Mathf.Max(1, voiceCount)];
        voices[0] = source;

        for (int i = 1; i < voices.Length; i++)
        {
            var extra = gameObject.AddComponent<AudioSource>();
            extra.playOnAwake = false;
            extra.loop = false;
            extra.outputAudioMixerGroup = source.outputAudioMixerGroup;
            extra.spatialBlend = source.spatialBlend;
            voices[i] = extra;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>어디서든 부를 수 있는 재생 함수. 재생기가 없으면 조용히 넘어갑니다.</summary>
    public static void Play(BattleSfxType type)
    {
        if (Instance == null) return;
        Instance.PlayLocal(type);
    }

    private void PlayLocal(BattleSfxType type)
    {
        SfxEntry entry = EntryOf(type);
        if (entry == null || source == null) return;

        // 너무 짧은 간격으로 같은 소리가 겹치면 지저분해서 건너뜁니다.
        if (Time.unscaledTime - entry.lastPlayTime < entry.minInterval) return;

        AudioClip clip = entry.Pick();
        if (clip == null) return;

        entry.lastPlayTime = Time.unscaledTime;

        AudioSource voice = NextVoice();
        voice.pitch = 1f + Random.Range(-entry.pitchJitter, entry.pitchJitter);
        voice.PlayOneShot(clip, entry.volume * masterVolume);
    }

    /// <summary>돌아가며 쓸 재생기를 고릅니다. 놀고 있는 게 있으면 그걸 먼저 씁니다.</summary>
    private AudioSource NextVoice()
    {
        if (voices == null || voices.Length == 0) return source;

        for (int i = 0; i < voices.Length; i++)
        {
            AudioSource candidate = voices[(nextVoice + i) % voices.Length];
            if (candidate != null && !candidate.isPlaying)
            {
                nextVoice = (nextVoice + i + 1) % voices.Length;
                return candidate;
            }
        }

        // 전부 울리는 중이면 그냥 순서대로 덮어씁니다.
        AudioSource fallback = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;
        return fallback != null ? fallback : source;
    }

    private SfxEntry EntryOf(BattleSfxType type)
    {
        switch (type)
        {
            case BattleSfxType.Hit:         return hit;
            case BattleSfxType.Block:       return block;
            case BattleSfxType.Evade:       return evade;
            case BattleSfxType.Counter:     return counter;
            case BattleSfxType.Die:         return die;
            case BattleSfxType.ButtonClick: return buttonClick;
            case BattleSfxType.ButtonHover: return buttonHover;
            default: return null;
        }
    }
}
