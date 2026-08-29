using System.Collections.Generic;
using UnityEngine;

public class CombatResolver : MonoBehaviour
{
    private BattleManager manager;
    private HashSet<BattleCharacter> usedDefenseCharacters = new HashSet<BattleCharacter>();

    void Awake()
    {
        manager = GetComponent<BattleManager>();
    }

    public void ClearDefenseRecords() { usedDefenseCharacters.Clear(); }

    public SkillCard GetDefenseCard(BattleCharacter defender)
    {
        if (usedDefenseCharacters.Contains(defender)) return null;
        SkillCard card = defender.DrawRandomCard(CardType.Defense, CardCategory.Action);
        if (card != null) usedDefenseCharacters.Add(defender);
        return card;
    }

    // ==========================================
    // 💥 카드에 담긴 부가 효과를 실제 대상에게 뿌려주는 부분
    // ==========================================

    // scope 설정에 따라 효과를 받을 캐릭터 목록을 만들어 줍니다.
    private List<BattleCharacter> ResolveScope(BattleCharacter caster, BattleCharacter target, EffectScope scope)
    {
        List<BattleCharacter> result = new List<BattleCharacter>();
        if (manager == null) { if (caster != null) result.Add(caster); return result; }

        bool casterIsEnemy = (caster != null && caster.role == CharacterRole.Enemy);
        List<BattleCharacter> ownTeam = casterIsEnemy ? manager.enemyTeam : manager.playerTeam;
        List<BattleCharacter> foeTeam = casterIsEnemy ? manager.playerTeam : manager.enemyTeam;

        switch (scope)
        {
            case EffectScope.Self:
                if (caster != null) result.Add(caster);
                break;
            case EffectScope.Target:
                if (target != null) result.Add(target);
                break;
            case EffectScope.AllAllies:
                foreach (var c in ownTeam) if (c != null && c.IsAlive) result.Add(c);
                break;
            case EffectScope.AllEnemies:
                foreach (var c in foeTeam) if (c != null && c.IsAlive) result.Add(c);
                break;
        }
        return result;
    }

    // 카드의 부가 효과를 전부 적용합니다.
    // ignoreDefense / extraTargetCount 는 이번 합에서만 쓰이는 즉발 결과라 밖으로 돌려줍니다.
    public void ApplyCardEffects(BattleCharacter caster, BattleCharacter target, SkillCard card,
                                 ref bool ignoreDefense, ref int extraTargetCount)
    {
        if (card == null || card.additionalEffects == null) return;

        foreach (var effect in card.additionalEffects)
        {
            if (effect.effectType == EffectType.None) continue;

            // 이번 합에서 즉시 판정되는 효과들
            if (effect.effectType == EffectType.IgnoreDefense)
            {
                ignoreDefense = true;
                Debug.Log("🎯 [방어 무시] 상대의 방어를 관통합니다.");
                continue;
            }
            if (effect.effectType == EffectType.RedirectAllyTarget)
            {
                RedirectAllyTargets(caster, target, Mathf.Max(1, effect.value));
                continue;
            }
            if (effect.effectType == EffectType.MultiTarget)
            {
                extraTargetCount += Mathf.Max(1, effect.value);
                Debug.Log($"💥 [광역 공격] 추가 대상 {extraTargetCount}명을 함께 노립니다.");
                continue;
            }

            foreach (var receiver in ResolveScope(caster, target, effect.scope))
            {
                if (receiver == null || !receiver.IsAlive) continue;

                switch (effect.effectType)
                {
                    // --- 즉발 ---
                    case EffectType.Heal:
                        receiver.currentHp = Mathf.Min(receiver.currentHp + effect.value, receiver.maxHp);
                        Debug.Log($"💚 [{receiver.characterName}] 체력 {effect.value} 회복 (현재 {receiver.currentHp})");
                        break;

                    case EffectType.Damage:
                        // 공격력/방어력 계산을 거치지 않는 고정 피해입니다. (즉사 방지를 위해 죽지는 않게 하고 싶다면
                        // 여기서 Mathf.Max(1, receiver.currentHp - effect.value) 로 바꾸면 됩니다)
                        if (effect.value > 0) BattleSfx.Play(BattleSfxType.Hit);
                        receiver.TakeDamage(Mathf.Max(0, effect.value));
                        Debug.Log($"💥 [{receiver.characterName}] 즉시 피해 {effect.value} (현재 {receiver.currentHp})");
                        break;

                    case EffectType.AddAggro:
                        receiver.aggroWeight = Mathf.Max(0, receiver.aggroWeight + effect.value);
                        Debug.Log($"🛡️ [{receiver.characterName}] 도발치 변화 {effect.value} → 현재 {receiver.aggroWeight}");
                        break;

                    // --- 지속 (공격력/방어력 증감, 독, 출혈, 반사, 회피, 합공, 원호방어) ---
                    default:
                        // 💥 원호방어(CoverAlly)는 "누구에게 오는 일방공격을 막을지"가 반드시 필요합니다.
                        //    카드의 대상(target)이 곧 지켜줄 아군입니다. (scope=Self로 caster 자신이
                        //    이 효과를 받고, target을 기억해뒀다가 나중에 그 대상이 공격받을 때 씁니다)
                        BattleCharacter protectTarget = (effect.effectType == EffectType.CoverAlly) ? target : null;

                        // 💥 공격력/방어력 증감은 '마디' 단위로 지속되는 스탯 버프라서, 시전한 이번
                        //    마디에는 적용하지 않고 다음 마디부터 duration만큼 유지됩니다.
                        //    (Thorns/Evade/ReflectAll/CoverAlly/Poison/Bleed 등은 그 즉시 필요한
                        //    효과라 여기 포함하지 않고 기존처럼 즉시 적용합니다)
                        bool isMeasureBoundStat =
                            effect.effectType == EffectType.AttackUp || effect.effectType == EffectType.AttackDown ||
                            effect.effectType == EffectType.DefenseUp || effect.effectType == EffectType.DefenseDown;

                        if (effect.condition != EffectCondition.Always || isMeasureBoundStat)
                        {
                            // 조건부(자세잡기)든, 마디 단위 스탯 버프든 — 지금 걸지 않고 마디가 끝날 때
                            // (조건이 있으면 그 조건도 함께 확인해서) 다음 마디부터 적용되게 대기시킵니다.
                            receiver.AddPendingEffect(effect.effectType, effect.value, effect.duration, effect.multiplier, effect.condition, caster, protectTarget);
                            Debug.Log($"🧘 [{receiver.characterName}] {BattleCharacter.GetEffectLabel(effect.effectType)} 대기 (다음 마디부터 적용, 조건: {effect.condition})");
                            break;
                        }

                        receiver.AddEffect(effect.effectType, effect.value, effect.duration, effect.multiplier, caster, protectTarget);
                        string coverInfo = (protectTarget != null) ? $" (대상: {protectTarget.characterName})" : "";
                        Debug.Log($"✨ [{receiver.characterName}] {BattleCharacter.GetEffectLabel(effect.effectType)} {effect.value} / {Mathf.Max(1, effect.duration)}마디{coverInfo}");
                        break;
                }
            }
        }
    }

    // 💥 아군 유도 : 아직 행동하지 않은 아군 몇 명의 공격 대상을 내가 노린 상대로 바꿉니다.
    //    (공격 마디에서만 의미가 있습니다)
    private void RedirectAllyTargets(BattleCharacter caster, BattleCharacter newTarget, int count)
    {
        if (manager == null || caster == null || newTarget == null) return;
        if (manager.currentPhase != BattlePhase.PlayerTurn) return;

        bool casterIsEnemy = (caster.role == CharacterRole.Enemy);
        List<BattleCharacter> team = casterIsEnemy ? manager.enemyTeam : manager.playerTeam;

        int changed = 0;
        foreach (var mate in team)
        {
            if (changed >= count) break;
            if (mate == null || mate == caster || !mate.IsAlive) continue;
            if (mate.hasActedThisTurn) continue;          // 이미 때린 아군은 되돌릴 수 없음
            if (mate.preparedCard == null) continue;      // 낼 카드가 없으면 제외
            if (mate.preparedTarget == newTarget) continue; // 이미 같은 대상

            mate.preparedTarget = newTarget;
            mate.ShowIntent(mate.preparedCard, newTarget);
            changed++;

            Debug.Log($"🎯 [아군 유도] {mate.characterName}의 공격 대상을 {newTarget.characterName}(으)로 돌렸습니다.");
        }

        if (changed == 0) Debug.Log("🎯 [아군 유도] 대상을 바꿀 수 있는 아군이 없습니다.");
    }

    // ==========================================
    // 💥 실제 피해 계산
    // ==========================================
    public void ResolveAction(BattleCharacter attacker, BattleCharacter defender, SkillCard atkCard, SkillCard defCard)
    {
        if (attacker == null || defender == null) return;

        bool ignoreDefense = false;
        int extraTargets = 0;

        // 1. 공격 카드 / 방어 카드의 부가 효과 발동
        if (atkCard != null) ApplyCardEffects(attacker, defender, atkCard, ref ignoreDefense, ref extraTargets);
        if (defCard != null) ApplyCardEffects(defender, attacker, defCard, ref ignoreDefense, ref extraTargets);

        // 2. 출혈 : 공격 행동을 하는 순간 자신이 피해를 입습니다.
        int bleed = attacker.GetEffectValue(EffectType.Bleed);
        if (bleed > 0)
        {
            Debug.Log($"🩸 [{attacker.characterName}] 출혈 피해 {bleed}");
            attacker.TakeDamage(bleed);
        }

        if (atkCard == null) return; // 방어 카드만 오간 경우 여기서 종료

        ApplyDamage(attacker, defender, atkCard, defCard, ignoreDefense);
    }

    // 한 명에게 피해를 넣는 실제 처리 (광역 공격의 추가 대상도 이 함수를 재사용합니다)
    public void ApplyDamage(BattleCharacter attacker, BattleCharacter defender, SkillCard atkCard, SkillCard defCard, bool ignoreDefense)
    {
        if (attacker == null || defender == null || atkCard == null) return;
        if (!defender.IsAlive) return;

        // 3. 최종 공격력 : 카드 위력 + 버프/디버프, 합공이면 배율
        int atkPower = atkCard.attackPower + attacker.AttackBonus;
        float joint = attacker.GetJointMultiplier();
        if (joint > 1f)
        {
            atkPower = Mathf.RoundToInt(atkPower * joint);
            Debug.Log($"🤝 [합공] {attacker.characterName} 공격력 {joint}배 적용 → {atkPower}");
        }
        atkPower = Mathf.Max(0, atkPower);

        // 4. 최종 방어력 : 카드 방어력 + 버프/디버프, 방어 무시면 0
        int defPower = (defCard != null ? defCard.defensePower : 0) + defender.DefenseBonus;
        defPower = Mathf.Max(0, defPower);
        if (ignoreDefense) defPower = 0;

        int damage = Mathf.Max(atkPower - defPower, 0);

        // 5. 회피 : 공격을 통째로 무효화 (1회 소모)
        if (defender.HasEffect(EffectType.Evade))
        {
            defender.ConsumeOneShot(EffectType.Evade);
            Debug.Log($"💨 [{defender.characterName}] 회피! 공격이 완전히 빗나갔습니다.");
            BattleSfx.Play(BattleSfxType.Evade);
            return;
        }

        // 6. 공격 반사 : 받을 피해를 그대로 공격자에게 되돌립니다 (1회 소모)
        if (defender.HasEffect(EffectType.ReflectAll))
        {
            defender.ConsumeOneShot(EffectType.ReflectAll);
            Debug.Log($"🪞 [{defender.characterName}] 공격 반사! {damage}의 피해를 {attacker.characterName}에게 되돌립니다.");
            BattleSfx.Play(BattleSfxType.Counter);
            if (damage > 0) attacker.TakeDamage(damage);
            return;
        }

        // 7. 피해 적용
        if (damage > 0)
        {
            BattleSfx.Play(BattleSfxType.Hit);
            defender.TakeDamage(damage);

            // 8. 반사 데미지 / 반격 : 맞은 쪽이 '최초 공격자' 한 명에게만 고정 피해
            int thorns = defender.GetEffectValue(EffectType.Thorns);
            if (thorns > 0 && attacker.IsAlive)
            {
                // 💥 터지는 즉시 소모합니다. 같은 마디에 뒤이어 때리는 적들에게는 발동하지 않습니다.
                defender.ConsumeAllOfType(EffectType.Thorns);

                Debug.Log($"🌵 [{defender.characterName}] 반격! {attacker.characterName}에게 {thorns}의 피해.");
                BattleSfx.Play(BattleSfxType.Counter);
                attacker.TakeDamage(thorns);
            }
        }
        else
        {
            Debug.Log($"[{defender.characterName}] 완벽하게 방어했습니다!");
            BattleSfx.Play(BattleSfxType.Block);
        }
    }

    // 💥 공격 카드에 광역 공격 효과가 몇 명분 붙어 있는지 미리 확인합니다.
    public int GetExtraTargetCount(SkillCard atkCard)
    {
        if (atkCard == null || atkCard.additionalEffects == null) return 0;

        int count = 0;
        foreach (var e in atkCard.additionalEffects)
            if (e.effectType == EffectType.MultiTarget) count += Mathf.Max(1, e.value);

        return count;
    }

    // 💥 합(Clash) 진행 및 종료 처리
    public void ResolveClash(BattleCharacter attacker, BattleCharacter defender, SkillCard defCard)
    {
        ResolveAction(attacker, defender, attacker.preparedCard, defCard);

        attacker.hasActedThisTurn = true;
        attacker.SetGlow(false);
        attacker.HideIntent();

        // 방어자를 향한 남은 공격이 없다면 방어 상태를 해제함
        bool stillTargeted = manager.enemyTeam.Exists(e => e != null && e.IsAlive && e.preparedTarget == defender && !e.hasActedThisTurn);
        if (!stillTargeted)
        {
            defender.SetGlow(false);
            defender.HideIntent();
        }

        manager.CheckEnemyPhaseEnd();
    }

    // 💥 스페이스바를 눌렀을 때 남은 적들의 일방 공격 처리
    public void ExecuteRemainingOneSidedAttacks()
    {
        foreach (var enemy in manager.enemyTeam)
        {
            if (enemy == null || !enemy.IsAlive || enemy.hasActedThisTurn) continue;

            // 💥 노리던 대상이 쓰러졌으면 공격이 성립하지 않으므로 차례를 넘깁니다.
            if (enemy.preparedTarget == null || !enemy.preparedTarget.IsAlive)
            {
                manager.SkipTurn(enemy, "노리던 대상이 쓰러져");
                continue;
            }

            Debug.Log($"[일방 공격] {enemy.characterName}이(가) {enemy.preparedTarget.characterName}을 일방적으로 팹니다!");
            SkillCard defCard = GetDefenseCard(enemy.preparedTarget);
            ResolveClash(enemy, enemy.preparedTarget, defCard);
        }
    }

    // 💥 주인공이 아군을 노리는 첫 공격을 대신 막아주는 기능
    public void InterceptFirstAttackAgainst(BattleCharacter defender, SkillCard interceptCard)
    {
        var attacker = manager.enemyTeam.Find(e => e.IsAlive && !e.hasActedThisTurn && e.preparedTarget == defender);
        if (attacker != null)
        {
            Debug.Log($"[주인공 보조] {defender.characterName}을 노리던 {attacker.characterName}을 주인공이 막아섭니다!");
            ResolveClash(attacker, defender, interceptCard);
        }
        else
        {
            Debug.Log("현재 이 아군을 노리는 공격이 없습니다.");
        }
    }
}
