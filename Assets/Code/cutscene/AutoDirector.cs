using UnityEngine;
using System.Collections;
using System.Collections.Generic; 
using UnityEngine.UI;
using UnityEngine.SceneManagement; 

[System.Serializable]
public class SpriteChangeAction
{
    public SpriteRenderer targetRenderer; 
    public Sprite newSprite;              
}

[System.Serializable]
public class MapCharacterAction
{
    public SpriteRenderer mapSprite; 

    [Header("순간이동 (이동 전 즉시 위치 이동)")]
    public Transform teleportPoint; 

    [Header("페이드 연출 (체크 시 사용)")]
    public bool useFade = false; 
    public bool isFadeIn = true; 
    public float fadeSpeed = 3f;

    [Header("이동 연출 (비워두면 이동 안 함)")]
    public Transform moveTargetPoint;
    public float moveSpeed = 3f;
    public bool useSmoothMove = false; // 감속 이동
    
    // 💥 새롭게 추가: 밀려날 때 원래 보던 방향을 유지하는 기능
    public bool disableAutoFlip = false; 
}

// 글리치 재생 방식. TurnOn으로 켜두면 TurnOff를 만날 때까지 계속 지직거립니다.
public enum GlitchMode { None, Burst, TurnOn, TurnOff }

[System.Serializable]
public class SequenceStep
{
    [Header("1. 주인공 이동 및 순간이동 설정")]
    public Transform playerTeleportPoint;
    public Transform targetPoint;
    public bool playerSmoothMove = false;

    // 💥 새롭게 추가: 주인공 방향 전환 끄기
    public bool playerDisableAutoFlip = false;

    [Header("1.5 주인공 이미지 변경 (표정, 의상 등)")]
    [Tooltip("지정하면 이 스텝 동안 주인공이 이 포즈로 고정됩니다. 이동 중에도 유지됩니다.\n" +
             "비워두면(None) 기본 걷기/대기 애니메이션으로 돌아갑니다.")]
    public Sprite playerNewSprite;
    
    [Header("1.6 이 스텝만 걷는 속도 바꾸기")]
    [Tooltip("0이면 Director의 기본 Walk Speed를 그대로 씁니다.\n" +
             "값을 넣으면 이 스텝에서만 그 속도로 걷습니다. (작을수록 느림)")]
    public float playerWalkSpeed = 0f;

    [Header("2. 다른 캐릭터들 행동 (순간이동/등장/퇴장/이동)")] 
    public List<MapCharacterAction> npcActions;

    [Header("2.5 특정 오브젝트 이미지 변경 (문 열림, 표정 변화 등)")]
    public List<SpriteChangeAction> spriteChanges;

    [Header("3. 이 지점 도착 후 재생할 컷씬")]
    public CutsceneManager cutscene;

    [Header("3.5 컷씬 종료 후 맞출 퍼즐 (비워두면 안 함)")]
    [Tooltip("퍼즐 화면이 이 씬 위에 덮여서 열립니다. 다 맞추고 나면 이 씬은 그대로 살아있으므로\n" +
             "여기서부터 다음 스텝으로 자연스럽게 이어집니다.")]
    public PuzzleDefinition puzzle;

    [Header("4. 컷씬 종료 후 켤 배경 오브젝트")]
    public GameObject backgroundToTurnOn; 

    [Header("5. 사운드 재생 (발소리, 피아노 등 여러 개 추가 가능)")]
    public List<AudioClip> sfxClips; 

    [Header("5.5 사운드 (소리마다 음량 / 음높이 개별 조절)")]
    [Tooltip("위 5번과 달리 클립마다 음량·음높이·지연을 따로 정할 수 있습니다.\n" +
             "앞으로는 이쪽을 쓰시는 걸 권합니다.")]
    public List<SoundCue> stepSounds;

    [Header("6. 화면 연출 (흔들림 / 전체 화면 암전)")]
    public bool useCameraShake = false;
    public float shakeDuration = 0.5f;
    
    public bool useGlobalFade = false;
    public bool isGlobalFadeIn = true;

    [Header("6.5 치지직 글리치 (암전보다 먼저 처리됩니다)")]
    [Tooltip("None: 사용 안 함\n" +
             "Burst: Duration 동안만 재생하고 끝날 때까지 기다림\n" +
             "TurnOn: 계속 켜둠 (기다리지 않고 다음 연출로 진행, TurnOff 할 때까지 유지)\n" +
             "TurnOff: 켜둔 글리치를 끔")]
    public GlitchMode glitchMode = GlitchMode.None;
    public float glitchDuration = 0.6f;
    [Range(0f, 1f)] public float glitchIntensity = 1f;

    [Header("7. 대화 없이 가만히 멈춰있을 시간 (초)")]
    public float waitTime = 0f; 

    [Header("7.5 이 스텝에서는 발소리를 끄기")]
    public bool muteFootstep = false;

    [Header("8. 연출 종료 후 넘어갈 씬 이름 (전투 등)")] 
    public string loadSceneName = ""; 
}

public class AutoDirector : MonoBehaviour
{
    [Header("등장인물 & 애니메이션")]
    public Transform playerCharacter;
    public Animator playerAnim;
    public SpriteRenderer playerSprite; 
    public float walkSpeed = 3f;

    [Header("씬에 배치된 전체 배경 리스트 (전환 시 나머지를 끄기 위함)")]
    public List<GameObject> allBackgrounds; 

    [Header("시스템 연출 컴포넌트")]
    public AudioSource audioSource;
    public Image globalFadeImage;
    public Transform mainCameraTransform;
    public GlitchEffect glitchEffect;

    [Header("걷는 소리 (다른 효과음과 음량이 따로 놉니다)")]
    [Tooltip("주인공이 이동하는 동안 재생할 발소리. 비워두면 발소리 없음.")]
    public AudioClip footstepClip;

    [Range(0f, 1f)]
    [Tooltip("발소리 음량. 5번 사운드(sfxClips)와 완전히 별개로 조절됩니다.")]
    public float footstepVolume = 0.5f;

    [Tooltip("체크하면 걷는 내내 반복 재생, 해제하면 걷기 시작할 때 한 번만 재생합니다.")]
    public bool loopFootstep = true;

    [Tooltip("발소리 전용 AudioSource. 비워두면 자동으로 하나 만들어 씁니다.")]
    public AudioSource footstepSource;

    [Header("예전 5번 Sfx Clips도 스텝이 끝나면 끄기")]
    [Tooltip("5.5번 Step Sounds는 항상 스텝 끝에 꺼집니다.\n" +
             "이 항목은 예전 방식(5번 Sfx Clips)에도 같은 규칙을 적용할지 정합니다.")]
    public bool stopLegacySfxOnStepEnd = true;

    [Header("연출 시퀀스")]
    public SequenceStep[] sequence;

    void Start()
    {
        StartCoroutine(PlaySequence());
    }

    IEnumerator PlaySequence()
    {
        for (int i = 0; i < sequence.Length; i++)
        {
            var step = sequence[i];

            if (step.sfxClips != null && step.sfxClips.Count > 0 && audioSource != null)
            {
                foreach (var clip in step.sfxClips)
                {
                    audioSource.PlayOneShot(clip);
                }
            }

            // 💥 소리마다 음량·음높이를 따로 정할 수 있는 쪽. SfxKit이 재생기를 나눠서 관리합니다.
            SfxHandle stepSfx = null;
            if (step.stepSounds != null && step.stepSounds.Count > 0)
            {
                stepSfx = SfxKit.PlayAll(step.stepSounds);
            }

            if (step.useCameraShake && mainCameraTransform != null)
            {
                StartCoroutine(CameraShake(step.shakeDuration));
            }

            if (glitchEffect != null)
            {
                switch (step.glitchMode)
                {
                    case GlitchMode.Burst:
                        // 다 끝날 때까지 기다렸다가 다음 연출로 넘어갑니다.
                        yield return StartCoroutine(glitchEffect.Play(step.glitchDuration, step.glitchIntensity));
                        break;

                    case GlitchMode.TurnOn:
                        // 켜두기만 하고 바로 진행합니다. 대사/이동 내내 지직거립니다.
                        glitchEffect.StartContinuous(step.glitchIntensity);
                        break;

                    case GlitchMode.TurnOff:
                        glitchEffect.StopNow();
                        break;
                }
            }

            if (step.useGlobalFade && globalFadeImage != null)
            {
                yield return StartCoroutine(GlobalFade(step.isGlobalFadeIn));
            }

            // 💥 주인공 이미지 변경 (전용 필드). 범용 spriteChanges와 같은 타이밍(스텝 시작 시)에 적용됩니다.
            if (playerSprite != null)
            {
                if (step.playerNewSprite != null)
                {
                    // 포즈 지정: Animator가 매 프레임 m_Sprite를 덮어쓰므로 꺼서 포즈를 고정합니다.
                    // Animator가 꺼진 동안에는 이동 중에도 이 포즈가 그대로 유지됩니다.
                    if (playerAnim != null) playerAnim.enabled = false;
                    playerSprite.sprite = step.playerNewSprite;
                }
                else if (playerAnim != null)
                {
                    // 포즈 미지정: 기본 idle/Walk 애니메이션으로 되돌립니다.
                    playerAnim.enabled = true;
                }
            }

            if (step.spriteChanges != null && step.spriteChanges.Count > 0)
            {
                foreach (var change in step.spriteChanges)
                {
                    if (change.targetRenderer != null && change.newSprite != null)
                    {
                        ApplySpriteManually(change.targetRenderer, change.newSprite);
                    }
                }
            }

            if (step.playerTeleportPoint != null)
            {
                playerCharacter.position = step.playerTeleportPoint.position;
            }

            if (step.targetPoint != null)
            {
                // 💥 방향 유지 변수(playerDisableAutoFlip) 추가 전달
                yield return StartCoroutine(MoveCharacter(step.targetPoint.position, step.playerSmoothMove, step.playerDisableAutoFlip, step.muteFootstep, step.playerWalkSpeed));
            }

            if (step.npcActions != null && step.npcActions.Count > 0)
            {
                List<Coroutine> activeCoroutines = new List<Coroutine>();

                foreach (var action in step.npcActions)
                {
                    if (action.mapSprite != null)
                    {
                        if (action.teleportPoint != null)
                        {
                            action.mapSprite.transform.position = action.teleportPoint.position;
                        }

                        if (action.useFade) 
                            activeCoroutines.Add(StartCoroutine(FadeMapSprite(action)));
                        if (action.moveTargetPoint != null) 
                            activeCoroutines.Add(StartCoroutine(MoveNPC(action)));
                    }
                }

                foreach (var c in activeCoroutines)
                {
                    yield return c;
                }
            }

            if (step.cutscene != null)
            {
                step.cutscene.gameObject.SetActive(true);
                while (!step.cutscene.isCutsceneDone)
                {
                    yield return null;
                }
            }

            // 💥 퍼즐. 이 씬 위에 퍼즐 화면을 덮어 씌웠다가 걷어냅니다.
            //    씬을 갈아엎지 않으므로 배경/캐릭터 위치/지나간 대사가 그대로 남아
            //    끝난 뒤 다음 스텝부터 이어서 진행됩니다.
            if (step.puzzle != null)
            {
                yield return StartCoroutine(PuzzleRunner.Play(step.puzzle));
            }

            if (step.waitTime > 0f)
            {
                yield return new WaitForSeconds(step.waitTime);
            }

            if (step.backgroundToTurnOn != null)
            {
                if (allBackgrounds != null)
                {
                    foreach (var bg in allBackgrounds)
                    {
                        if (bg != null) bg.SetActive(false);
                    }
                }
                step.backgroundToTurnOn.SetActive(true);
            }

            if (!string.IsNullOrEmpty(step.loadSceneName))
            {
                if (step.backgroundToTurnOn != null)
                {
                    BattleContext.currentStageID = step.backgroundToTurnOn.name;
                }
                yield return new WaitForSeconds(0.5f);

                // 💥 어두워진 뒤에 씬을 넘깁니다. (ScreenFader가 없으면 즉시 전환)
                StopStepSounds(stepSfx);
                yield return StartCoroutine(ScreenFader.TransitionTo(step.loadSceneName));
                yield break;
            }

            // 💥 이 스텝이 켠 소리는 스텝이 끝나면 함께 끕니다.
            //    (SoundCue의 Keep Playing After Step을 켠 소리는 그대로 이어집니다)
            StopStepSounds(stepSfx);
        }
    }

    /// <summary>
    /// 이 스텝이 켠 소리를 끕니다.
    /// 5.5번 Step Sounds는 손잡이로 정확히 끄고,
    /// 예전 5번 Sfx Clips는 Director의 audioSource를 통째로 멈춰서 끕니다.
    /// </summary>
    void StopStepSounds(SfxHandle handle)
    {
        if (handle != null) handle.Stop();

        if (stopLegacySfxOnStepEnd && audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    // 💥 Animator가 붙어있는 오브젝트의 스프라이트를 수동으로 바꿀 때 쓰는 헬퍼.
    // Animator를 끄지 않으면 애니메이션 클립이 매 프레임 m_Sprite를 되돌려 깜빡입니다.
    static void ApplySpriteManually(SpriteRenderer renderer, Sprite sprite)
    {
        if (renderer == null || sprite == null) return;

        var anim = renderer.GetComponent<Animator>();
        if (anim != null && anim.enabled) anim.enabled = false;

        renderer.sprite = sprite;
    }

    IEnumerator CameraShake(float duration)
    {
        Vector3 originalPos = mainCameraTransform.localPosition;
        float elapsed = 0.0f;
        while (elapsed < duration)
        {
            float x = Random.Range(-0.1f, 0.1f);
            float y = Random.Range(-0.1f, 0.1f);
            mainCameraTransform.localPosition = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);
            elapsed += Time.deltaTime;
            yield return null;
        }
        mainCameraTransform.localPosition = originalPos;
    }

    IEnumerator GlobalFade(bool isFadeIn)
    {
        float targetAlpha = isFadeIn ? 0f : 1f; 
        Color c = globalFadeImage.color;
        while (Mathf.Abs(c.a - targetAlpha) > 0.05f)
        {
            c.a = Mathf.Lerp(c.a, targetAlpha, Time.deltaTime * 3f);
            globalFadeImage.color = c;
            yield return null;
        }
        c.a = targetAlpha;
        globalFadeImage.color = c;
    }

    // ─────────────────────────── 걷는 소리 ───────────────────────────

    /// <summary>
    /// 발소리 전용 AudioSource를 준비합니다.
    /// 5번 사운드(sfxClips)가 쓰는 audioSource와 일부러 분리해서,
    /// 발소리 음량만 따로 조절할 수 있게 합니다.
    /// </summary>
    AudioSource EnsureFootstepSource()
    {
        if (footstepSource != null) return footstepSource;

        var go = new GameObject("FootstepSource");
        go.transform.SetParent(transform, false);

        footstepSource = go.AddComponent<AudioSource>();
        footstepSource.playOnAwake = false;
        footstepSource.spatialBlend = 0f;

        return footstepSource;
    }

    void StartFootsteps()
    {
        if (footstepClip == null) return;

        var src = EnsureFootstepSource();
        if (src == null) return;

        src.clip = footstepClip;
        src.volume = footstepVolume;
        src.loop = loopFootstep;
        src.Play();
    }

    void StopFootsteps()
    {
        if (footstepSource == null) return;
        if (!footstepSource.loop && footstepSource.isPlaying) return; // 한 번 재생은 끝까지 들려줍니다.

        footstepSource.Stop();
    }

    // 💥 주인공 이동 코루틴 
    IEnumerator MoveCharacter(Vector3 targetPos, bool smoothMove, bool disableFlip, bool muteFootstep, float speedOverride)
    {
        // 💥 Animator가 꺼져 있으면 수동 포즈 유지 중이므로 걷기 파라미터를 건드리지 않습니다.
        bool animDriven = playerAnim != null && playerAnim.enabled;
        if (animDriven) playerAnim.SetBool("IsWalking", true);

        // 💥 disableFlip이 꺼져(false) 있을 때만 자동 방향 전환
        if (!disableFlip && playerSprite != null)
        {
            if (targetPos.x > playerCharacter.position.x) playerSprite.flipX = true;  
            else if (targetPos.x < playerCharacter.position.x) playerSprite.flipX = false; 
        }

        // 💥 이 스텝에 속도가 지정돼 있으면 그걸 쓰고, 0이면 Director 기본값을 씁니다.
        float speed = speedOverride > 0.01f ? speedOverride : walkSpeed;

        // 💥 걷기 시작 — 발소리는 전용 AudioSource라 음량이 따로 놉니다.
        if (!muteFootstep) StartFootsteps();

        while (Vector2.Distance(playerCharacter.position, targetPos) > 0.05f)
        {
            if (smoothMove)
                playerCharacter.position = Vector3.Lerp(playerCharacter.position, targetPos, speed * Time.deltaTime);
            else
                playerCharacter.position = Vector3.MoveTowards(playerCharacter.position, targetPos, speed * Time.deltaTime);
            
            yield return null;
        }
        
        playerCharacter.position = targetPos;
        StopFootsteps();
        if (animDriven) playerAnim.SetBool("IsWalking", false);
    }

    // 💥 NPC 이동 코루틴
    IEnumerator MoveNPC(MapCharacterAction action)
    {
        // 💥 disableAutoFlip이 꺼져(false) 있을 때만 자동 방향 전환
        if (!action.disableAutoFlip)
        {
            if (action.moveTargetPoint.position.x > action.mapSprite.transform.position.x)
                action.mapSprite.flipX = true;
            else if (action.moveTargetPoint.position.x < action.mapSprite.transform.position.x)
                action.mapSprite.flipX = false;
        }

        while (Vector2.Distance(action.mapSprite.transform.position, action.moveTargetPoint.position) > 0.05f)
        {
            if (action.useSmoothMove)
                action.mapSprite.transform.position = Vector3.Lerp(action.mapSprite.transform.position, action.moveTargetPoint.position, action.moveSpeed * Time.deltaTime);
            else
                action.mapSprite.transform.position = Vector3.MoveTowards(action.mapSprite.transform.position, action.moveTargetPoint.position, action.moveSpeed * Time.deltaTime);
            
            yield return null;
        }
        
        action.mapSprite.transform.position = action.moveTargetPoint.position; 
    }

    IEnumerator FadeMapSprite(MapCharacterAction action)
    {
        float targetAlpha = action.isFadeIn ? 1f : 0f;
        Color c = action.mapSprite.color;
        while (Mathf.Abs(c.a - targetAlpha) > 0.05f)
        {
            c.a = Mathf.Lerp(c.a, targetAlpha, Time.deltaTime * action.fadeSpeed);
            action.mapSprite.color = c;
            yield return null;
        }
        c.a = targetAlpha;
        action.mapSprite.color = c;
    }
}
