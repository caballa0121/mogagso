using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아군(주인공 포함)의 체력을 씬을 넘어가도 기억하는 곳.
///
/// BattleCharacter는 Awake()에서 currentHp = maxHp로 매번 풀피가 되기 때문에,
/// 함정에서 깎인 체력이나 지난 전투에서 닳은 체력을 그대로 이어가려면
/// 이렇게 바깥에 따로 적어둬야 합니다.
///
/// 캐릭터를 구분하는 열쇠는 characterName입니다. (비어 있으면 오브젝트 이름)
/// </summary>
public static class PartyState
{
    public struct Entry
    {
        public int current;
        public int max;
    }

    private static readonly Dictionary<string, Entry> table = new Dictionary<string, Entry>();

    // 아직 전투를 한 번도 안 해서 명단이 비어 있을 때 받은 함정 피해를 모아둡니다.
    // 다음 전투가 시작될 때 한꺼번에 반영됩니다.
    private static float pendingRatioDamage = 0f;

    public static bool HasData => table.Count > 0;
    public static IEnumerable<KeyValuePair<string, Entry>> All => table;

    private static string KeyOf(BattleCharacter c)
    {
        if (c == null) return null;
        return string.IsNullOrEmpty(c.characterName) ? c.gameObject.name : c.characterName;
    }

    /// <summary>
    /// 전투가 시작될 때 부릅니다.
    /// 명단에 없는 캐릭터는 지금 체력 그대로 등록하고, 이미 있는 캐릭터는 저장된 체력을 씌웁니다.
    /// </summary>
    public static void Apply(IList<BattleCharacter> team)
    {
        if (team == null) return;

        // 1. 처음 보는 캐릭터 등록
        foreach (var c in team)
        {
            string key = KeyOf(c);
            if (key == null) continue;
            if (table.ContainsKey(key)) continue;

            table[key] = new Entry { current = c.currentHp > 0 ? c.currentHp : c.maxHp, max = c.maxHp };
        }

        // 2. 명단이 없던 시절에 받은 함정 피해 정산
        if (pendingRatioDamage > 0f)
        {
            DamageAllByMaxRatio(pendingRatioDamage);
            pendingRatioDamage = 0f;
        }

        // 3. 저장된 체력을 실제 캐릭터에 씌우기
        foreach (var c in team)
        {
            string key = KeyOf(c);
            if (key == null) continue;
            if (!table.TryGetValue(key, out Entry e)) continue;

            // 체력 0으로 시작하면 전투가 시작되자마자 게임오버라 최소 1은 남깁니다.
            c.currentHp = Mathf.Clamp(e.current, 1, c.maxHp);
        }
    }

    /// <summary>전투가 끝났을 때 부릅니다. 남은 체력을 기록해 둡니다.</summary>
    public static void Capture(IList<BattleCharacter> team)
    {
        if (team == null) return;

        foreach (var c in team)
        {
            string key = KeyOf(c);
            if (key == null) continue;

            table[key] = new Entry { current = Mathf.Max(0, c.currentHp), max = c.maxHp };
        }
    }

    /// <summary>함정용. 전원의 최대 체력 비율만큼 깎습니다. (0.2 = 20%)</summary>
    public static void DamageAllByMaxRatio(float ratio, int minRemain = 1)
    {
        if (ratio <= 0f) return;

        if (table.Count == 0)
        {
            // 아직 명단이 없으면 나중에 정산하도록 적어만 둡니다.
            pendingRatioDamage += ratio;
            Debug.Log($"[PartyState] 파티 명단이 아직 없어 함정 피해 {ratio:P0}를 다음 전투 시작 때 반영합니다.");
            return;
        }

        var keys = new List<string>(table.Keys);
        foreach (var key in keys)
        {
            Entry e = table[key];
            int dmg = Mathf.Max(1, Mathf.RoundToInt(e.max * ratio));
            e.current = Mathf.Max(minRemain, e.current - dmg);
            table[key] = e;

            Debug.Log($"[PartyState] {key} 함정 피해 {dmg} → {e.current}/{e.max}");
        }
    }

    /// <summary>전원 최대 체력 비율만큼 회복.</summary>
    public static void HealAllByMaxRatio(float ratio)
    {
        if (ratio <= 0f) return;

        var keys = new List<string>(table.Keys);
        foreach (var key in keys)
        {
            Entry e = table[key];
            e.current = Mathf.Min(e.max, e.current + Mathf.RoundToInt(e.max * ratio));
            table[key] = e;
        }
    }

    /// <summary>패배해서 처음부터 다시 할 때. 전원 풀피로 되돌립니다.</summary>
    public static void ResetToFull()
    {
        var keys = new List<string>(table.Keys);
        foreach (var key in keys)
        {
            Entry e = table[key];
            e.current = e.max;
            table[key] = e;
        }
        pendingRatioDamage = 0f;
    }

    /// <summary>기록을 완전히 지웁니다.</summary>
    public static void Clear()
    {
        table.Clear();
        pendingRatioDamage = 0f;
    }

    /// <summary>노드맵 화면에 띄울 체력 요약 문자열.</summary>
    public static string Summary()
    {
        if (table.Count == 0) return "";

        var sb = new System.Text.StringBuilder();
        foreach (var kv in table)
        {
            if (sb.Length > 0) sb.Append("   ");
            sb.Append($"{kv.Key} {kv.Value.current}/{kv.Value.max}");
        }
        return sb.ToString();
    }
}
