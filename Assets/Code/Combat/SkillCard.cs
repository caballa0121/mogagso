using System.Collections.Generic;
using UnityEngine;

// 카드의 종류 : 공격 마디에 쓰는지 / 수비 마디에 쓰는지 (기존과 동일, 절대 순서 바꾸지 마세요)
public enum CardType
{
    Attack,     // 공격 카드
    Defense,    // 방어 카드
    Hybrid      // 공방 일체
}

// 💥 CardType과 별개의 축 : 같은 공격 카드끼리도, 같은 수비 카드끼리도
//    '행동(상대에게 직접 영향)'인지 '버프(아군을 지원)'인지로 한 번 더 구분합니다.
//    예: CardType=Attack + Category=Action → 평범한 공격 카드
//        CardType=Attack + Category=Buff   → 공격 마디에 낼 수 있는 아군 지원 카드 (합공 등)
//        CardType=Defense + Category=Buff  → 수비 마디에 낼 수 있는 아군 지원 카드
public enum CardCategory
{
    Action = 0, // 평범한 공격/방어 카드
    Buff   = 1  // 아군(자신 포함) 지원 카드
}

// 카드가 발동할 부가 효과의 종류
// ⚠️ 이미 저장된 카드 에셋은 이 값을 '숫자'로 기억합니다.
//    0~5번의 순서는 절대 바꾸지 마세요. 새 효과는 항상 맨 뒤에 추가합니다.
public enum EffectType
{
    None            = 0,
    Heal            = 1,  // 체력 회복
    AddAggro        = 2,  // 도발치 증감 (value 에 음수를 넣으면 감소)
    Poison          = 3,  // 독 : 마디가 끝날 때마다 피해
    AttackDown      = 4,  // 공격력 감소
    IgnoreDefense   = 5,  // 방어력 무시

    AttackUp        = 6,  // 공격력 증가
    DefenseUp       = 7,  // 방어력 증가
    DefenseDown     = 8,  // 방어력 감소
    Bleed           = 9,  // 출혈 : 공격 행동을 할 때마다 자신이 피해
    Thorns          = 10, // 반사 데미지 / 반격 : 맞을 때 공격자에게 고정 피해
    ReflectAll      = 11, // 공격 반사 : 받을 피해를 무효화하고 그대로 공격자에게 (1회)
    Evade           = 12, // 회피 : 공격을 완전 무효화 (1회)
    MultiTarget     = 13, // 광역 공격 : value 만큼 추가 대상을 함께 공격
    JointAttack     = 14, // 합공 : 공격력에 배율 적용
    CoverAlly           = 15, // 원호방어 : 카드의 대상(scope=Self로 자신이 받고, target이 지킬 아군)에게
                               // 향하는 일방공격 하나를 대신 막아줍니다. duration(마디) 동안만 유효하고,
                               // 그 안에 발동하면 즉시 소멸, 발동 없이 마디가 다 지나면 자연 소멸합니다.
    RedirectAllyTarget  = 16, // 아군 유도 : 아군 몇 명의 공격 대상을 내가 노린 상대로 바꿈
    Damage              = 17  // 즉시 피해 : 공격/방어 계산을 거치지 않고 곧바로 체력을 깎음 (Heal의 반대)
}


// 💥 효과가 '언제' 적용될지를 정하는 조건
//    Always 가 아니면 효과는 곧바로 걸리지 않고 '대기' 상태로 들어갔다가,
//    마디가 끝날 때 조건을 확인해서 통과한 것만 실제로 걸립니다.
public enum EffectCondition
{
    Always   = 0, // 조건 없이 바로 적용
    IfNotHit = 1, // 이번 마디에 피해를 한 번도 받지 않았을 때만 (자세잡기 성공)
    IfHit    = 2  // 이번 마디에 피해를 받았을 때만 (자세잡기 실패 페널티)
}
// 효과를 누구에게 걸 것인지
public enum EffectScope
{
    Self        = 0, // 카드를 쓴 본인
    Target      = 1, // 카드가 노린 상대
    AllAllies   = 2, // 카드를 쓴 본인과 같은 편 전체
    AllEnemies  = 3  // 카드를 쓴 본인의 반대편 전체
}

// 인스펙터에서 여러 개의 효과를 자유롭게 조립할 수 있도록 돕는 데이터 블록
[System.Serializable]
public struct CardEffectData
{
    [Tooltip("어떤 효과인지")]
    public EffectType effectType;

    [Tooltip("효과를 받을 대상. 공격력/방어력 증감을 광역으로 걸려면 AllAllies / AllEnemies 를 고르세요.")]
    public EffectScope scope;

    [Tooltip("수치. 증감량 / 피해량 / 광역 공격의 추가 대상 수 등으로 쓰입니다. 도발치 감소처럼 음수도 가능합니다.")]
    public int value;

    [Tooltip("몇 마디 동안 지속되는지. 0이면 즉발(1회성)입니다. 회피·공격 반사는 지속과 무관하게 1회 발동 후 사라집니다.")]
    public int duration;

    [Tooltip("조건. IfNotHit / IfHit 를 고르면 이번 마디가 끝날 때 피격 여부를 확인해서 적용합니다. (자세잡기)")]
    public EffectCondition condition;

    [Tooltip("합공(JointAttack) 전용. 공격력 배율에 '더해지는' 증가분입니다. 예: 0.2 → 공격력 +20%(1.2배). 여러 개가 겹치면 전부 합산됩니다.")]
    public float multiplier;
}

[CreateAssetMenu(fileName = "New Skill Card", menuName = "TurnBattle/Skill Card")]
public class SkillCard : ScriptableObject
{
    public string cardName;
    
    // 툴팁 UI에 표시될 정보들
    [Header("툴팁 UI용 정보")]
    public Sprite cardImage;                  
    [TextArea] public string cardDescription; // 카드 설정이나 설명
    [TextArea] public string cardEffect;      // 툴팁에 표시될 짧은 효과 텍스트 (예: "[적중 시] 독 2 부여")

    [Header("카드 기본 정보")]
    public CardType cardType;

    [Tooltip("같은 CardType 안에서 '평범한 행동'인지 '아군 지원(버프)'인지 구분합니다.")]
    public CardCategory category = CardCategory.Action;


    [Header("스탯 설정")]
    public int attackPower;  
    public int defensePower; 
    
    // '아군 전체'를 포함하는 타겟팅 범위
    public enum TargetType 
    { 
        Enemy,          // 단일 적군
        Self,           // 자신만
        Ally,           // 단일 아군
        AllAllies,      // 아군 전체 (광역 버프/회복용)
        AllEnemies      // 적군 전체 (광역 공격/디버프용)
    }
    public TargetType targetType = TargetType.Enemy;

    // 💥 자신 / 단일 아군 / 아군 전체를 노리는 카드인지. (Enemy, AllEnemies 는 false)
    //    true 인 카드는 상대의 방어 카드를 쓰지 않고, 시전자만 움직이는 '지원' 행동으로 처리됩니다.
    public bool IsSameTeamCard =>
        targetType == TargetType.Self || targetType == TargetType.Ally || targetType == TargetType.AllAllies;

    // 부가 효과 조립 공간
    [Header("부가 효과 설정")]
    // 리스트로 만들어 두었기 때문에, 하나의 카드에 '회복'과 '도발 증가'를 동시에 넣을 수도 있습니다.
    public List<CardEffectData> additionalEffects = new List<CardEffectData>();
}
