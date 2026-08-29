using System.Collections.Generic;
using UnityEngine;

/// <summary>노드 한 칸이 가지고 있는 내용.</summary>
public enum MapNodeType
{
    Empty,   // 아무것도 없는 안전한 칸
    Battle,  // 전투 (전투 씬으로 넘어갔다가 이기면 돌아옴)
    Trap,    // 함정 (연출 + 파티 전원 체력 감소)
    Start,   // 시작 지점
    Goal     // 목적지 (도달하면 클리어 = 다음 씬)
}

/// <summary>
/// 노드맵의 진행 상황을 씬을 넘어가도 기억하는 곳.
/// BattleContext와 같은 방식(순수 static 클래스)입니다.
///
/// 노드맵 -> 전투 씬 -> 노드맵 으로 갔다 오는 동안 살아남아야 하므로
/// MonoBehaviour가 아니라 static으로 둡니다.
/// </summary>
public static class NodeMapContext
{
    /// <summary>전투가 노드맵에서 시작되었는지. false면 챕터에서 바로 들어온 전투입니다.</summary>
    public static bool inBattleFromMap = false;

    /// <summary>전투가 끝나고 돌아갈 노드맵 씬 이름.</summary>
    public static string returnSceneName = "";

    /// <summary>지금 전투 중인 노드의 좌표.</summary>
    public static Vector2Int battleNodeCoord;

    /// <summary>전투를 마치고 노드맵으로 막 돌아왔는지.</summary>
    public static bool returningFromBattle = false;

    /// <summary>방금 끝난 전투에서 이겼는지.</summary>
    public static bool lastBattleWon = false;

    /// <summary>주인공이 서 있는 칸.</summary>
    public static Vector2Int currentCoord;

    /// <summary>한 번이라도 노드맵을 진행한 적이 있는지. false면 시작 지점부터 시작합니다.</summary>
    public static bool hasProgress = false;

    /// <summary>이미 깬(안전한) 칸들.</summary>
    public static readonly HashSet<Vector2Int> clearedCoords = new HashSet<Vector2Int>();

    /// <summary>전투에 들어가기 직전에 노드맵이 불러줍니다.</summary>
    public static void EnterBattle(string returnScene, Vector2Int nodeCoord)
    {
        inBattleFromMap = true;
        returnSceneName = returnScene;
        battleNodeCoord = nodeCoord;
        returningFromBattle = false;
    }

    /// <summary>전투 씬이 끝날 때 BattleManager가 불러줍니다.</summary>
    public static void FinishBattle(bool won)
    {
        inBattleFromMap = false;
        returningFromBattle = true;
        lastBattleWon = won;
    }

    /// <summary>패배했을 때. 깬 기록을 전부 지우고 시작점부터 다시 하게 만듭니다.</summary>
    public static void ResetProgress()
    {
        clearedCoords.Clear();
        hasProgress = false;
        currentCoord = Vector2Int.zero;
    }

    /// <summary>목적지에 도달해서 노드맵을 완전히 떠날 때.</summary>
    public static void LeaveMap()
    {
        ResetProgress();
        inBattleFromMap = false;
        returningFromBattle = false;
        lastBattleWon = false;
        returnSceneName = "";
    }
}
