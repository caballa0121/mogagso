using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement; 

public enum BattlePhase { PlayerTurn, EnemyTurn, TurnEnd }

public class BattleManager : MonoBehaviour
{
    [Header("참여 캐릭터 리스트")]
    public List<BattleCharacter> playerTeam = new List<BattleCharacter>();
    public List<BattleCharacter> enemyTeam = new List<BattleCharacter>();

    [Header("시네마틱 연출 세팅")]
    public GameObject dimPanel;
    public float zoomMultiplier = 0.7f;

    [Header("합(Clash) 간격 세팅")]
    public float defenderAdvance = 1.5f; // 수비자가 자기 대열에서 앞으로 나오는 거리
    public float clashGap = 5.0f;        // 공격자가 수비자 앞까지 파고드는 거리

    [Header("광역 공격 추가 대상 배치")]
    [Tooltip("메인 타깃보다 얼마나 뒤/위에 세울지 (x = 뒤로, y = 위로)")]
    public Vector2 extraTargetOffset = new Vector2(1.6f, 0.8f);
    
    [Header("게임 오버 세팅")]
    public GameObject gameOverPanel;

    [Header("전투 승리 세팅")]
    [Tooltip("모든 웨이브를 클리어했을 때 넘어갈 씬 이름. 비워두면 씬을 전환하지 않습니다.")]
    public string victorySceneName = "";
    [Tooltip("씬 전환 전에 잠깐 띄울 승리 연출용 패널 (선택 사항, 없어도 됨)")]
    public GameObject victoryPanel;
    [Tooltip("승리 패널을 띄운 뒤 씬 전환까지 대기할 시간(초)")]
    public float victoryDelay = 2.0f;

    private Camera mainCamera;
    private float originalCamSize;
    private Vector3 originalCamPos;

    public BattlePhase currentPhase { get; private set; }
    public BattleUIController uiController { get; private set; } 
    public BattleCharacter protagonistCharacter { get; private set; }
    
    private CombatResolver combatResolver;
    private bool isAnimationPlaying = false;
    
    public int currentMeasure = 1; // 💥 현재 몇 마디인지 기억하는 카운터 추가!

    void Awake()
    {
        combatResolver = GetComponent<CombatResolver>();
        uiController = GetComponent<BattleUIController>(); 

        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            originalCamSize = mainCamera.orthographicSize;
            originalCamPos = mainCamera.transform.position;
        }
        if (dimPanel != null) dimPanel.SetActive(false); 
        if (gameOverPanel != null) gameOverPanel.SetActive(false); 
    }

    void Start() 
    { 
        // 💥 기존: ChangePhase(BattlePhase.PlayerTurn); 
        // 변경: 카메라 줌 & 캐릭터 이동 인트로 먼저 실행!
        StartCoroutine(BattleIntroSequence());
    }
    
    // 💥 새롭게 추가된 도입부(Intro) 연출
    private IEnumerator BattleIntroSequence()
    {
        isAnimationPlaying = true; // 조작 잠금

        // 💥 노드맵에서 함정을 밟았거나 지난 전투에서 체력이 닳았다면 그 체력으로 시작합니다.
        //    (BattleCharacter가 Awake에서 풀피로 만들기 때문에 Awake가 다 끝난 지금 덮어씁니다)
        PartyState.Apply(playerTeam);

        // 카메라가 위(컷씬 구도)에서 아래(전투 구도)로 슥 내려오는 연출
        if (mainCamera != null)
        {
            Vector3 introPos = originalCamPos;
            introPos.y += 5f; 
            mainCamera.transform.position = introPos;
            StartCoroutine(MoveCamera(originalCamPos, originalCamSize, 1.5f));
        }

        // WaveManager가 캐릭터들을 등장시키고 자리에 미끄러지듯 배치할 때까지 대기
        WaveManager wManager = FindAnyObjectByType<WaveManager>();
        if (wManager != null) yield return StartCoroutine(wManager.SpawnWaveAndMoveToFormation(this));
        else yield return new WaitForSeconds(1.5f); // 매니저가 없어도 에러 안 나게 대기

        // 진형 전개가 끝나면 본격적인 1마디(공격) 시작!
        StartCoroutine(StartMeasure(BattlePhase.PlayerTurn));
    }

    // 💥 새롭게 추가된 마디 알림 & 페이즈 전환 함수
    private IEnumerator StartMeasure(BattlePhase nextPhase)
    {
        isAnimationPlaying = true; // 텍스트가 떠있는 동안 조작 잠금
        if (uiController != null) uiController.HideHandUI();
        DisableAllLines();
        HideAllIntents(); // 💥 마디 텍스트가 뜨는 동안은 모든 캐릭터의 버튼(의도 아이콘)을 꺼둡니다

        string measureStr = (nextPhase == BattlePhase.PlayerTurn) ? $"{currentMeasure} 마디 (공격)" : $"{currentMeasure} 마디 (수비)";

        // 텍스트가 스르륵 떴다 사라지길 기다림
        if (uiController != null) yield return StartCoroutine(uiController.ShowMeasureText(measureStr));

        isAnimationPlaying = false; // 조작 잠금 해제
        currentPhase = nextPhase;

        // 💥 텍스트가 다 사라진 뒤에야 StartPlayerOffense/Defense가 각 캐릭터의 버튼을 다시 띄웁니다
        if (currentPhase == BattlePhase.PlayerTurn) StartPlayerOffense();
        else StartPlayerDefense();
    }

    // 💥 아군/적군 전원의 의도 아이콘(버튼)을 끕니다. 주인공도 예외 없이 포함합니다.
    private void HideAllIntents()
    {
        foreach (var p in playerTeam) if (p != null) p.HideIntent();
        foreach (var e in enemyTeam) if (e != null) e.HideIntent();
    }
    
    // 💥 웨이브 매니저가 "적 다 죽었어요!" 하면 부르는 다음 웨이브 처리 함수
    public IEnumerator HandleNextWave(WaveManager wManager)
    {
        isAnimationPlaying = true; // 조작 잠금
        if (uiController != null) uiController.HideHandUI();
        DisableAllLines();

        // 약간의 휴식 (전투 종료 후 쾌감 유지)
        yield return new WaitForSeconds(wManager.timeBetweenWaves);

        // 다음 적군들이 밖에서 미끄러져 들어오길 대기
        yield return StartCoroutine(wManager.SpawnWaveAndMoveToFormation(this));

        // 💥 더 이상 웨이브가 없으면 적이 하나도 없는 채로 새 마디를 시작하지 않고 승리 처리로 빠집니다.
        if (wManager.AllWavesCleared)
        {
            TriggerVictory();
            yield break;
        }

        // 배치가 끝나면 새로운 마디로 다시 플레이어 공격 턴 시작!
        EndOfMeasure(); // 💥 독 피해 정산 + 버프/디버프 지속 마디 감소
        currentMeasure++;
        StartCoroutine(StartMeasure(BattlePhase.PlayerTurn));
    }

    // 💥 등록된 웨이브를 전부 클리어했을 때. TriggerGameOver와 같은 패턴으로 만들었습니다.
    public void TriggerVictory()
    {
        Debug.Log("🎉 모든 웨이브 클리어! 승리!");

        isAnimationPlaying = true;
        if (victoryPanel != null) victoryPanel.SetActive(true);

        // 💥 살아남은 체력을 적어둡니다. 노드맵과 다음 전투가 이 체력을 이어받습니다.
        PartyState.Capture(playerTeam);

        StartCoroutine(LoadVictorySceneAfterDelay(victoryDelay));
    }

    private IEnumerator LoadVictorySceneAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // 💥 노드맵의 전투 노드에서 들어온 전투라면, 그 노드를 깬 것으로 하고 노드맵으로 돌아갑니다.
        if (NodeMapContext.inBattleFromMap)
        {
            string backScene = NodeMapContext.returnSceneName;
            NodeMapContext.FinishBattle(true);
            yield return StartCoroutine(ScreenFader.TransitionTo(backScene));
            yield break;
        }

        if (!string.IsNullOrEmpty(victorySceneName))
        {
            // 💥 어두워진 뒤에 씬을 넘깁니다. (ScreenFader가 없으면 즉시 전환)
            yield return StartCoroutine(ScreenFader.TransitionTo(victorySceneName));
        }
    }

    void Update()
    {
        if (Mouse.current == null || Keyboard.current == null || isAnimationPlaying) return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (currentPhase == BattlePhase.PlayerTurn)
            {
                // 💥 턴이 넘어갈 때 바로 ChangePhase를 부르지 않고 마디 텍스트를 띄웁니다!
                StartCoroutine(StartMeasure(BattlePhase.EnemyTurn)); 
            }
            else if (currentPhase == BattlePhase.EnemyTurn)
            {
                StartCoroutine(ExecuteRemainingEnemyAttacks()); 
            }
        }
    }

    private void ChangePhase(BattlePhase newPhase)
    {
        currentPhase = newPhase;
        switch (currentPhase)
        {
            case BattlePhase.PlayerTurn: StartPlayerOffense(); break;
            case BattlePhase.EnemyTurn: StartPlayerDefense(); break;
            case BattlePhase.TurnEnd: ChangePhase(BattlePhase.PlayerTurn); break;
        }
    }

    // ==========================================
    // ⚔️ 페이즈 1: 플레이어 공격 턴
    // ==========================================
    private void StartPlayerOffense()
    {
        if (combatResolver != null) combatResolver.ClearDefenseRecords();
        if (uiController != null) uiController.HideHandUI();

        // 💥 아무도 버튼을 누르기 전에, 이번 마디의 버프 카드를 전부 먼저 처리합니다.
        //    (적은 이번 마디 수비 역할 → Defense쪽 버프, 아군은 공격 역할 → Attack쪽 버프)
        HashSet<BattleCharacter> buffedEnemies = ResolveAiBuffsAtMeasureStart(enemyTeam, playerTeam, CardType.Defense);
        HashSet<BattleCharacter> buffedAllies = ResolveAiBuffsAtMeasureStart(playerTeam, enemyTeam, CardType.Attack);

        foreach (var enemy in enemyTeam)
        {
            if (enemy == null || !enemy.IsAlive) continue;
            if (buffedEnemies.Contains(enemy)) continue; // 💥 이미 버프로 행동을 마쳤습니다
            enemy.hasActedThisTurn = false;

            enemy.preparedCard = enemy.DrawRandomCard(CardType.Defense, CardCategory.Action);
            enemy.preparedTarget = null; 
            
            if (enemy.preparedCard != null)
            {
                enemy.ShowIntent(enemy.preparedCard, null); 
                enemy.SetGlow(false);
            }
            else { enemy.hasActedThisTurn = true; enemy.HideIntent(); }
        }

        foreach (var player in playerTeam)
        {
            if (player == null || !player.IsAlive) continue;
            if (buffedAllies.Contains(player)) continue; // 💥 이미 버프로 행동을 마쳤습니다
            player.hasActedThisTurn = false;

            if (player.role == CharacterRole.Protagonist)
            {
                protagonistCharacter = player;
                player.preparedCard = null;
                player.preparedTarget = null;
                if (uiController != null) uiController.DrawProtagonistHand(player, CardType.Attack);
                player.ShowIntentForType(CardType.Attack, false); // 💥 주인공 아이콘도 항상 표시
                continue;
            }

            player.preparedCard = player.DrawRandomCard(CardType.Attack, CardCategory.Action);
            player.preparedTarget = PickCardTarget(player, player.preparedCard, enemyTeam);

            if (player.preparedCard != null && player.preparedTarget != null)
            {
                player.ShowIntent(player.preparedCard, player.preparedTarget);
                player.SetGlow(false);
            }
            else { player.hasActedThisTurn = true; player.HideIntent(); }
        }
    }

    // ==========================================
    // 🛡️ 페이즈 2: 플레이어 수비 턴
    // ==========================================
    private void StartPlayerDefense()
    {
        DisableAllLines();

        // 💥 아무도 버튼을 누르기 전에, 이번 마디의 버프 카드를 전부 먼저 처리합니다.
        //    (아군은 이번 마디 수비 역할 → Defense쪽 버프, 적은 공격 역할 → Attack쪽 버프)
        HashSet<BattleCharacter> buffedAllies = ResolveAiBuffsAtMeasureStart(playerTeam, enemyTeam, CardType.Defense);
        HashSet<BattleCharacter> buffedEnemies = ResolveAiBuffsAtMeasureStart(enemyTeam, playerTeam, CardType.Attack);

        foreach (var player in playerTeam)
        {
            if (player == null || !player.IsAlive) continue;
            if (buffedAllies.Contains(player)) continue; // 💥 이미 버프로 행동을 마쳤습니다
            player.hasActedThisTurn = false;

            if (player.role == CharacterRole.Protagonist)
            {
                protagonistCharacter = player;
                player.preparedCard = null;
                player.preparedTarget = null;
                if (uiController != null) uiController.DrawProtagonistHand(player, CardType.Defense);
                player.ShowIntentForType(CardType.Defense, false); // 💥 주인공 아이콘도 항상 표시
                continue;
            }

            player.preparedCard = player.DrawRandomCard(CardType.Defense, CardCategory.Action);
            player.preparedTarget = null;

            if (player.preparedCard != null)
            {
                player.ShowIntent(player.preparedCard, null);
                player.SetGlow(false);
            }
            else { player.hasActedThisTurn = true; player.HideIntent(); }
        }

        foreach (var enemy in enemyTeam)
        {
            if (enemy == null || !enemy.IsAlive) continue;
            if (buffedEnemies.Contains(enemy)) continue; // 💥 이미 버프로 행동을 마쳤습니다
            enemy.hasActedThisTurn = false;

            enemy.preparedCard = enemy.DrawRandomCard(CardType.Attack, CardCategory.Action);
            enemy.preparedTarget = PickCardTarget(enemy, enemy.preparedCard, playerTeam);

            if (enemy.preparedCard != null && enemy.preparedTarget != null)
            {
                enemy.ShowIntent(enemy.preparedCard, enemy.preparedTarget); 
                enemy.SetGlow(false);
            }
            else { enemy.hasActedThisTurn = true; enemy.HideIntent(); }
        }
    }

    // ==========================================
    // 💡 호버(Hover) 로직
    // ==========================================
    public void OnCharacterHoverEnter(BattleCharacter hoveredChar) 
    { 
        if (isAnimationPlaying) return;
        DisableAllLines(); 

        if (uiController != null && uiController.isProtagonistTargeting && protagonistCharacter != null && hoveredChar.IsAlive && hoveredChar.role == CharacterRole.Enemy)
        {
            protagonistCharacter.ShowIntent(uiController.selectedProtagonistCard, hoveredChar);
            protagonistCharacter.SetGlow(false);
            DrawLine(protagonistCharacter, hoveredChar);
            protagonistCharacter.preparedTarget = hoveredChar; 
        }

        if (uiController != null) uiController.ProcessHoverEnter(hoveredChar); 

        // 💥 버프 카드는 준비되는 즉시 실행되어 hasActedThisTurn이 true입니다 — 그래도
        //    호버하면 "누구에게 썼는지" 화살표는 계속 보여야 하므로 예외로 둡니다.
        bool isBuffIntent = hoveredChar.preparedCard != null && hoveredChar.preparedCard.category == CardCategory.Buff;
        if ((!hoveredChar.hasActedThisTurn || isBuffIntent) && hoveredChar.preparedTarget != null && hoveredChar.preparedTarget.IsAlive && hoveredChar.role != CharacterRole.Protagonist)
        {
            DrawLine(hoveredChar, hoveredChar.preparedTarget);
        }

        foreach(var p in playerTeam)
            if (p != null && p.IsAlive && !p.hasActedThisTurn && p.preparedTarget == hoveredChar && p.role != CharacterRole.Protagonist)
                DrawLine(p, hoveredChar);
                
        foreach(var e in enemyTeam)
            if (e != null && e.IsAlive && !e.hasActedThisTurn && e.preparedTarget == hoveredChar)
                DrawLine(e, hoveredChar);
    }

    public void OnCharacterHoverExit(BattleCharacter hoveredChar) 
    { 
        if (isAnimationPlaying) return;
        if (uiController != null) uiController.ProcessHoverExit(hoveredChar); 
        DisableAllLines(); 
        
        if (uiController != null && uiController.isProtagonistTargeting && protagonistCharacter != null)
        {
            protagonistCharacter.preparedTarget = null;
            CardType hType = (currentPhase == BattlePhase.PlayerTurn) ? CardType.Attack : CardType.Defense;
            protagonistCharacter.ShowIntentForType(hType, protagonistCharacter.hasActedThisTurn);
        }
    }

    // 포물선 화살표 연출 유지!
    private void DrawLine(BattleCharacter from, BattleCharacter to)
    {
        if (from != null && from.targetLine != null && to != null)
        {
            int segments = 20; 
            from.targetLine.positionCount = segments + 1; 
            
            Vector3 startPos = from.intentCanvas != null ? from.intentCanvas.transform.position : from.transform.position;
            Vector3 endPos = to.intentCanvas != null ? to.intentCanvas.transform.position : to.transform.position;
            
            Vector3 controlPos = (startPos + endPos) / 2f + Vector3.up * 1.5f; 

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 curvePos = Mathf.Pow(1 - t, 2) * startPos + 
                                   2 * (1 - t) * t * controlPos + 
                                   Mathf.Pow(t, 2) * endPos;
                                   
                from.targetLine.SetPosition(i, curvePos);
            }
            from.targetLine.enabled = true;
        }
    }
    private void DisableAllLines()
    {
        foreach(var p in playerTeam) if (p != null && p.targetLine != null) p.targetLine.enabled = false;
        foreach(var e in enemyTeam) if (e != null && e.targetLine != null) e.targetLine.enabled = false;
    }

    // ==========================================
    // 💡 클릭(Click): 전투 실행
    // ==========================================
    // 💥 자신 / 아군 전체를 대상으로 하는 카드는 따로 클릭할 대상이 없으므로,
    //    손패에서 고르는 즉시 실행됩니다. (BattleUIController.SelectCard 에서 호출)
    public void ExecuteProtagonistSelfCard(SkillCard card)
    {
        if (protagonistCharacter == null || card == null || isAnimationPlaying) return;

        protagonistCharacter.preparedCard = card;
        protagonistCharacter.preparedTarget = protagonistCharacter;
        protagonistCharacter.hasActedThisTurn = true;

        StartCoroutine(ExecuteActionWithAnimation(protagonistCharacter, protagonistCharacter, card, null));
    }

    public void OnCharacterClicked(BattleCharacter clickedChar)
    {
        if (isAnimationPlaying) return; 
        DisableAllLines(); 

        if (currentPhase == BattlePhase.PlayerTurn) 
        {
            if (uiController != null && uiController.isProtagonistTargeting && protagonistCharacter != null && clickedChar.role == CharacterRole.Enemy)
            {
                protagonistCharacter.preparedTarget = clickedChar;
                protagonistCharacter.preparedCard = uiController.selectedProtagonistCard;
                
                uiController.ConsumeSelectedCard(); 
                StartCoroutine(ExecuteActionWithAnimation(protagonistCharacter, clickedChar, protagonistCharacter.preparedCard, clickedChar.preparedCard));
                return;
            }

            // 💥 '단일 아군' 대상 카드를 고른 채로 아군을 클릭하면 그 아군에게 씁니다.
            if (uiController != null && uiController.isProtagonistTargeting && protagonistCharacter != null
                && uiController.selectedProtagonistCard != null
                && uiController.selectedProtagonistCard.targetType == SkillCard.TargetType.Ally
                && clickedChar.role == CharacterRole.Ally)
            {
                protagonistCharacter.preparedTarget = clickedChar;
                protagonistCharacter.preparedCard = uiController.selectedProtagonistCard;
                protagonistCharacter.hasActedThisTurn = true;

                uiController.ConsumeSelectedCard();
                StartCoroutine(ExecuteActionWithAnimation(protagonistCharacter, clickedChar, protagonistCharacter.preparedCard, null));
                return;
            }

            if (clickedChar.role == CharacterRole.Ally && !clickedChar.hasActedThisTurn)
            {
                // 💥 노리던 대상이 이미 쓰러졌으면 공격이 성립하지 않습니다.
                //    (전에는 여기서 preparedTarget이 null이라 터졌고, 그 바람에 행동 처리가
                //     중간에 끊겨 해당 캐릭터의 차례가 넘어가지 않았습니다)
                if (clickedChar.preparedTarget == null || !clickedChar.preparedTarget.IsAlive)
                {
                    SkipTurn(clickedChar, "노리던 대상이 이미 쓰러져");
                    return;
                }

                StartCoroutine(ExecuteActionWithAnimation(clickedChar, clickedChar.preparedTarget, clickedChar.preparedCard, clickedChar.preparedTarget.preparedCard));
            }
        }
        else if (currentPhase == BattlePhase.EnemyTurn)
        {
            if (uiController != null && uiController.isProtagonistTargeting && protagonistCharacter != null && clickedChar.role == CharacterRole.Enemy)
            {
                // 💥 이미 공격을 마쳤거나 공격 카드가 없는 적 = 막을 공격이 없음 -> '일방방어'로 알리고 종료
                if (clickedChar.hasActedThisTurn || clickedChar.preparedCard == null)
                {
                    uiController.CancelCardSelection();
                    StartCoroutine(ShowOneSidedDefenseNotice());
                    return;
                }

                clickedChar.preparedTarget = protagonistCharacter; 
                protagonistCharacter.preparedCard = uiController.selectedProtagonistCard; 
                // 💥 수비턴도 공격턴처럼 1회만 개입: 이 플래그가 없으면 주인공은 attacker가 아니라서
                //    hasActedThisTurn이 계속 false로 남고, 연출이 끝날 때 빈 손패 패널이 다시 켜집니다.
                protagonistCharacter.hasActedThisTurn = true;
                
                uiController.ConsumeSelectedCard();
                StartCoroutine(ExecuteActionWithAnimation(clickedChar, protagonistCharacter, clickedChar.preparedCard, protagonistCharacter.preparedCard));
                return;
            }

            // 💥 수비 카드를 고른 채로 '아군'을 클릭하면, 그 아군에게 오는 공격을
            //    주인공이 몸으로 대신 받아냅니다. (막아줄 공격이 없으면 일방방어 안내)
            if (uiController != null && uiController.isProtagonistTargeting && protagonistCharacter != null
                && clickedChar != protagonistCharacter && clickedChar.role == CharacterRole.Ally)
            {
                BattleCharacter incoming = enemyTeam.Find(e => e != null && e.IsAlive && !e.hasActedThisTurn && e.preparedTarget == clickedChar);

                if (incoming == null || incoming.preparedCard == null)
                {
                    uiController.CancelCardSelection();
                    StartCoroutine(ShowOneSidedDefenseNotice());
                    return;
                }

                Debug.Log($"🛡️ [대신 막기] {clickedChar.characterName}을(를) 노린 {incoming.characterName}의 공격을 주인공이 받아냅니다!");

                incoming.preparedTarget = protagonistCharacter;                       // 공격 대상을 주인공으로 전환
                protagonistCharacter.preparedCard = uiController.selectedProtagonistCard;
                protagonistCharacter.hasActedThisTurn = true;

                uiController.ConsumeSelectedCard();
                StartCoroutine(ExecuteActionWithAnimation(incoming, protagonistCharacter, incoming.preparedCard, protagonistCharacter.preparedCard));
                return;
            }

            if (clickedChar.role == CharacterRole.Ally || clickedChar.role == CharacterRole.Protagonist)
            {
                BattleCharacter attackingEnemy = enemyTeam.Find(e => e != null && e.IsAlive && !e.hasActedThisTurn && e.preparedTarget == clickedChar);
                if (attackingEnemy != null)
                {
                    StartCoroutine(ExecuteActionWithAnimation(attackingEnemy, clickedChar, attackingEnemy.preparedCard, clickedChar.preparedCard));
                }
            }
        }
    }

    private IEnumerator ExecuteActionWithAnimation(BattleCharacter attacker, BattleCharacter defender, SkillCard atkCard, SkillCard defCard = null)
    {
        isAnimationPlaying = true;

        // 💥 자신/아군을 노리는 카드는 '합(공격 vs 방어)'이 아니라 '지원' 행동입니다.
        //    상대의 방어 카드를 끌어오지 않고, 시전자만 움직이는 별도 경로로 처리합니다.
        if (atkCard != null && atkCard.IsSameTeamCard)
        {
            yield return StartCoroutine(ExecuteSupportAction(attacker, defender, atkCard));
            yield break;
        }

        if (defCard == null && defender != null) defCard = defender.preparedCard;

        // 💥 원호방어 : defCard가 없다 = 막을 방법이 없는 '일방공격'입니다.
        //    이때만, 이 대상을 콕 집어 지키고 있는 아군이 있으면 그쪽이 대신 맞습니다. (1회 소모)
        if (defCard == null && defender != null)
        {
            BattleCharacter cover = FindCoverFor(defender);
            if (cover != null)
            {
                Debug.Log($"🛡️ [원호방어] {cover.characterName}이(가) {defender.characterName}에게 향하는 일방공격을 대신 막아섭니다!");
                cover.ConsumeCoverFor(defender);
                defender = cover;
                defCard = (combatResolver != null) ? combatResolver.GetDefenseCard(cover) : null;
            }
        }

        if (uiController != null) 
        {
            uiController.HideHandUI();
            uiController.ShowClashPanel(attacker, atkCard, defender, defCard);
        }

        foreach(var p in playerTeam) 
        {
            if (p != null && p != attacker && p != defender) p.HideIntent();
        }
        foreach(var e in enemyTeam) 
        {
            if (e != null && e != attacker && e != defender) e.HideIntent();
        }

        if (dimPanel != null) dimPanel.SetActive(true);
        attacker.PopUpToCinematicLayer();
        if (defender != null && defender.IsAlive) defender.PopUpToCinematicLayer();

        // 💥 수비자는 자기 대열에서 앞으로 조금만 나오고, 공격자가 그 앞까지 크게 파고듭니다.
        Vector3 defenderHome = (defender != null) ? defender.transform.position : attacker.transform.position;

        // 수비자 진영 기준 '상대 쪽(앞)' 방향 : 적군이면 -x, 아군이면 +x
        float defForward = (defender != null && defender.role == CharacterRole.Enemy) ? -1f : 1f;

        Vector3 defStagePos = defenderHome + Vector3.right * (defForward * defenderAdvance);
        Vector3 atkStagePos = defStagePos  + Vector3.right * (defForward * clashGap);
        defStagePos.z = 0f;
        atkStagePos.z = 0f;

        // 합이 벌어지는 지점으로 카메라를 옮겨서 줌인 (중앙 고정이면 대열 끝 전투가 화면 밖으로 나갑니다)
        if (mainCamera != null && defender != null)
        {
            float clashMidX = (atkStagePos.x + defStagePos.x) * 0.5f;
            Vector3 camClashPos = new Vector3(clashMidX, originalCamPos.y, originalCamPos.z);
            StartCoroutine(MoveCamera(camClashPos, originalCamSize * zoomMultiplier, 0.1f));
        }

        // 💥 광역 공격 : 추가 대상들을 메인 타깃보다 조금 뒤쪽(공격자 반대편)으로 불러옵니다.
        List<BattleCharacter> extraTargets = new List<BattleCharacter>();
        List<Coroutine> extraMoves = new List<Coroutine>();
        if (combatResolver != null && defender != null)
        {
            int extraCount = combatResolver.GetExtraTargetCount(atkCard);
            extraTargets = PickExtraTargets(defender, extraCount);

            for (int i = 0; i < extraTargets.Count; i++)
            {
                extraTargets[i].PopUpToCinematicLayer();
                Vector3 backPos = defStagePos
                                + Vector3.right * (-defForward * extraTargetOffset.x * (i + 1))
                                + Vector3.up * (extraTargetOffset.y * (i + 1));
                backPos.z = 0f;
                extraMoves.Add(StartCoroutine(extraTargets[i].MoveToClashPosition(backPos, 0.2f)));
            }
        }

        Coroutine moveAtk = StartCoroutine(attacker.MoveToClashPosition(atkStagePos, 0.2f));
        Coroutine moveDef = null;
        if (defender != null && defender.IsAlive) moveDef = StartCoroutine(defender.MoveToClashPosition(defStagePos, 0.2f));

        yield return moveAtk;
        if (moveDef != null) yield return moveDef;
        foreach (var m in extraMoves) yield return m;

        Coroutine atkAnim = StartCoroutine(attacker.PlayActionAnimation("Attack"));
        Coroutine defAnim = null;
        if (defender != null && defender.IsAlive) defAnim = StartCoroutine(defender.PlayActionAnimation("Defense", 0.3f));

        // 💥 광역 공격의 추가 대상들도 메인 수비자와 함께 방어 자세를 취합니다.
        List<Coroutine> extraDefAnims = new List<Coroutine>();
        foreach (var extra in extraTargets)
        {
            if (extra != null && extra.IsAlive) extraDefAnims.Add(StartCoroutine(extra.PlayActionAnimation("Defense", 0.3f)));
        }

        yield return new WaitForSeconds(0.5f);

        if (combatResolver != null) combatResolver.ResolveAction(attacker, defender, atkCard, defCard);

        // 💥 광역 공격 : 추가 대상에게도 같은 공격 카드로 피해를 넣습니다.
        //    (추가 대상은 방어 카드 없이 맨몸으로 맞습니다)
        if (combatResolver != null)
        {
            foreach (var extra in extraTargets)
            {
                if (extra == null || !extra.IsAlive) continue;
                Debug.Log($"💥 [광역 공격] {attacker.characterName} → {extra.characterName}");
                combatResolver.ApplyDamage(attacker, extra, atkCard, null, false);
            }
        }

        if (uiController != null)
        {
            uiController.RefreshHpUI(attacker, defender);
        }

        attacker.hasActedThisTurn = true; 

        if (defender != null && defender.preparedCard != null)
        {
            defender.preparedCard = null;
        }

        yield return atkAnim;
        if (defAnim != null) yield return defAnim;
        foreach (var m in extraDefAnims) yield return m;

        Coroutine backAtk = StartCoroutine(attacker.ReturnToStartPosition(0.2f));
        Coroutine backDef = null;
        if (defender != null && defender.IsAlive) backDef = StartCoroutine(defender.ReturnToStartPosition(0.2f));

        yield return backAtk;
        if (backDef != null) yield return backDef;

        if (mainCamera != null) StartCoroutine(MoveCamera(originalCamPos, originalCamSize, 0.15f));

        foreach (var extra in extraTargets)
        {
            if (extra == null) continue;
            if (extra.IsAlive) StartCoroutine(extra.ReturnToStartPosition(0.2f));
            extra.ResetToDefaultLayer();
        }

        attacker.ResetToDefaultLayer();
        if (defender != null) defender.ResetToDefaultLayer();
        if (dimPanel != null) dimPanel.SetActive(false);

        RefreshPostActionUI();
        isAnimationPlaying = false;
    }

    // 💥 시전자/자신을 노리는 '지원' 카드 전용 실행 경로.
    //    합(Clash)과 달리 상대의 방어 카드를 쓰지 않고, 대상 캐릭터는 무대로 불러오지 않습니다.
    //    시전자만 제자리에서 살짝 나왔다 들어가는 동작으로 연출합니다.
    private IEnumerator ExecuteSupportAction(BattleCharacter caster, BattleCharacter target, SkillCard card)
    {
        if (target == null) target = caster; // 안전장치 (정상 경로에서는 항상 채워져 들어옵니다)

        if (uiController != null)
        {
            uiController.HideHandUI();
            uiController.ShowSupportPanel(caster, card, target);
        }

        foreach (var p in playerTeam) if (p != null && p != caster) p.HideIntent();
        foreach (var e in enemyTeam) if (e != null && e != caster) e.HideIntent();

        if (dimPanel != null) dimPanel.SetActive(true);
        caster.PopUpToCinematicLayer();

        if (mainCamera != null)
        {
            Vector3 camPos = new Vector3(caster.transform.position.x, originalCamPos.y, originalCamPos.z);
            StartCoroutine(MoveCamera(camPos, originalCamSize * zoomMultiplier, 0.1f));
        }

        // 💥 상대를 부르지 않고, 시전자만 제자리에서 앞으로 살짝 나왔다 들어갑니다.
        yield return StartCoroutine(caster.PlayActionAnimation("Attack"));

        if (combatResolver != null)
        {
            bool ignoreDefense = false;
            int extra = 0;
            combatResolver.ApplyCardEffects(caster, target, card, ref ignoreDefense, ref extra);
        }

        if (uiController != null) uiController.RefreshHpUI(caster, target);

        caster.hasActedThisTurn = true;

        if (mainCamera != null) StartCoroutine(MoveCamera(originalCamPos, originalCamSize, 0.15f));

        caster.ResetToDefaultLayer();
        if (dimPanel != null) dimPanel.SetActive(false);

        RefreshPostActionUI();
        isAnimationPlaying = false;
    }

    // 💥 전투 연출(합 / 지원 행동) 종료 후 공통으로 필요한 UI 갱신.
    //    ExecuteActionWithAnimation 과 ExecuteSupportAction 이 함께 씁니다.
    private void RefreshPostActionUI()
    {
        // 💥 preparedCard가 null이어도(예: 방어에 성공해서 카드가 소모된 경우) 무조건 숨기지 않습니다.
        //    ShowIntent 내부에서 card가 없으면 이전에 냈던 카드(lastIntentCard)로 자연스럽게
        //    아이콘을 복원하고, 정말 아무것도 없을 때만 스스로 HideIntent를 호출합니다.
        foreach(var p in playerTeam) if (p != null && p.IsAlive && p.role != CharacterRole.Protagonist)
        {
            p.ShowIntent(p.preparedCard, p.preparedTarget);
            p.SetGlow(p.hasActedThisTurn);
        }
        foreach(var e in enemyTeam) if (e != null && e.IsAlive)
        {
            e.ShowIntent(e.preparedCard, e.preparedTarget);
            e.SetGlow(e.hasActedThisTurn);
        }
        if (protagonistCharacter != null && protagonistCharacter.IsAlive && (uiController == null || !uiController.isProtagonistTargeting))
        {
            // 💥 주인공도 아군처럼 아이콘을 계속 띄우고, 행동을 마쳤으면 빛나는 아이콘으로 바꿉니다.
            CardType pType = (currentPhase == BattlePhase.PlayerTurn) ? CardType.Attack : CardType.Defense;
            protagonistCharacter.ShowIntentForType(pType, protagonistCharacter.hasActedThisTurn);
        }

        if (protagonistCharacter != null && protagonistCharacter.IsAlive && !protagonistCharacter.hasActedThisTurn)
        {
            if (uiController != null) uiController.ShowHandUI();
        }

        if (uiController != null) uiController.HideTooltip();
    }

    private IEnumerator ExecuteRemainingEnemyAttacks()
    {
        isAnimationPlaying = true;
        foreach(var p in playerTeam) if (p != null) p.HideIntent();
        foreach(var e in enemyTeam) if (e != null) e.HideIntent();

        foreach (var enemy in enemyTeam)
        {
            if (enemy == null || !enemy.IsAlive || enemy.hasActedThisTurn) continue;

            // 💥 노리던 대상이 쓰러졌으면 차례를 넘깁니다.
            //    그냥 건너뛰기만 하면 hasActedThisTurn이 false로 남아 다음 마디에도 계속 걸립니다.
            if (enemy.preparedTarget == null || !enemy.preparedTarget.IsAlive)
            {
                SkipTurn(enemy, "노리던 대상이 쓰러져");
                continue;
            }

            yield return StartCoroutine(ExecuteActionWithAnimation(enemy, enemy.preparedTarget, enemy.preparedCard, enemy.preparedTarget.preparedCard));
        }
        
        // 💥 모든 적의 행동이 끝나면 마디(Measure)를 올리고 다음 턴 알림을 띄웁니다!
        EndOfMeasure(); // 💥 독 피해 정산 + 버프/디버프 지속 마디 감소
        currentMeasure++;
        StartCoroutine(StartMeasure(BattlePhase.PlayerTurn));
    }

    // 💥 막을 공격이 없는데 방어 카드를 낸 경우 -> 안내만 띄우고 아무 일도 일어나지 않습니다.
    //    (카드는 소모되지 않고, 주인공의 개입 기회도 그대로 남습니다)
    private IEnumerator ShowOneSidedDefenseNotice()
    {
        isAnimationPlaying = true;
        DisableAllLines();

        if (uiController != null) yield return StartCoroutine(uiController.ShowMeasureText("일방방어입니다"));
        else yield return new WaitForSeconds(0.5f);

        isAnimationPlaying = false;
    }

    private IEnumerator MoveCamera(Vector3 targetPos, float targetSize, float duration)
    {
        if (mainCamera == null) yield break;
        Vector3 startPos = mainCamera.transform.position;
        float startSize = mainCamera.orthographicSize;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            mainCamera.transform.position = Vector3.Lerp(startPos, targetPos, elapsed / duration);
            mainCamera.orthographicSize = Mathf.Lerp(startSize, targetSize, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        mainCamera.transform.position = targetPos;
        mainCamera.orthographicSize = targetSize;
    }

    // 💥 카드의 targetType 을 보고 실제 대상을 고릅니다.
    //    Enemy/AllEnemies 카드는 기존처럼 상대편에서, Self/Ally/AllAllies 카드는 같은 편에서 고릅니다.
    //    (AllAllies 도 연출/표시용으로 대표 한 명이 필요하므로 같은 방식으로 고릅니다 —
    //     실제 효과 적용 범위는 각 CardEffectData의 scope 가 따로 결정합니다)
    // 💥 마디가 시작될 때, Buff 카테고리 카드를 가진 적을 전부 찾아 즉시 사용합니다.
    //    플레이어가 손패/캐릭터를 클릭하기 전에 끝나므로, 버프가 아무도 반응하지 않는
    //    '헛곳'에 쓰이는 일 없이 항상 유효한 아군(없으면 자기 자신)에게 확실히 적용됩니다.
    //    phaseCardType : 이번 마디에 적이 준비할 카드 타입 (StartPlayerOffense→Defense, StartPlayerDefense→Attack)
    // 💥 반환값 : 이번 마디에 버프를 사용해서 '행동을 이미 끝낸' 적 목록.
    //    호출하는 쪽(StartPlayerOffense/StartPlayerDefense)은 이 목록에 든 적을
    //    일반 행동 카드 준비 루프에서 건너뛰어야 합니다 — 안 그러면 방금 쓴 버프 카드가
    //    빠진 덱에서 또 카드를 뽑으려다 실패해서(방어 카드가 그 버프 카드 하나뿐인 경우 등)
    //    아이콘이 사라지거나 빈 이미지로 남는 문제가 생깁니다.
    // 💥 team(아군 또는 적군)에 속한 AI 캐릭터들이 버프 카드를 가지고 있으면 마디 시작 즉시 사용합니다.
    //    주인공은 손패로 직접 조작하므로 여기서 건너뜁니다.
    private HashSet<BattleCharacter> ResolveAiBuffsAtMeasureStart(List<BattleCharacter> team, List<BattleCharacter> foeTeam, CardType phaseCardType)
    {
        HashSet<BattleCharacter> actedViaBuff = new HashSet<BattleCharacter>();
        if (combatResolver == null) return actedViaBuff;

        foreach (var caster in team)
        {
            if (caster == null || !caster.IsAlive) continue;
            if (caster.role == CharacterRole.Protagonist) continue; // 주인공은 손패로 직접 사용합니다

            SkillCard buffCard = caster.DrawRandomCard(phaseCardType, CardCategory.Buff);
            if (buffCard == null) continue; // 버프 카드가 없으면 그냥 넘어갑니다

            BattleCharacter target = PickCardTarget(caster, buffCard, foeTeam);
            if (target == null || !target.IsAlive) continue;

            Debug.Log($"🌀 [{caster.characterName}] 버프 '{buffCard.cardName}' 자동 사용 → {target.characterName}");

            bool ignoreDefense = false;
            int extra = 0;
            combatResolver.ApplyCardEffects(caster, target, buffCard, ref ignoreDefense, ref extra);

            // 💥 버프 사용을 '이번 마디의 행동'으로 확정합니다. 밝은(빛나는) 아이콘으로 표시하고,
            //    호버하면 이 카드와 target을 향한 화살표가 뜹니다 (일반 공격 완료 상태와 동일한 방식).
            caster.preparedCard = buffCard;
            caster.preparedTarget = target;
            caster.hasActedThisTurn = true;
            caster.ShowIntent(buffCard, target);
            caster.SetGlow(true);

            actedViaBuff.Add(caster);
        }
        return actedViaBuff;
    }

    private BattleCharacter PickCardTarget(BattleCharacter caster, SkillCard card, List<BattleCharacter> foeTeam)
    {
        if (card == null || !card.IsSameTeamCard) return GetRandomTarget(foeTeam);

        if (card.targetType == SkillCard.TargetType.Self) return caster;

        List<BattleCharacter> ownTeam = (caster.role == CharacterRole.Enemy) ? enemyTeam : playerTeam;
        List<BattleCharacter> mates = ownTeam.FindAll(c => c != null && c.IsAlive && c != caster);
        if (mates.Count == 0) return caster; // 혼자 남았으면 자기 자신에게

        return mates[Random.Range(0, mates.Count)];
    }

    // 💥 한 마디가 끝날 때 : 독 피해를 넣고 버프/디버프의 남은 마디를 1 줄입니다.
    private void EndOfMeasure()
    {
        foreach (var p in playerTeam) if (p != null && p.IsAlive) p.TickEffectsAtMeasureEnd();
        foreach (var e in enemyTeam)  if (e != null && e.IsAlive) e.TickEffectsAtMeasureEnd();
    }

    // 💥 원호방어 : 노려진 아군 대신 맞아주는 캐릭터를 찾습니다.
    //    같은 편에서 CoverAlly 효과를 들고 있는 다른 살아있는 캐릭터가 있으면 그쪽으로 공격을 돌립니다.
    // 💥 defender를 콕 집어 보호(protectTarget)하고 있는 아군을 찾습니다.
    //    "아무나 막아주는" 게 아니라, 그 카드로 지정된 특정 대상만 해당됩니다.
    public BattleCharacter FindCoverFor(BattleCharacter defender)
    {
        if (defender == null || !defender.IsAlive) return null;

        List<BattleCharacter> team = (defender.role == CharacterRole.Enemy) ? enemyTeam : playerTeam;
        foreach (var c in team)
        {
            if (c == null || c == defender || !c.IsAlive) continue;
            foreach (var eff in c.activeEffects)
            {
                if (eff.type == EffectType.CoverAlly && eff.protectTarget == defender) return c;
            }
        }
        return null;
    }

    // 💥 광역 공격의 추가 대상을 고릅니다. (메인 타겟 제외, 같은 편에서)
    public List<BattleCharacter> PickExtraTargets(BattleCharacter mainTarget, int count)
    {
        List<BattleCharacter> picked = new List<BattleCharacter>();
        if (mainTarget == null || count <= 0) return picked;

        List<BattleCharacter> team = (mainTarget.role == CharacterRole.Enemy) ? enemyTeam : playerTeam;
        foreach (var c in team)
        {
            if (picked.Count >= count) break;
            if (c == null || c == mainTarget || !c.IsAlive) continue;
            picked.Add(c);
        }
        return picked;
    }

    // 💥 도발치(aggroWeight)가 높을수록 잘 맞도록 가중 추첨합니다.
    private BattleCharacter GetRandomTarget(List<BattleCharacter> team)
    {
        List<BattleCharacter> aliveMembers = team.FindAll(c => c != null && c.IsAlive);
        if (aliveMembers.Count == 0) return null;

        int total = 0;
        foreach (var c in aliveMembers) total += Mathf.Max(1, c.aggroWeight);

        int roll = Random.Range(0, total);
        foreach (var c in aliveMembers)
        {
            roll -= Mathf.Max(1, c.aggroWeight);
            if (roll < 0) return c;
        }
        return aliveMembers[aliveMembers.Count - 1];
    }

    public void OnCharacterDied(BattleCharacter deadChar)
    {
        Debug.Log($"[{deadChar.characterName}]이(가) 쓰러졌습니다!");

        LoseTurnIfTargeting(playerTeam, deadChar);
        LoseTurnIfTargeting(enemyTeam, deadChar);
    }

    // 💥 노리던 대상이 쓰러진 캐릭터는 이번 마디 행동을 잃습니다.
    //    대상만 null로 비워두면 공격 실행부에서 터지거나, 행동하지 않은 채로 남아
    //    차례가 영영 넘어가지 않습니다.
    private void LoseTurnIfTargeting(List<BattleCharacter> team, BattleCharacter deadChar)
    {
        if (team == null) return;

        foreach (var c in team)
        {
            if (c == null || c.preparedTarget != deadChar) continue;

            c.preparedTarget = null;

            // 주인공은 직접 다른 대상을 고를 수 있으므로 차례를 뺏지 않습니다.
            if (c.role == CharacterRole.Protagonist) continue;

            SkipTurn(c, "노리던 대상이 쓰러져");
        }
    }

    // 💥 한 캐릭터의 이번 차례를 넘깁니다. (이미 행동했다면 아무것도 하지 않습니다)
    public void SkipTurn(BattleCharacter c, string reason)
    {
        if (c == null || c.hasActedThisTurn) return;

        c.hasActedThisTurn = true;
        c.preparedTarget = null;
        c.SetGlow(false);
        c.HideIntent();

        Debug.Log($"⏭️ [{c.characterName}] {reason} 이번 차례를 넘깁니다.");
    }

    public void TriggerGameOver()
    {
        Debug.Log("💀 주인공 사망! 게임 오버!");
        
        isAnimationPlaying = true; 
        if (gameOverPanel != null) gameOverPanel.SetActive(true);

        StartCoroutine(RestartWaveAfterDelay(2.5f));
    }

    private IEnumerator RestartWaveAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // 💥 노드맵에서 들어온 전투에서 졌다면, 전투를 다시 하는 게 아니라
        //    노드맵으로 돌아가서 시작 지점부터 다시 깨게 합니다.
        if (NodeMapContext.inBattleFromMap)
        {
            string backScene = NodeMapContext.returnSceneName;
            NodeMapContext.FinishBattle(false);
            yield return StartCoroutine(ScreenFader.TransitionTo(backScene));
            yield break;
        }

        // 💥 챕터에서 바로 들어온 전투는 예전처럼 같은 전투를 다시 합니다.
        //    이때 체력까지 물려받으면 영영 못 이기는 상태가 되므로 풀피로 되돌립니다.
        PartyState.ResetToFull();

        string currentSceneName = SceneManager.GetActiveScene().name;
        yield return StartCoroutine(ScreenFader.TransitionTo(currentSceneName));
    }

    public void CheckPlayerPhaseEnd() { }
    public void CheckEnemyPhaseEnd() { }
}
