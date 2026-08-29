using UnityEngine;

// MonoBehaviour를 상속받지 않는 순수 static 클래스입니다.
public static class BattleContext
{
    // 어떤 배경(혹은 스테이지)에서 전투가 시작되었는지 기억하는 변수
    public static string currentStageID = ""; 
}