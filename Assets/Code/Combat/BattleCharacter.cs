using System.Collections; // 코루틴을 위해 추가
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum CharacterRole { Protagonist, Ally, Enemy }

// 💥 캐릭터에게 실제로 걸려 있는 효과 1건
[System.Serializable]
public class ActiveEffect
{
    public EffectType type;
    public int value;
    public int remainingMeasures; // 남은 마디 수
    public float multiplier; // 합공(JointAttack) 전용 : 배율에 더해지는 증가분 (0.2 → +20%)
    public bool oneShot;          // 회피 / 공격 반사 / 원호방어처럼 1회 발동 후 사라지는 효과
    public EffectCondition condition; // 대기 효과일 때 확인할 조건
    public BattleCharacter source;
    public BattleCharacter protectTarget; // 원호방어(CoverAlly) 전용 : 누구에게 오는 일방공격을 대신 막을지
}

[System.Serializable]
public class CardDeckSetting
{
    public SkillCard cardPrefab; 
    public int maxCount;         
}

public class BattleCharacter : MonoBehaviour
{
    [Header("기본 정보")]
    public string characterName;
    public CharacterRole role;

    [Header("전투 스탯")]
    public int maxHp = 100;     
    public int currentHp;       
    public int aggroWeight = 10; 

    [Header("덱 설정")]
    public List<CardDeckSetting> deckSettings; 
    private List<SkillCard> drawPile = new List<SkillCard>(); 

    public bool IsAlive => currentHp > 0;
    
    // 💥 중복 죽음 방지용 플래그
    [HideInInspector] public bool isDead = false; 
    [Header("심리스 등장 설정")]
    public bool startInCenter = false; // 체크하면 화면 중앙에서 시작, 안 하면 화면 밖(증원군)에서 시작

    [Header("캐릭터 애니메이션 (3장)")]
    public Sprite idleSprite;    
    public Sprite attackSprite;  
    public Sprite defenseSprite; 
    
    private SpriteRenderer spriteRenderer; 
    private Vector3 originalPosition;
    private Vector3 startPosition; // 💥 맵에서의 진짜 원래 자리를 기억할 변수      

    [Header("이 캐릭터 전용 효과음")]
    [Tooltip("이 캐릭터가 공격 동작을 할 때 나는 소리. 캐릭터마다 다르게 넣을 수 있습니다.")]
    public SoundCue attackSfx = new SoundCue();

    [Tooltip("이 캐릭터가 방어 동작을 할 때 나는 소리. 비워두면 소리 없음.")]
    public SoundCue defenseSfx = new SoundCue();

    [Tooltip("이 캐릭터의 의도 버튼을 눌렀을 때 나는 소리.\n" +
             "비워두면 전투 공용 클릭음(BattleSfx의 Button Click)이 대신 납니다.")]
    public SoundCue buttonClickSfx = new SoundCue();

    [Header("시각 효과 & UI")]
    public Renderer modelRenderer;
    private Color originalColor;

    [Header("행동 의도 (버튼 UI 캔버스)")]
    public GameObject intentCanvas;
    public LineRenderer targetLine; 

    [Header("의도(상태) UI 이미지")]
    public Image intentIcon;            

    [Header("아이콘 소스 (4종)")]
    public Sprite attackNormalSprite;   
    public Sprite attackGlowSprite;     
    public Sprite defenseNormalSprite;  
    public Sprite defenseGlowSprite;    

    private bool isIntentConfirmed = false; 

    [HideInInspector] public bool hasActedThisTurn = false;
    [HideInInspector] public SkillCard preparedCard;
    [HideInInspector] public BattleCharacter preparedTarget;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // 💥 시작할 때 자신의 절대 위치를 기억합니다.
        startPosition = transform.position; 
        originalPosition = startPosition;

        if (modelRenderer != null) originalColor = modelRenderer.material.color;
        
        HideIntent(); 
        currentHp = maxHp;
        isDead = false;
        InitializeDeck();
    }

    private void InitializeDeck()
    {
        drawPile.Clear();
        foreach (var setting in deckSettings)
            for (int i = 0; i < setting.maxCount; i++)
                drawPile.Add(setting.cardPrefab);
    }

    public void ShowIntent(SkillCard card, BattleCharacter target)
    {
        // 💥 보여줄 카드가 진짜 하나도 없으면(card도 없고 예전에 기억해둔 카드도 없으면)
        //    캔버스를 켜지 않습니다. 그냥 켜버리면 스프라이트가 비어있는 '디폴트 이미지'만 보입니다.
        if (card == null && lastIntentCard == null)
        {
            HideIntent();
            return;
        }

        if (intentCanvas != null)
        {
            intentCanvas.SetActive(true);
            isIntentConfirmed = false;
            UpdateIntentIcon(card);
        }

        if (targetLine != null && target != null)
        {
            targetLine.positionCount = 2; 
            
            Vector3 startPoint = intentCanvas != null ? intentCanvas.transform.position : transform.position;
            Vector3 endPoint = target.intentCanvas != null ? target.intentCanvas.transform.position : target.transform.position;
            
            targetLine.SetPosition(0, startPoint);
            targetLine.SetPosition(1, endPoint);
            
            targetLine.enabled = false; 
        }
    }

    public void HideIntent()
    {
        if (intentCanvas != null) intentCanvas.SetActive(false);
        if (targetLine != null) 
        {
            targetLine.enabled = false;
            targetLine.positionCount = 0; 
        }
        isIntentConfirmed = false;
    }

    // 💥 마지막으로 표시한 카드를 기억합니다. (수비턴에는 preparedCard가 비워지기 때문에
    //    이게 없으면 행동 후 아이콘을 '빛나는 상태'로 갱신할 수 없습니다)
    private SkillCard lastIntentCard;

    public void UpdateIntentIcon(SkillCard card)
    {
        if (card != null) lastIntentCard = card;
        SkillCard shown = (card != null) ? card : lastIntentCard;
        if (shown == null || intentIcon == null) return;

        if (shown.cardType == CardType.Attack)
            intentIcon.sprite = isIntentConfirmed ? attackGlowSprite : attackNormalSprite;
        else if (shown.cardType == CardType.Defense)
            intentIcon.sprite = isIntentConfirmed ? defenseGlowSprite : defenseNormalSprite;
            
        intentIcon.gameObject.SetActive(true);
    }

    // 💥 카드가 아직 정해지지 않았을 때(주인공의 턴 시작 등) 마디 종류만으로 아이콘을 띄웁니다.
    //    glow = true 면 '행동 완료(빛나는)' 아이콘이 됩니다.
    public void ShowIntentForType(CardType type, bool glow)
    {
        if (intentCanvas != null) intentCanvas.SetActive(true);
        isIntentConfirmed = glow;

        if (intentIcon == null) return;

        if (type == CardType.Attack)
            intentIcon.sprite = glow ? attackGlowSprite : attackNormalSprite;
        else
            intentIcon.sprite = glow ? defenseGlowSprite : defenseNormalSprite;

        intentIcon.gameObject.SetActive(true);
    }

    public void SetGlow(bool glow)
    {
        isIntentConfirmed = glow; 
        SkillCard shown = (preparedCard != null) ? preparedCard : lastIntentCard;
        if (shown != null) UpdateIntentIcon(shown);
    }

    public void OnPointerEnterIntentButton()
    {
        BattleSfx.Play(BattleSfxType.ButtonHover);

        BattleManager manager = FindAnyObjectByType<BattleManager>();
        if (manager != null) manager.OnCharacterHoverEnter(this);
    }

    public void OnPointerExitIntentButton()
    {
        BattleManager manager = FindAnyObjectByType<BattleManager>();
        if (manager != null) manager.OnCharacterHoverExit(this);
    }

    public void OnIntentButtonClicked()
    {
        // 1. 죽었으면 클릭 불가
        if (!IsAlive) return;

        // 💥 2. 아군이나 주인공은 행동을 마쳤거나 쓸 카드가 없으면 클릭 불가!
        // (적군은 카드가 비어있어도 주인공이 일방공격으로 때릴 수 있어야 하므로 예외 처리)
        if (role != CharacterRole.Enemy && (hasActedThisTurn || preparedCard == null)) return;

        // 💥 눌리지 않는 클릭까지 소리가 나면 헷갈리므로, 위 조건을 다 통과한 뒤에 냅니다.
        // 💥 이 캐릭터에 정해둔 클릭음이 있으면 그걸 쓰고, 없으면 전투 공용 클릭음을 씁니다.
        //    (둘 다 울려서 소리가 겹치지 않게 하나만 고릅니다)
        if (buttonClickSfx != null && buttonClickSfx.HasClip) SfxKit.Play(buttonClickSfx);
        else BattleSfx.Play(BattleSfxType.ButtonClick);

        if (role != CharacterRole.Enemy)
        {
            SetGlow(true);
        }

        BattleManager manager = FindAnyObjectByType<BattleManager>();
        if (manager != null) manager.OnCharacterClicked(this);
    }

    public void ChangePose(string pose)
    {
        if (spriteRenderer == null) return;

        if (pose == "Idle" && idleSprite != null) spriteRenderer.sprite = idleSprite;
        else if (pose == "Attack" && attackSprite != null) spriteRenderer.sprite = attackSprite;
        else if (pose == "Defense" && defenseSprite != null) spriteRenderer.sprite = defenseSprite;
    }

    public IEnumerator PlayActionAnimation(string pose, float moveDistance = 1.0f)
    {
        ChangePose(pose); 

        // 💥 캐릭터마다 정해둔 전용 효과음. 비워두면 조용히 넘어갑니다.
        if (pose == "Attack") SfxKit.Play(attackSfx);
        else if (pose == "Defense") SfxKit.Play(defenseSfx);
        // 💥 아군은 +x, 적군은 -x가 '상대 쪽(앞)'. 방어 모션일 때는 반대로 뒤로 물러납니다.
        float dir = (role == CharacterRole.Enemy) ? -1f : 1f;
        if (pose == "Defense") dir = -dir;
        Vector3 targetPos = originalPosition + Vector3.right * (dir * moveDistance);

        float elapsedTime = 0f;
        while (elapsedTime < 0.1f)
        {
            transform.position = Vector3.Lerp(originalPosition, targetPos, elapsedTime / 0.1f);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPos;

        yield return new WaitForSeconds(0.8f); 

        elapsedTime = 0f;
        while (elapsedTime < 0.15f)
        {
            transform.position = Vector3.Lerp(targetPos, originalPosition, elapsedTime / 0.15f);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        transform.position = originalPosition;
        ChangePose("Idle");
    }

    // 💥 requiredType(공격/수비)은 항상 정확히 일치해야 합니다.
    //    category를 지정하면 그 안에서 '행동 카드'인지 '버프 카드'인지로 한 번 더 거릅니다.
    //    category를 안 넘기면(기본값 null) 행동/버프 구분 없이 그 타입 전체에서 뽑습니다.
    // 💥 exclude : 이미 손에 들고 있는 카드 등, 이번엔 다시 뽑히면 안 되는 카드 목록.
    //    (손패 여러 장을 한 번에 뽑을 때, 도중에 덱이 바닥나 리셔플이 일어나도
    //    같은 카드가 같은 손패에 중복으로 나오는 걸 막기 위한 용도입니다)
    public SkillCard DrawRandomCard(CardType requiredType, CardCategory? category = null, List<SkillCard> exclude = null)
    {
        System.Predicate<SkillCard> matches = card =>
            card.cardType == requiredType && (category == null || card.category == category.Value)
            && (exclude == null || !exclude.Contains(card));

        List<SkillCard> availableCards = drawPile.FindAll(matches);
        if (availableCards.Count == 0)
        {
            // 💥 애초에 덱 설정 자체에 이 조건에 맞는 카드가 하나도 없으면 재구성해도 소용없으니
            //    그냥 포기합니다. 여기서 무조건 InitializeDeck을 부르면, 예를 들어 Buff 카드가
            //    하나도 없는 캐릭터에게 "혹시 있나" 확인만 해도 매번 덱 전체가 새로 섞여서
            //    이미 뽑은 카드가 다시 돌아오는(뽑기 없이 뽑는) 문제가 생깁니다.
            bool deckHasSuchCard = false;
            foreach (var setting in deckSettings)
            {
                if (setting.cardPrefab != null && matches(setting.cardPrefab)) { deckHasSuchCard = true; break; }
            }
            if (!deckHasSuchCard) return null;

            InitializeDeck();
            availableCards = drawPile.FindAll(matches);
            if (availableCards.Count == 0) return null;
        }

        int randomIndex = Random.Range(0, availableCards.Count);
        SkillCard drawnCard = availableCards[randomIndex];
        drawPile.Remove(drawnCard);

        return drawnCard;
    }

    // 💥 피해를 입고 죽음을 판단하는 부분 수정
    public void TakeDamage(int damage)
    {
        if (isDead) return; // 이미 죽었다면 무시

        wasHitThisMeasure = true; // 💥 자세잡기 등 "맞았는지"를 보는 효과의 판정 근거

        currentHp -= damage;
        Debug.Log($"[{characterName}] 님이 {damage}의 피해를 입었습니다.");

        if (currentHp <= 0) 
        { 
            currentHp = 0; 
            StartCoroutine(DieRoutine()); // 죽음 연출 코루틴 실행
        }
    }

    // 💥 캐릭터 죽음 연출 및 처리 코루틴 (새로 추가됨!)
    private IEnumerator DieRoutine()
    {
        isDead = true;
        BattleSfx.Play(BattleSfxType.Die);
        HideIntent(); // 1. 즉시 의도(버튼 UI 및 라인) 숨기기

        // 2. BattleManager에게 알려서 현재 턴 대기열(순서)에서 삭제하기
        BattleManager bManager = FindAnyObjectByType<BattleManager>();
        if (bManager != null) bManager.OnCharacterDied(this);

        // 3. 페이드 아웃 연출 (1초 동안 스르륵 투명해짐)
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            float fadeDuration = 1.0f;
            float elapsedTime = 0f;

            while (elapsedTime < fadeDuration)
            {
                c.a = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
                spriteRenderer.color = c;
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            c.a = 0f;
            spriteRenderer.color = c;
        }

        // 4. 역할(Role)에 따른 사후 처리
        if (role == CharacterRole.Protagonist)
        {
            // 주인공 사망 -> 게임 오버 처리
            if (bManager != null) bManager.TriggerGameOver();
        }
        else if (role == CharacterRole.Enemy)
        {
            // 적군 사망 -> 웨이브 매니저에게 알리고 화면에서 완전 삭제
            WaveManager wManager = FindAnyObjectByType<WaveManager>();
            if (wManager != null) wManager.EnemyDied(gameObject);
            
            Destroy(gameObject); 
        }
        else if (role == CharacterRole.Ally)
        {
            // 아군 사망 -> 파괴하지 않고 비활성화만 (웨이브 초기화 시 부활을 위해)
            gameObject.SetActive(false);
        }
    }
    
    public void PopUpToCinematicLayer()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingLayerName = "Cinematic";
            
            if (intentCanvas != null)
            {
                Canvas charCanvas = intentCanvas.GetComponent<Canvas>();
                if (charCanvas != null) charCanvas.sortingLayerName = "Cinematic";
            }
        }
    }

    public void ResetToDefaultLayer()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingLayerName = "Default";
            
            if (intentCanvas != null)
            {
                Canvas charCanvas = intentCanvas.GetComponent<Canvas>();
                if (charCanvas != null) charCanvas.sortingLayerName = "Default";
            }
        }
    }

    public IEnumerator MoveToClashPosition(Vector3 targetPos, float duration = 0.2f)
    {
        Vector3 start = transform.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(start, targetPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPos;
        
        // 중앙에 도착한 위치를 '새로운 기준점'으로 삼아서 
        // PlayActionAnimation(앞으로 살짝 나가는 모션)이 중앙에서 실행되게 만듭니다!
        originalPosition = targetPos; 
    }

    // 💥 합이 끝나고 원래 자리로 휙 돌아가는 함수
    public IEnumerator ReturnToStartPosition(float duration = 0.2f)
    {
        Vector3 start = transform.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(start, startPosition, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = startPosition;
        
        // 기준점도 다시 원래 자리로 복구합니다.
        originalPosition = startPosition; 
    }

    // 💥 지정된 자리로 '스르륵' 미끄러지듯 이동(Slide)하는 연출 코루틴
    public System.Collections.IEnumerator MoveToFormation(Vector3 targetPos, float duration = 0.8f)
    {
        // 이동하는 내내 걷기가 아니라 '대기(Idle)' 또는 '전투 준비' 자세 유지
        ChangePose("Idle");
        
        // 이동 중에는 타겟 방향을 바라보도록 셋팅
        if (spriteRenderer != null) spriteRenderer.flipX = (targetPos.x > transform.position.x);

        Vector3 startPos = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Lerp를 사용하여 처음엔 훅! 이동하고 끝에선 스무스하게 멈추는 미끄러짐 연출
            // (Ease-Out 효과를 위해 Mathf.Pow 등으로 수학적 곡선을 줄 수도 있습니다)
            float t = elapsed / duration;
            t = 1f - Mathf.Pow(1f - t, 3f); // 스무스하게 감속하는 마법의 공식 (Cubic Ease Out)
            
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;
        originalPosition = targetPos; // 도착한 곳을 진짜 내 자리로 저장!
        startPosition = targetPos;    // 💥 복귀 기준점도 갱신. 없으면 소환 시점의 화면 밖 좌표로 되돌아갑니다.

        // 제자리 도착 시 전투 방향(아군은 오른쪽, 적군은 왼쪽)으로 휙! 돌아보기
        if (spriteRenderer != null) spriteRenderer.flipX = (role != CharacterRole.Enemy); // 원본 스프라이트가 왼쪽을 보고 있어 아군을 뒤집습니다
    }

    // ==========================================
    // 💥 지속 효과(버프/디버프) 시스템
    // ==========================================

    [Header("현재 걸려 있는 효과 (읽기 전용)")]
    public List<ActiveEffect> activeEffects = new List<ActiveEffect>();

    // 💥 조건부(자세잡기) 효과가 판정을 기다리는 대기열
    public List<ActiveEffect> pendingEffects = new List<ActiveEffect>();

    // 💥 이번 마디에 피해를 받았는지. 마디가 끝날 때 조건 판정에 쓰고 초기화합니다.
    [HideInInspector] public bool wasHitThisMeasure = false;

    // 같은 종류 효과의 수치를 전부 합산합니다.
    public int GetEffectValue(EffectType type)
    {
        int sum = 0;
        foreach (var e in activeEffects) if (e.type == type) sum += e.value;
        return sum;
    }

    public bool HasEffect(EffectType type)
    {
        foreach (var e in activeEffects) if (e.type == type) return true;
        return false;
    }

    // 합공 배율 중 가장 큰 값을 돌려줍니다. (없으면 1배)
    // 💥 기본 배율 1에서 시작해서, 걸려 있는 합공 효과의 multiplier 값을 전부 더합니다.
    //    예: 0.2짜리 하나 → 1.2배. 0.2 두 개가 겹치면 → 1.4배 (더해짐, 최댓값만 쓰지 않음)
    public float GetJointMultiplier()
    {
        float bonus = 0f;
        foreach (var e in activeEffects)
            if (e.type == EffectType.JointAttack) bonus += e.multiplier;
        return 1f + bonus;
    }

    public int AttackBonus  => GetEffectValue(EffectType.AttackUp)  - GetEffectValue(EffectType.AttackDown);
    public int DefenseBonus => GetEffectValue(EffectType.DefenseUp) - GetEffectValue(EffectType.DefenseDown);

    public void AddEffect(EffectType type, int value, int duration, float multiplier, BattleCharacter source, BattleCharacter protectTarget = null)
    {
        // 💥 회피 / 공격 반사는 지속 마디와 상관없이 '발동될 때까지' 남아있다 1회성으로 사라집니다.
        //    원호방어는 다릅니다 — 정해진 마디 동안만 유효해야 하므로 여기 포함하지 않습니다.
        //    (발동 시 소멸은 ConsumeCoverFor가 별도로 처리합니다. 안 쓰이면 duration만큼 지나 자연 소멸)
        bool oneShot = (type == EffectType.Evade || type == EffectType.ReflectAll);

        var eff = new ActiveEffect
        {
            type = type,
            value = value,
            // duration 0 은 '이번 마디 동안만' 으로 취급합니다.
            remainingMeasures = Mathf.Max(1, duration),
            // 💥 합공(JointAttack)에서는 이 값이 '배율에 더해지는 증가분'입니다. (0.2 → +20%)
            //    예전처럼 0일 때 1.2로 몰래 치환하지 않습니다 — 적은 값 그대로 반영됩니다.
            multiplier = multiplier,
            oneShot = oneShot,
            source = source,
            protectTarget = protectTarget
        };
        activeEffects.Add(eff);
    }


    // 💥 조건부 효과를 대기열에 넣습니다. 마디가 끝날 때 조건을 확인해 실제로 걸립니다.
    public void AddPendingEffect(EffectType type, int value, int duration, float multiplier, EffectCondition condition, BattleCharacter source, BattleCharacter protectTarget = null)
    {
        pendingEffects.Add(new ActiveEffect
        {
            type = type,
            value = value,
            remainingMeasures = Mathf.Max(1, duration),
            multiplier = multiplier,
            oneShot = (type == EffectType.Evade || type == EffectType.ReflectAll), // 원호방어는 duration으로 관리
            condition = condition,
            source = source,
            protectTarget = protectTarget
        });
    }
    // 회피처럼 한 번 쓰면 사라지는 효과를 소모합니다.
    public void ConsumeOneShot(EffectType type)
    {
        for (int i = 0; i < activeEffects.Count; i++)
        {
            if (activeEffects[i].type == type)
            {
                activeEffects.RemoveAt(i);
                return;
            }
        }
    }

    // 💥 반격처럼 '한 번 터지면 끝'인 효과를 종류째로 전부 지웁니다.
    //    같은 종류가 여러 장 겹쳐 걸려 있으면 값이 합쳐져 한 번에 나가므로,
    //    발동한 뒤에는 남김없이 사라져야 합니다.
    public void ConsumeAllOfType(EffectType type)
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            if (activeEffects[i].type == type) activeEffects.RemoveAt(i);
        }
    }

    // 💥 원호방어는 캐릭터 하나가 여러 아군을 각각 지정해 보호 중일 수 있으므로,
    //    실제로 이번에 발동한 '그 대상을 지키던 효과' 하나만 정확히 소모합니다.
    public void ConsumeCoverFor(BattleCharacter protectedAlly)
    {
        for (int i = 0; i < activeEffects.Count; i++)
        {
            if (activeEffects[i].type == EffectType.CoverAlly && activeEffects[i].protectTarget == protectedAlly)
            {
                activeEffects.RemoveAt(i);
                return;
            }
        }
    }

    // 💥 마디가 끝날 때 호출됩니다.
    //    순서가 중요합니다 : 독 피해 → 지속 감소 → 조건부(자세잡기) 판정 → 피격 기록 초기화
    //    조건 판정을 지속 감소보다 뒤에 두어야, 이번에 새로 걸린 버프가 곧바로 깎이지 않습니다.
    public void TickEffectsAtMeasureEnd()
    {
        if (!IsAlive) return;

        // 1) 독 피해
        int poison = GetEffectValue(EffectType.Poison);
        if (poison > 0)
        {
            Debug.Log($"☠️ [{characterName}] 독 피해 {poison}");
            TakeDamage(poison);
        }

        // 2) 지속 시간 감소 및 만료 제거
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            // 1회성 효과는 마디가 지나도 소모될 때까지 남겨둡니다.
            if (activeEffects[i].oneShot) continue;

            activeEffects[i].remainingMeasures--;
            if (activeEffects[i].remainingMeasures <= 0) activeEffects.RemoveAt(i);
        }

        // 3) 조건부(자세잡기) 효과 판정
        ResolvePendingEffects();

        // 4) 다음 마디를 위해 피격 기록 초기화
        wasHitThisMeasure = false;
    }

    // 💥 대기 중인 조건부 효과들을 이번 마디의 피격 여부로 판정합니다.
    private void ResolvePendingEffects()
    {
        if (pendingEffects.Count == 0) return;

        foreach (var p in pendingEffects)
        {
            bool pass = (p.condition == EffectCondition.IfNotHit && !wasHitThisMeasure)
                     || (p.condition == EffectCondition.IfHit    &&  wasHitThisMeasure)
                     || (p.condition == EffectCondition.Always);

            if (!pass)
            {
                Debug.Log($"🚫 [{characterName}] 자세 무너짐 — {GetEffectLabel(p.type)} 무효");
                continue;
            }

            p.condition = EffectCondition.Always; // 판정이 끝났으므로 일반 효과로 승격
            activeEffects.Add(p);
            Debug.Log($"🧘 [{characterName}] 자세잡기 성공 — {GetEffectLabel(p.type)} {p.value} / {p.remainingMeasures}마디");
        }

        pendingEffects.Clear();
    }

    // 툴팁에 보여줄 짧은 상태 요약 (효과가 없으면 빈 문자열)
    public string GetStatusText()
    {
        bool nothing = (activeEffects == null || activeEffects.Count == 0)
                    && (pendingEffects == null || pendingEffects.Count == 0);
        if (nothing) return "";

        List<string> parts = new List<string>();
        foreach (var e in activeEffects)
        {
            if (!IsVisibleStatusEffect(e.type)) continue; // 반사데미지 등 트리거형 효과는 체력 밑에 안 보여줍니다
            string label = GetEffectLabel(e.type);
            if (string.IsNullOrEmpty(label)) continue;

            parts.Add($"{label}{(e.value != 0 ? " " + e.value : "")}({e.remainingMeasures})");
        }
        // 아직 판정 전인 자세잡기류는 물음표를 붙여 구분합니다.
        foreach (var p in pendingEffects)
        {
            if (!IsVisibleStatusEffect(p.type)) continue;
            string plabel = GetEffectLabel(p.type);
            if (!string.IsNullOrEmpty(plabel)) parts.Add($"{plabel}?");
        }

        return parts.Count == 0 ? "" : string.Join(" ", parts);
    }

    // 💥 체력 밑 상태 표시는 '지속 스탯 효과'만 보여줍니다 (출혈/독/공격력·방어력 증감).
    //    반사데미지·회피·공격반사·원호방어·합공·광역공격·아군유도처럼 트리거/즉발성 메커니즘은
    //    상시 표시할 지속 상태가 아니므로 여기서 제외합니다.
    private static bool IsVisibleStatusEffect(EffectType type)
    {
        switch (type)
        {
            case EffectType.Poison:
            case EffectType.Bleed:
            case EffectType.AttackUp:
            case EffectType.AttackDown:
            case EffectType.DefenseUp:
            case EffectType.DefenseDown:
                return true;
            default:
                return false;
        }
    }

    public static string GetEffectLabel(EffectType type)
    {
        switch (type)
        {
            case EffectType.Poison:        return "독";
            case EffectType.Bleed:         return "출혈";
            case EffectType.AttackUp:      return "공↑";
            case EffectType.AttackDown:    return "공↓";
            case EffectType.DefenseUp:     return "방↑";
            case EffectType.DefenseDown:   return "방↓";
            case EffectType.Thorns:        return "반사";
            case EffectType.ReflectAll:    return "공격반사";
            case EffectType.Evade:         return "회피";
            case EffectType.CoverAlly:     return "원호";
            case EffectType.JointAttack:   return "합공";
            case EffectType.MultiTarget:   return "광역";
            case EffectType.RedirectAllyTarget: return "유도";
            default: return "";
        }
    }
}
