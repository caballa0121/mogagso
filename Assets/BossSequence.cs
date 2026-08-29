using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>웨이브 하나의 설정</summary>
[System.Serializable]
public class BossWave
{
    public string waveName = "1웨이브";

    [Tooltip("웨이브 시작 전 나올 대사 (선택)")]
    public DialogueData introDialogue;

    [Tooltip("덩쿨에 도달해 E로 해체하면 클리어됩니다. 이 값은 안전장치용 최대 제한시간(초)이며 0이면 무제한")]
    public float safetyTimeLimit = 0f;

    [Header("이번 웨이브에 동작할 스포너")]
    public ProjectileSpawner[] spawners;

    [Header("발사 설정")]
    public WaveSpawnSettings spawnSettings = new WaveSpawnSettings();

    [Header("클리어 시 해제할 덩쿨")]
    public VineBarrier vineToUnlock;

    [Header("사망 시 되돌아갈 위치 (비우면 보스맵 시작점)")]
    public Transform respawnPoint;
}

/// <summary>
/// 튜토리얼 종료 → 암전 → 보스맵 이동 → 대사 → 웨이브 3회 → 피아노 도달 → 최종 연주 클리어
///
/// '항상 켜져 있는' 오브젝트(예: GameSystems)에 붙이세요.
/// 보스맵 오브젝트에 붙이면 맵이 꺼져 있을 때 동작하지 않습니다.
/// </summary>
public class BossSequence : MonoBehaviour
{
    [Header("── 1. 튜토리얼 연결 ──")]
    [Tooltip("이 튜토리얼이 끝나면 자동으로 보스 시퀀스가 시작됩니다")]
    public PianoTutorial tutorial;
    [Tooltip("튜토리얼 마지막 대사 후 대기 시간(초)")]
    public float delayAfterTutorial = 2f;

    [Header("── 2. 전환 연출 ──")]
    [Tooltip("암전용 CanvasGroup. 비우면 PianoScreenFader.Instance를 사용합니다")]
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 0.6f;
    [Tooltip("암전 상태를 유지하는 시간(초)")]
    public float blackoutHold = 0.5f;

    [Header("── 3. 보스맵 이동 ──")]
    public Transform playerTransform;
    [Tooltip("보스맵의 복도 시작 지점")]
    public Transform bossMapSpawnPoint;
    [Tooltip("보스맵 오브젝트 (꺼져 있다면 켜줍니다)")]
    public GameObject bossMapRoot;
    public GameObject currentVCam;   // 마을 카메라
    public GameObject targetVCam;    // 보스맵 카메라

    [Header("── 4. 보스 등장 대사 ──")]
    public DialogueData bossIntroDialogue;
    [Tooltip("체크하면 암전된 상태에서 대사가 나옵니다")]
    public bool dialogueDuringBlackout = false;

    [Header("── 5. 웨이브 구성 ──")]
    public BossWave[] waves;
    [Tooltip("웨이브 사이 휴식 시간(초)")]
    public float restBetweenWaves = 2f;

    [Header("── 6. 최종 피아노 ──")]
    [Tooltip("복도 끝 피아노의 위치")]
    public Transform pianoApproachPoint;
    public float pianoReachDistance = 2.5f;
    [Tooltip("피아노에 도달했을 때 나올 대사")]
    public DialogueData pianoArrivalDialogue;
    [Tooltip("정확히 눌러야 클리어되는 멜로디")]
    public MelodyData finalMelody;
    [Tooltip("피아노 건반 UI 패널 (PianoManager가 붙은 곳)")]
    public GameObject pianoUI;
    [Tooltip("체크하면 먼저 한 번 들려줍니다")]
    public bool demonstrateFinalMelody = true;
    [Tooltip("체크하면 눌러야 할 건반이 빛납니다")]
    public bool showGuideOnFinal = false;
    [Tooltip("최종 연주를 틀렸을 때 처음부터 다시 (해제하면 진행도 유지)")]
    public bool resetOnFinalMistake = true;

    [Header("── 7. 클리어 ──")]
    public DialogueData clearDialogue;

    [Header("── 7.5 클리어 후 : 퍼즐 → 대사 → 다음 씬 ──")]
    [Tooltip("클리어 대사가 끝난 뒤 맞출 퍼즐. 비우면 퍼즐 없이 넘어갑니다.\n" +
             "퍼즐 화면은 이 씬 위에 덮여서 열렸다 걷히므로 씬 상태가 그대로 유지됩니다.")]
    public PuzzleDefinition clearPuzzle;

    [Tooltip("퍼즐을 다 맞춘 뒤 나올 대사. 비우면 건너뜁니다.")]
    public DialogueData afterPuzzleDialogue;

    [Tooltip("전부 끝난 뒤 넘어갈 씬 이름 (예: CHAPTER 3). 비우면 씬을 넘기지 않습니다.")]
    public string nextSceneName = "";

    [Header("── 8. 플레이어 제어 ──")]
    [Tooltip("PlayerController를 드래그")]
    public MonoBehaviour playerMovementScript;

    [Header("── 9. HUD (선택) ──")]
    public GameObject bossHudRoot;
    public TextMeshProUGUI waveText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI playerHpText;

    [Header("── 자동 연결 (참조가 비었을 때 씬에서 자동으로 찾음) ──")]
    [Tooltip("Waves의 덩쿨/스포너가 비어 있으면 씬에서 자동으로 찾아 채웁니다")]
    public bool autoWireIfEmpty = true;

    [Header("── 디버그 ──")]
    public bool autoStartOnTutorialEnd = true;
    public bool debugLog = true;

    private bool sequenceStarted = false;
    private bool playerDied = false;
    private MelodyChecker activeChecker;

    // ==================================================================
    //  진입
    // ==================================================================

    private void Start()
    {
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        if (bossHudRoot != null) bossHudRoot.SetActive(false);

        if (autoWireIfEmpty) AutoWire();
        ValidateSetup();

        // 시작 시 덩쿨 전부 세워두기
        if (waves != null)
        {
            foreach (BossWave w in waves)
            {
                if (w != null && w.vineToUnlock != null) w.vineToUnlock.Lock();
            }
        }
    }

    // ==================================================================
    //  자동 연결 & 검증  (문제 1, 5 해결)
    // ==================================================================

    /// <summary>
    /// Waves의 덩쿨/스포너 참조가 비어 있으면 씬에서 찾아 채웁니다.
    /// 스크립트 교체로 인스펙터 참조가 날아갔을 때의 안전장치입니다.
    /// </summary>
    [ContextMenu("설정 자동 채우기")]
    public void AutoWire()
    {
        if (waves == null || waves.Length == 0)
        {
            Debug.LogError("[BossSequence] Waves 배열이 비어 있습니다! Size를 3으로 설정하세요.");
            return;
        }

        // --- 씬의 덩쿨을 복도 순서대로 정렬 ---
        List<VineBarrier> vines = new List<VineBarrier>(
            FindObjectsByType<VineBarrier>(FindObjectsInactive.Include));

        if (bossMapSpawnPoint != null)
        {
            Vector3 origin = bossMapSpawnPoint.position;
            vines.Sort((a, b) =>
                Vector3.Distance(a.transform.position, origin)
                .CompareTo(Vector3.Distance(b.transform.position, origin)));
        }

        // --- 씬의 스포너 수집 ---
        List<ProjectileSpawner> spawners = new List<ProjectileSpawner>(
            FindObjectsByType<ProjectileSpawner>(FindObjectsInactive.Include));

        // 정렬 순서가 보장되지 않으므로 이름순으로 고정합니다
        spawners.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

        int wiredVines = 0, wiredSpawners = 0;

        for (int i = 0; i < waves.Length; i++)
        {
            BossWave w = waves[i];
            if (w == null) continue;

            // 덩쿨: i번째 웨이브 → i번째 덩쿨
            if (w.vineToUnlock == null && i < vines.Count)
            {
                w.vineToUnlock = vines[i];
                wiredVines++;
            }

            // 스포너: 웨이브가 진행될수록 하나씩 늘려서 배정
            if ((w.spawners == null || w.spawners.Length == 0) && spawners.Count > 0)
            {
                int count = Mathf.Min(i + 1, spawners.Count);
                w.spawners = spawners.GetRange(0, count).ToArray();
                wiredSpawners += count;
            }
        }

        if (wiredVines > 0 || wiredSpawners > 0)
        {
            Debug.Log($"[BossSequence] 자동 연결 완료 — 덩쿨 {wiredVines}개, 스포너 {wiredSpawners}개");
        }
    }

    /// <summary>세팅이 제대로 됐는지 콘솔에 보고합니다.</summary>
    [ContextMenu("설정 검사")]
    public void ValidateSetup()
    {
        if (waves == null || waves.Length == 0)
        {
            Debug.LogError("[BossSequence] Waves 배열이 비어 있습니다!");
            return;
        }

        for (int i = 0; i < waves.Length; i++)
        {
            BossWave w = waves[i];
            string tag = $"[BossSequence] {(w != null ? w.waveName : $"웨이브{i + 1}")}";

            if (w == null)
            {
                Debug.LogError($"{tag}: 웨이브 데이터가 null입니다.");
                continue;
            }

            if (w.vineToUnlock == null)
                Debug.LogError($"{tag}: 해제할 덩쿨(Vine To Unlock)이 비어 있습니다!");

            if (w.spawners == null || w.spawners.Length == 0)
            {
                Debug.LogError($"{tag}: 스포너(Spawners)가 비어 있습니다! 투사체가 발사되지 않습니다.");
            }
            else
            {
                for (int k = 0; k < w.spawners.Length; k++)
                {
                    if (w.spawners[k] == null)
                        Debug.LogError($"{tag}: Spawners[{k}]가 비어 있습니다(None).");
                    else if (w.spawners[k].projectilePrefab == null)
                        Debug.LogError($"{tag}: {w.spawners[k].name}에 Projectile Prefab이 없습니다!");
                }
            }
        }

        if (playerTransform == null) Debug.LogError("[BossSequence] Player Transform을 찾지 못했습니다!");
        if (PlayerHealth.Instance == null) Debug.LogWarning("[BossSequence] 플레이어에 PlayerHealth가 없습니다.");
    }

    private void Update()
    {
        if (sequenceStarted || !autoStartOnTutorialEnd) return;
        if (tutorial == null || !tutorial.HasCompleted) return;

        // 튜토리얼 대사가 완전히 끝난 뒤에만
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive) return;

        StartBossSequence();
    }

    [ContextMenu("테스트: 보스 시퀀스 시작")]
    public void StartBossSequence()
    {
        if (sequenceStarted) return;
        sequenceStarted = true;
        StartCoroutine(RunSequence());
    }

    // ==================================================================
    //  메인 시퀀스
    // ==================================================================

    private IEnumerator RunSequence()
    {
        Log("보스 시퀀스 시작");

        // ---------- 1. 2초 대기 ----------
        yield return new WaitForSeconds(delayAfterTutorial);

        // ---------- 2. 암전 ----------
        SetPlayerMovement(false);
        yield return FadeTo(1f);
        yield return new WaitForSeconds(blackoutHold);

        // ---------- 3. 보스맵으로 이동 ----------
        if (bossMapRoot != null && !bossMapRoot.activeSelf) bossMapRoot.SetActive(true);

        TeleportPlayer(bossMapSpawnPoint);

        if (currentVCam != null) currentVCam.SetActive(false);
        if (targetVCam != null) targetVCam.SetActive(true);

        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.2f);

        // ---------- 4. 대사 ----------
        if (dialogueDuringBlackout)
        {
            yield return RunDialogue(bossIntroDialogue);
            yield return FadeTo(0f);
        }
        else
        {
            yield return FadeTo(0f);
            yield return RunDialogue(bossIntroDialogue);
        }

        // ---------- 5. 전투 준비 ----------
        if (bossHudRoot != null) bossHudRoot.SetActive(true);

        PlayerHealth hp = PlayerHealth.Instance;
        if (hp != null)
        {
            hp.ResetHP();
            hp.OnDeath += HandlePlayerDeath;
            hp.OnHPChanged += UpdateHpUI;
            UpdateHpUI(hp.CurrentHP);
        }

        SetPlayerMovement(true);

        // ---------- 6. 웨이브 루프 ----------
        for (int i = 0; i < waves.Length; i++)
        {
            yield return RunWave(waves[i], i);
            if (i < waves.Length - 1) yield return new WaitForSeconds(restBetweenWaves);
        }

        // ---------- 7. 피아노 도달 대기 ----------
        if (waveText != null) waveText.text = "피아노로!";
        if (timerText != null) timerText.text = "";
        if (PianoFX.Instance != null) PianoFX.Instance.Toast("길이 열렸다. 피아노로 가라!", 2.5f);

        yield return WaitForPianoApproach();

        // ---------- 8. 최종 연주 ----------
        if (bossHudRoot != null) bossHudRoot.SetActive(false);
        if (hp != null) hp.SetInvincible(true);

        yield return RunDialogue(pianoArrivalDialogue);
        yield return FinalMelodyChallenge();

        // ---------- 9. 클리어 ----------
        if (PianoFX.Instance != null)
        {
            PianoFX.Instance.Flash(Color.white, 0.9f, 1.2f);
            PianoFX.Instance.Toast("클리어!", 2.5f);
        }
        yield return new WaitForSeconds(1.2f);

        yield return RunDialogue(clearDialogue);

        // ---------- 9.5 퍼즐 ----------
        // 퍼즐 화면은 이 씬 위에 덮여서 열립니다. 씬을 떠나지 않으므로
        // 보스맵 상태와 플레이어 위치가 그대로 남아 있습니다.
        if (clearPuzzle != null)
        {
            // 퍼즐이 떠 있는 동안 플레이어가 뒤에서 돌아다니지 않게 잠급니다.
            SetPlayerMovement(false);
            yield return PuzzleRunner.Play(clearPuzzle);
        }

        // ---------- 9.6 퍼즐 후 대사 ----------
        yield return RunDialogue(afterPuzzleDialogue);

        // 정리
        if (hp != null)
        {
            hp.OnDeath -= HandlePlayerDeath;
            hp.OnHPChanged -= UpdateHpUI;
            hp.SetInvincible(false);
        }
        SetPlayerMovement(true);
        Log("보스 시퀀스 완료");

        // ---------- 10. 다음 씬 ----------
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SetPlayerMovement(false);
            yield return FadeTo(1f);
            SceneManager.LoadScene(nextSceneName);
        }
    }

    // ==================================================================
    //  웨이브
    // ==================================================================

    private IEnumerator RunWave(BossWave wave, int index)
    {
        if (wave == null) yield break;

        // 웨이브 소개 대사
        if (wave.introDialogue != null)
        {
            StopAllSpawners(wave);
            yield return RunDialogue(wave.introDialogue);
        }

        bool waveCleared = false;

        while (!waveCleared)
        {
            playerDied = false;

            if (waveText != null) waveText.text = wave.waveName;
            if (PianoFX.Instance != null) PianoFX.Instance.Toast(wave.waveName, 1.5f);

            yield return new WaitForSeconds(0.8f);

            // ---------- 이번 웨이브의 덩쿨 (문제 1 해결) ----------
            VineBarrier vine = wave.vineToUnlock;

            if (vine == null && autoWireIfEmpty)
            {
                // 인스펙터 참조가 날아간 경우를 대비한 런타임 복구
                AutoWire();
                vine = wave.vineToUnlock;
            }

            if (vine == null)
            {
                Debug.LogError($"[BossSequence] {wave.waveName}에 해제할 덩쿨이 지정되지 않았습니다! " +
                               "인스펙터의 Vine To Unlock을 연결하거나, " +
                               "컴포넌트 우클릭 → '설정 자동 채우기'를 실행하세요.");
                waveCleared = true;
                break;
            }

            vine.EnableDismantle(playerTransform);

            if (PianoFX.Instance != null)
                PianoFX.Instance.Toast("덩쿨까지 접근해 E로 해체하라!", 2.5f);

            // ---------- 스포너 가동 (문제 5 해결) ----------
            int startedSpawners = 0;

            if (wave.spawners != null)
            {
                foreach (ProjectileSpawner sp in wave.spawners)
                {
                    if (sp == null) continue;

                    // 스포너 오브젝트가 꺼져 있으면 코루틴이 시작되지 않습니다
                    if (!sp.gameObject.activeInHierarchy)
                    {
                        Debug.LogWarning($"[BossSequence] 스포너 '{sp.name}'가 꺼져 있어 자동으로 켭니다.", sp);
                        sp.gameObject.SetActive(true);
                    }

                    // 컴포넌트 자체가 꺼져 있어도 마찬가지
                    if (!sp.enabled) sp.enabled = true;

                    sp.BeginWave(wave.spawnSettings, playerTransform);
                    startedSpawners++;
                }
            }

            if (startedSpawners == 0)
            {
                Debug.LogError($"[BossSequence] {wave.waveName}: 가동된 스포너가 0개입니다! " +
                               "인스펙터의 Spawners를 확인하거나 Auto Wire If Empty를 켜세요.");
            }
            else
            {
                Log($"{wave.waveName}: 스포너 {startedSpawners}개 가동");
            }

            // 해체 완료 or 사망 대기
            float elapsed = 0f;
            bool timedOut = false;

            while (!vine.DismantleCompleted && !playerDied)
            {
                elapsed += Time.deltaTime;

                if (timerText != null)
                    timerText.text = Mathf.FloorToInt(elapsed).ToString() + "s";

                if (wave.safetyTimeLimit > 0f && elapsed >= wave.safetyTimeLimit)
                {
                    timedOut = true;
                    break;
                }
                yield return null;
            }

            // 정리
            StopAllSpawners(wave);
            BossProjectile.ClearAll();

            if (playerDied || timedOut)
            {
                vine.DisableDismantle();
                yield return HandleWaveRestart(wave);
                continue;   // 같은 웨이브 재시작
            }

            waveCleared = true;

            // 해체 연출이 끝날 때까지 대기 (콜라이더는 이미 풀린 상태)
            if (PianoFX.Instance != null) PianoFX.Instance.Toast("덩쿨이 갈라졌다!", 2f);
            while (vine.IsAnimating) yield return null;
        }

        if (timerText != null) timerText.text = "";
    }

    private void StopAllSpawners(BossWave wave)
    {
        if (wave.spawners == null) return;
        foreach (ProjectileSpawner sp in wave.spawners)
        {
            if (sp != null) sp.StopWave();
        }
    }

    private void HandlePlayerDeath()
    {
        playerDied = true;
    }

    private IEnumerator HandleWaveRestart(BossWave wave)
    {
        Log("플레이어 사망 → 웨이브 재시작");

        if (PianoFX.Instance != null) PianoFX.Instance.Toast("쓰러졌다...", 1.5f);

        SetPlayerMovement(false);
        yield return new WaitForSeconds(0.8f);

        yield return FadeTo(1f);
        yield return new WaitForSeconds(0.3f);

        Transform point = wave.respawnPoint != null ? wave.respawnPoint : bossMapSpawnPoint;
        TeleportPlayer(point);

        if (PlayerHealth.Instance != null) PlayerHealth.Instance.ResetHP();

        yield return new WaitForEndOfFrame();
        yield return FadeTo(0f);

        SetPlayerMovement(true);
        yield return new WaitForSeconds(0.5f);
    }

    // ==================================================================
    //  피아노 도달 & 최종 연주
    // ==================================================================

    private IEnumerator WaitForPianoApproach()
    {
        if (pianoApproachPoint == null)
        {
            Debug.LogError("[BossSequence] Piano Approach Point가 비어있습니다!");
            yield break;
        }

        while (true)
        {
            if (playerTransform != null)
            {
                float d = Vector3.Distance(playerTransform.position, pianoApproachPoint.position);
                if (d <= pianoReachDistance) break;
            }
            yield return null;
        }

        Log("피아노 도달");
    }

    private IEnumerator FinalMelodyChallenge()
    {
        if (finalMelody == null)
        {
            Debug.LogError("[BossSequence] Final Melody가 비어있습니다!");
            yield break;
        }

        PianoSession.Busy = true;
        SetPlayerMovement(false);

        if (pianoUI != null) pianoUI.SetActive(true);
        yield return null;

        if (PianoManager.Instance != null) PianoManager.Instance.WarmUpAudio();

        // 시연
        if (demonstrateFinalMelody && PianoManager.Instance != null)
        {
            if (PianoFX.Instance != null) PianoFX.Instance.Toast("잘 들어라", 1.2f);
            yield return new WaitForSeconds(0.8f);

            PianoManager.Instance.inputLocked = true;
            yield return PianoManager.Instance.PlayMelody(finalMelody, true, 1f);
            PianoManager.Instance.inputLocked = false;

            if (PianoFX.Instance != null) PianoFX.Instance.Toast("연주해라!", 1.2f);
            yield return new WaitForSeconds(0.4f);
        }

        // 입력 판정
        bool cleared = false;
        MelodyChecker checker = new MelodyChecker();
        activeChecker = checker;

        checker.OnCorrect += (idx) =>
        {
            if (PianoManager.Instance == null) return;
            PianoManager.Instance.ClearAllGuides();
            if (showGuideOnFinal)
            {
                int next = checker.ExpectedKey();
                if (next >= 0) PianoManager.Instance.SetGuide(next, true);
            }
        };

        checker.OnWrong += (idx) =>
        {
            if (PianoFX.Instance != null)
            {
                StartCoroutine(PianoFX.Instance.WrongFeedback(checker.ExpectedKey()));
            }
        };

        checker.OnComplete += () => cleared = true;

        checker.Begin(finalMelody, resetOnFinalMistake);

        if (showGuideOnFinal && PianoManager.Instance != null)
        {
            int first = checker.ExpectedKey();
            if (first >= 0) PianoManager.Instance.SetGuide(first, true);
        }

        // 틀려서 멈췄으면 다시 시작 (무한 재도전)
        while (!cleared)
        {
            if (!checker.Running && !cleared)
            {
                yield return new WaitForSeconds(0.6f);
                if (PianoFX.Instance != null) PianoFX.Instance.Toast("처음부터 다시", 1.2f);
                checker.Begin(finalMelody, resetOnFinalMistake);

                if (showGuideOnFinal && PianoManager.Instance != null)
                {
                    PianoManager.Instance.ClearAllGuides();
                    int first = checker.ExpectedKey();
                    if (first >= 0) PianoManager.Instance.SetGuide(first, true);
                }
            }
            yield return null;
        }

        checker.Stop();
        activeChecker = null;

        if (PianoManager.Instance != null) PianoManager.Instance.ClearAllGuides();

        if (PianoFX.Instance != null)
        {
            PianoFX.Instance.Flash(new Color(1f, 0.95f, 0.6f), 0.5f, 0.5f);
            yield return PianoFX.Instance.KeyRipple(0.02f, true);
        }

        yield return new WaitForSeconds(0.5f);

        if (pianoUI != null) pianoUI.SetActive(false);
        PianoSession.Busy = false;
    }

    // ==================================================================
    //  헬퍼
    // ==================================================================

    private void TeleportPlayer(Transform point)
    {
        if (playerTransform == null || point == null)
        {
            Debug.LogError("[BossSequence] 텔레포트 대상이 비어있습니다!");
            return;
        }

        Transform root = playerTransform.root;

        // CharacterController가 켜져 있으면 position 대입이 무시됩니다
        CharacterController cc = root.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        root.position = point.position;
        Physics.SyncTransforms();

        if (cc != null) cc.enabled = true;

        Log($"텔레포트: {point.name} ({point.position})");
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        // 1순위: 인스펙터에 직접 연결한 CanvasGroup
        if (fadeCanvasGroup != null)
        {
            float start = fadeCanvasGroup.alpha;
            float elapsed = 0f;
            float dur = Mathf.Max(0.05f, fadeDuration);

            fadeCanvasGroup.blocksRaycasts = (targetAlpha > 0f);

            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                fadeCanvasGroup.alpha = Mathf.Lerp(start, targetAlpha, elapsed / dur);
                yield return null;
            }
            fadeCanvasGroup.alpha = targetAlpha;
            yield break;
        }

        // 2순위: 기존 PianoScreenFader
        if (PianoScreenFader.Instance != null)
        {
            if (targetAlpha > 0f) yield return PianoScreenFader.Instance.FadeOut(fadeDuration);
            else yield return PianoScreenFader.Instance.FadeIn(fadeDuration);
            yield break;
        }

        Debug.LogWarning("[BossSequence] 페이드용 CanvasGroup도 PianoScreenFader도 없습니다. 암전 없이 진행합니다.");
    }

    private IEnumerator RunDialogue(DialogueData data)
    {
        if (data == null || DialogueManager.Instance == null) yield break;

        bool done = false;
        DialogueManager.Instance.StartDialogue(data, () => done = true);
        yield return new WaitUntil(() => done);
        yield return new WaitForSeconds(0.25f);
    }

    private void UpdateHpUI(int hp)
    {
        if (playerHpText != null) playerHpText.text = new string('♥', Mathf.Max(0, hp));
    }

    private void SetPlayerMovement(bool enable)
    {
        if (playerMovementScript != null) playerMovementScript.enabled = enable;
    }

    private void Log(string msg)
    {
        if (debugLog) Debug.Log($"[BossSequence] {msg}");
    }

    private void OnDisable()
    {
        if (activeChecker != null) activeChecker.Stop();
        PianoSession.Busy = false;

        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.OnDeath -= HandlePlayerDeath;
            PlayerHealth.Instance.OnHPChanged -= UpdateHpUI;
        }
        if (PianoManager.Instance != null) PianoManager.Instance.inputLocked = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (pianoApproachPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(pianoApproachPoint.position, pianoReachDistance);
        }
        if (bossMapSpawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(bossMapSpawnPoint.position, 0.5f);
        }
    }
}
