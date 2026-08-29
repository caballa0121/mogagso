using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BattleUIController : MonoBehaviour
{

    [Header("마디(페이즈) 알림 UI")]
    public TextMeshProUGUI measureText; // 화면 중앙에 띄울 텍스트 (알파값 0으로 시작)
    [Header("주인공 UI 세팅")]
    public GameObject protagonistHandUI; 
    public TextMeshProUGUI[] handCardTexts;         
    public Image[] handCardImages; 

    [Header("툴팁 UI - 왼쪽 (나/선택된 캐릭)")]
    public GameObject tooltipPanel;
    public TextMeshProUGUI leftCardText;
    public Image leftCardImage;
    public Image leftHpCircleImage; 
    // 💥 [추가됨] 왼쪽 체력바 가운데에 들어갈 숫자 텍스트
    public TextMeshProUGUI leftHpText; 

    [Header("툴팁 UI - 오른쪽 (상대방)")]
    public TextMeshProUGUI rightCardText;
    public Image rightCardImage;
    public Image rightHpCircleImage;
    // 💥 [추가됨] 오른쪽 체력바 가운데에 들어갈 숫자 텍스트
    public TextMeshProUGUI rightHpText;

    public List<SkillCard> protagonistDrawnCards = new List<SkillCard>();
    public SkillCard selectedProtagonistCard { get; private set; }
    public bool isProtagonistTargeting { get; private set; }
    
    private int currentSelectedCardIndex = -1;
    // 💥 클래시 패널이 좌/우에 실제로 올린 캐릭터 (체력 갱신이 같은 배치를 따르도록)
    private BattleCharacter panelLeftChar;
    private BattleCharacter panelRightChar;
    private BattleManager manager;

    void Awake()
    {
        manager = GetComponent<BattleManager>();
        if (tooltipPanel != null) tooltipPanel.SetActive(false);

        // 💥 인트로(웨이브 배치) 도중에는 아직 첫 마디가 시작되지 않았으므로,
        //    손패 패널과 마디 텍스트를 처음부터 꺼둡니다. (StartMeasure가 진행 시점에 맞게 다시 켭니다)
        if (protagonistHandUI != null) protagonistHandUI.SetActive(false);
        if (measureText != null) measureText.gameObject.SetActive(false);
    }

    public void DrawProtagonistHand(BattleCharacter protagonist, CardType typeToDraw)
    {
        protagonistDrawnCards.Clear();
        if (protagonistHandUI != null) protagonistHandUI.SetActive(true);
        isProtagonistTargeting = false;
        currentSelectedCardIndex = -1; 
        selectedProtagonistCard = null;

        for (int i = 0; i < 3; i++)
        {
            // 💥 protagonistDrawnCards를 제외 목록으로 넘겨서, 덱이 부족해 도중에 리셔플이
            //    일어나도 이번 손패에 이미 나온 카드가 다시 뽑히지 않도록 합니다.
            SkillCard card = protagonist.DrawRandomCard(typeToDraw, CardCategory.Action, protagonistDrawnCards);
            protagonistDrawnCards.Add(card);

            if (card != null)
            {
                if (i < handCardTexts.Length) handCardTexts[i].text = ""; 
                
                if (i < handCardImages.Length && handCardImages[i] != null)
                {
                    handCardImages[i].gameObject.SetActive(true); 
                    handCardImages[i].sprite = card.cardImage;
                    handCardImages[i].preserveAspect = true; 
                }
            }
        }
    }

    // 💥 카드 숨기기 함수
    public void HideHandUI()
    {
        if (protagonistHandUI != null) protagonistHandUI.SetActive(false);
    }

    // 💥 [새로 추가됨!!] 카드를 다시 켜주는 함수 
    public void ShowHandUI()
    {
        if (protagonistHandUI != null) protagonistHandUI.SetActive(true);
    }

    public void SelectCard(int cardIndex)
    {
        BattleCharacter protagonist = manager.protagonistCharacter;
        if (protagonist != null && protagonist.hasActedThisTurn) return;
        if (protagonistDrawnCards[cardIndex] == null) return;

        // 💥 실제로 카드가 눌린 경우에만 소리를 냅니다.
        BattleSfx.Play(BattleSfxType.ButtonClick);

        if (currentSelectedCardIndex == cardIndex && isProtagonistTargeting)
        {
            CancelCardSelection();
            return;
        }

        currentSelectedCardIndex = cardIndex;
        selectedProtagonistCard = protagonistDrawnCards[cardIndex];

        // 💥 자신 / 아군 전체를 대상으로 하는 카드는 클릭할 대상이 없으므로 바로 사용합니다.
        if (selectedProtagonistCard.targetType == SkillCard.TargetType.Self
            || selectedProtagonistCard.targetType == SkillCard.TargetType.AllAllies)
        {
            SkillCard cardToUse = selectedProtagonistCard;
            manager.ExecuteProtagonistSelfCard(cardToUse);
            ConsumeSelectedCard();
            return;
        }

        isProtagonistTargeting = true;

        ShowTargetingTooltip();
    }

    public void CancelCardSelection()
    {
        currentSelectedCardIndex = -1;
        isProtagonistTargeting = false;
        selectedProtagonistCard = null;

        BattleCharacter protagonist = manager.protagonistCharacter;
        if (protagonist != null && protagonist.IsAlive)
        {
            // 💥 선택을 취소해도 주인공의 공격/수비 아이콘은 계속 떠 있어야 합니다.
            //    HideIntent()로 통째로 꺼버리면 여기서 '버튼이 사라지는' 버그가 됩니다.
            CardType pType = (manager.currentPhase == BattlePhase.PlayerTurn) ? CardType.Attack : CardType.Defense;
            protagonist.ShowIntentForType(pType, protagonist.hasActedThisTurn);
        }

        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    public void ConsumeSelectedCard()
    {
        if (currentSelectedCardIndex != -1)
        {
            protagonistDrawnCards[currentSelectedCardIndex] = null; 
            if (currentSelectedCardIndex < handCardTexts.Length) handCardTexts[currentSelectedCardIndex].text = "";
            if (currentSelectedCardIndex < handCardImages.Length && handCardImages[currentSelectedCardIndex] != null)
            {
                handCardImages[currentSelectedCardIndex].sprite = null;
                handCardImages[currentSelectedCardIndex].gameObject.SetActive(false);
            }
        }
        CancelCardSelection(); 
        HideHandUI();
    }

    private string GetCardDetails(SkillCard card, bool isAttack)
    {
        if (card == null) return "카드 없음 / 대기 중";
        string stat = isAttack ? $"위력: {card.attackPower}" : $"방어: {card.defensePower}";
        string effect = string.IsNullOrEmpty(card.cardEffect) ? "" : $"\n<color=yellow>{card.cardEffect}</color>";
        // 💥 이름 자리에는 내부 식별용 cardName 대신, 실제로 보여줄 cardDescription을 씁니다.
        //    (설명이 비어있는 카드는 예전처럼 cardName으로 대체합니다)
        string displayName = string.IsNullOrEmpty(card.cardDescription) ? card.cardName : card.cardDescription;
        return $"{displayName} ({stat}){effect}";
    }

    // 💥 체력 수치 텍스트 갱신 함수
    private void UpdateHpCircle(Image hpImage, TextMeshProUGUI hpText, BattleCharacter character)
    {
        if (hpImage == null) return;
        
        if (character != null && character.IsAlive)
        {
            hpImage.gameObject.SetActive(true);
            hpImage.fillAmount = (float)character.currentHp / character.maxHp;

            if (hpText != null)
            {
                hpText.gameObject.SetActive(true);
                // 💥 현재 걸려 있는 버프/디버프를 체력 아래에 함께 보여줍니다.
                string status = character.GetStatusText();
                hpText.text = $"{character.currentHp} / {character.maxHp}" + (string.IsNullOrEmpty(status) ? "" : $"\n<size=60%><color=#FFD37A>{status}</color></size>");
            }
        }
        else
        {
            hpImage.gameObject.SetActive(false); 
            if (hpText != null) hpText.gameObject.SetActive(false); 
        }
    }

    private void ShowTargetingTooltip()
    {
        bool isAtkPhase = (manager.currentPhase == BattlePhase.PlayerTurn);
        string phaseLabel = isAtkPhase ? "공격" : "수비";
        leftCardText.text = $"<b>[{phaseLabel}]</b> <color=yellow>선택됨</color>\n{GetCardDetails(selectedProtagonistCard, isAtkPhase)}";
        if (leftCardImage != null)
        {
            leftCardImage.gameObject.SetActive(selectedProtagonistCard.cardImage != null);
            leftCardImage.sprite = selectedProtagonistCard.cardImage;
            leftCardImage.preserveAspect = true;
        }
        
        rightCardText.text = "\n\n<size=120%><color=yellow>조준할 대상을 선택하세요!</color></size>";
        if (rightCardImage != null) rightCardImage.gameObject.SetActive(false);

        UpdateHpCircle(leftHpCircleImage, leftHpText, manager.protagonistCharacter);
        UpdateHpCircle(rightHpCircleImage, rightHpText, null); 

        if (tooltipPanel != null) tooltipPanel.SetActive(true);
    }

    public void OnHandCardHoverEnter(int index)
    {
        if (isProtagonistTargeting) return;
        if (protagonistDrawnCards.Count <= index || protagonistDrawnCards[index] == null) return;

        BattleSfx.Play(BattleSfxType.ButtonHover);

        SkillCard card = protagonistDrawnCards[index];
        bool isAtkPhase = (manager.currentPhase == BattlePhase.PlayerTurn);
        string phaseLabel = isAtkPhase ? "공격" : "수비";
        leftCardText.text = $"<b>[{phaseLabel}]</b> 내 카드\n{GetCardDetails(card, isAtkPhase)}";
        
        if (leftCardImage != null)
        {
            leftCardImage.gameObject.SetActive(card.cardImage != null);
            leftCardImage.sprite = card.cardImage;
            leftCardImage.preserveAspect = true;
        }
        
        rightCardText.text = "\n\n<size=120%>클릭하여 카드를 선택하세요.</size>";
        if (rightCardImage != null) rightCardImage.gameObject.SetActive(false);

        UpdateHpCircle(leftHpCircleImage, leftHpText, manager.protagonistCharacter);
        UpdateHpCircle(rightHpCircleImage, rightHpText, null);

        if (tooltipPanel != null) tooltipPanel.SetActive(true);
    }

    public void OnHandCardHoverExit(int index)
    {
        if (isProtagonistTargeting) return; 
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    public void ProcessHoverEnter(BattleCharacter hoveredChar)
    {
        if (tooltipPanel != null)
        {
            SkillCard lCard = null; string lText = ""; BattleCharacter lChar = null;
            SkillCard rCard = null; string rText = ""; BattleCharacter rChar = null;

            // 💥 좌측은 항상 아군(주인공/아군), 우측은 항상 적군으로 고정합니다.
            //    [공격] / [수비] 표기만 현재 마디에서의 실제 역할을 따라갑니다.
            if (manager.currentPhase == BattlePhase.PlayerTurn)
            {
                // 공격 마디: 아군이 공격 / 적군이 수비
                if (isProtagonistTargeting && hoveredChar.role == CharacterRole.Enemy)
                {
                    lChar = manager.protagonistCharacter; lCard = selectedProtagonistCard;
                    lText = $"<b>[공격]</b> <color=yellow>선택됨</color>\n{GetCardDetails(lCard, true)}";

                    rChar = hoveredChar; rCard = hoveredChar.preparedCard;
                    rText = $"<b>[수비]</b>\n{GetCardDetails(rCard, false)}";
                }
                else if (hoveredChar.role == CharacterRole.Ally)
                {
                    lChar = hoveredChar; lCard = hoveredChar.preparedCard;
                    lText = $"<b>[공격]</b>\n{GetCardDetails(lCard, true)}";

                    // 💥 이미 공격을 마쳤으면 preparedTarget이 남아있어도 '지나간' 정보입니다.
                    //    더 이상 상대방 체력/수비를 보여주지 않고 대기중으로 채웁니다.
                    if (!hoveredChar.hasActedThisTurn && hoveredChar.preparedTarget != null)
                    {
                        rChar = hoveredChar.preparedTarget; rCard = rChar.preparedCard;
                        rText = $"<b>[수비]</b>\n{GetCardDetails(rCard, false)}";
                    }
                    else rText = "\n\n<size=120%>대기 중</size>";
                }
                else if (hoveredChar.role == CharacterRole.Enemy)
                {
                    // 좌측=아군/우측=적군 규칙은 혼자 훑어볼 때도 예외 없이 유지합니다.
                    rChar = hoveredChar; rCard = hoveredChar.preparedCard;
                    rText = $"<b>[수비]</b>\n{GetCardDetails(rCard, false)}";
                    lText = "\n\n<size=120%>대기 중</size>";
                }
            }
            else if (manager.currentPhase == BattlePhase.EnemyTurn)
            {
                // 수비 마디: 적군이 공격 / 아군이 수비
                if (hoveredChar.role == CharacterRole.Enemy)
                {
                    rChar = hoveredChar; rCard = hoveredChar.preparedCard;
                    rText = $"<b>[공격]</b>\n{GetCardDetails(rCard, true)}";

                    if (isProtagonistTargeting && selectedProtagonistCard != null && hoveredChar.preparedTarget == manager.protagonistCharacter)
                    {
                        lChar = manager.protagonistCharacter; lCard = selectedProtagonistCard;
                        lText = $"<b>[수비]</b> <color=yellow>나의 개입</color>\n{GetCardDetails(lCard, false)}";
                    }
                    // 💥 이미 공격을 마쳤으면 preparedTarget이 남아있어도 '지나간' 정보라 대기중으로 채웁니다.
                    else if (!hoveredChar.hasActedThisTurn && hoveredChar.preparedTarget != null)
                    {
                        lChar = hoveredChar.preparedTarget; lCard = lChar.preparedCard;
                        lText = $"<b>[수비]</b>\n{GetCardDetails(lCard, false)}";
                    }
                    else lText = "\n\n<size=120%>대기 중</size>";
                }
                else if (hoveredChar.role == CharacterRole.Ally || hoveredChar.role == CharacterRole.Protagonist)
                {
                    lChar = hoveredChar; lCard = hoveredChar.preparedCard;
                    lText = $"<b>[수비]</b>\n{GetCardDetails(lCard, false)}";

                    BattleCharacter firstAttacker = manager.enemyTeam.Find(e => e != null && e.IsAlive && e.preparedTarget == hoveredChar && !e.hasActedThisTurn);

                    if (firstAttacker != null)
                    {
                        rChar = firstAttacker; rCard = firstAttacker.preparedCard;
                        rText = $"<b>[공격]</b>\n{GetCardDetails(rCard, true)}";
                    }
                    else rText = "\n\n<size=120%><color=green><b>안전함</b></color></size>";
                }
            }

            if (leftCardText != null) leftCardText.text = lText;
            if (rightCardText != null) rightCardText.text = rText;

            if (leftCardImage != null)
            {
                leftCardImage.gameObject.SetActive(lCard != null && lCard.cardImage != null);
                if (lCard != null && lCard.cardImage != null) 
                {
                    leftCardImage.sprite = lCard.cardImage;
                    leftCardImage.preserveAspect = true; 
                }
            }
            if (rightCardImage != null)
            {
                rightCardImage.gameObject.SetActive(rCard != null && rCard.cardImage != null);
                if (rCard != null && rCard.cardImage != null) 
                {
                    rightCardImage.sprite = rCard.cardImage;
                    rightCardImage.preserveAspect = true; 
                }
            }

            UpdateHpCircle(leftHpCircleImage, leftHpText, lChar);
            UpdateHpCircle(rightHpCircleImage, rightHpText, rChar);

            tooltipPanel.SetActive(true);
        }
    }

    public void ProcessHoverExit(BattleCharacter hoveredChar)
    {
        if (isProtagonistTargeting && selectedProtagonistCard != null)
        {
            ShowTargetingTooltip();
        }
        else
        {
            if (tooltipPanel != null) tooltipPanel.SetActive(false);
        }
    }

    // 💥 1. 전투 연출 중 상단 툴팁 패널에 두 싸움꾼의 정보만 강제로 띄우는 함수
    //    좌측은 항상 아군(주인공/아군), 우측은 항상 적군으로 고정하고
    //    [공격] / [수비] 표기만 실제 역할을 따라가게 합니다.
    public void ShowClashPanel(BattleCharacter attacker, SkillCard atkCard, BattleCharacter defender, SkillCard defCard)
    {
        if (tooltipPanel == null) return;

        bool attackerIsEnemy = (attacker != null && attacker.role == CharacterRole.Enemy);

        BattleCharacter leftChar  = attackerIsEnemy ? defender : attacker;
        SkillCard       leftCard  = attackerIsEnemy ? defCard  : atkCard;
        BattleCharacter rightChar = attackerIsEnemy ? attacker : defender;
        SkillCard       rightCard = attackerIsEnemy ? atkCard  : defCard;

        // 적이 공격자가 아니라면 = 왼쪽(아군)이 공격자
        bool leftIsAttacker = !attackerIsEnemy;

        // 체력 갱신(RefreshHpUI)이 같은 배치를 따르도록 기억해 둡니다.
        panelLeftChar  = leftChar;
        panelRightChar = rightChar;

        string leftLabel  = leftIsAttacker ? "공격" : "수비";
        string rightLabel = leftIsAttacker ? "수비" : "공격";

        leftCardText.text = $"<b>[{leftLabel}]</b>\n{GetCardDetails(leftCard, leftIsAttacker)}";
        if (leftCardImage != null)
        {
            leftCardImage.gameObject.SetActive(leftCard != null && leftCard.cardImage != null);
            if (leftCard != null && leftCard.cardImage != null) { leftCardImage.sprite = leftCard.cardImage; leftCardImage.preserveAspect = true; }
        }

        rightCardText.text = $"<b>[{rightLabel}]</b>\n{GetCardDetails(rightCard, !leftIsAttacker)}";
        if (rightCardImage != null)
        {
            rightCardImage.gameObject.SetActive(rightCard != null && rightCard.cardImage != null);
            if (rightCard != null && rightCard.cardImage != null) { rightCardImage.sprite = rightCard.cardImage; rightCardImage.preserveAspect = true; }
        }

        UpdateHpCircle(leftHpCircleImage, leftHpText, leftChar);
        UpdateHpCircle(rightHpCircleImage, rightHpText, rightChar);

        tooltipPanel.SetActive(true);
    }

    // 💥 1.5. 자신/아군을 대상으로 하는 '지원' 카드용 패널. 방어 카드가 없으므로 ShowClashPanel과 분리했습니다.
    public void ShowSupportPanel(BattleCharacter caster, SkillCard card, BattleCharacter target)
    {
        if (tooltipPanel == null) return;

        bool sameChar = (target == null || target == caster);

        panelLeftChar  = caster;
        panelRightChar = sameChar ? null : target;

        leftCardText.text = $"<b>[지원]</b>\n{GetCardDetails(card, true)}";
        if (leftCardImage != null)
        {
            leftCardImage.gameObject.SetActive(card != null && card.cardImage != null);
            if (card != null && card.cardImage != null) { leftCardImage.sprite = card.cardImage; leftCardImage.preserveAspect = true; }
        }

        if (sameChar)
        {
            // 자신에게 쓰는 카드는 오른쪽 칸을 비웁니다.
            rightCardText.text = "\n\n<size=120%>대상: 자신</size>";
            if (rightCardImage != null) rightCardImage.gameObject.SetActive(false);
        }
        else
        {
            rightCardText.text = $"<b>[대상]</b>\n{target.characterName}";
            if (rightCardImage != null) rightCardImage.gameObject.SetActive(false);
        }

        UpdateHpCircle(leftHpCircleImage, leftHpText, panelLeftChar);
        UpdateHpCircle(rightHpCircleImage, rightHpText, panelRightChar);

        tooltipPanel.SetActive(true);
    }

    // 💥 2. 데미지를 입은 직후 즉시 체력 숫자와 게이지를 깎아주는(새로고침) 함수
    public void RefreshHpUI(BattleCharacter attacker, BattleCharacter defender)
    {
        // ShowClashPanel이 정해둔 좌(아군)/우(적군) 배치를 그대로 따릅니다.
        UpdateHpCircle(leftHpCircleImage, leftHpText, panelLeftChar);
        UpdateHpCircle(rightHpCircleImage, rightHpText, panelRightChar);
    }
    // 💥 3. 연출이 모두 끝나면 상단 패널을 완전히 숨기는 함수
    public void HideTooltip()
    {
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    // 💥 마디 글자가 스르륵 나타났다 사라지는 연출 코루틴
    // 💥 소속(System.Collections.)을 정확히 붙여서 에러 해결!
    public System.Collections.IEnumerator ShowMeasureText(string text)
    {
        if (measureText == null) yield break;
        
        measureText.text = text;
        measureText.gameObject.SetActive(true);
        Color c = measureText.color;
        c.a = 0f; measureText.color = c;

        // 페이드 인
        while (c.a < 1f) { c.a += Time.deltaTime * 2f; measureText.color = c; yield return null; }
        yield return new WaitForSeconds(1.0f); // 1초 대기
        
        // 페이드 아웃
        while (c.a > 0f) { c.a -= Time.deltaTime * 2f; measureText.color = c; yield return null; }
        measureText.gameObject.SetActive(false);
    }
}
