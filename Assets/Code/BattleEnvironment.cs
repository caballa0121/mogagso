using UnityEngine;
using System.Collections.Generic;

public class BattleEnvironment : MonoBehaviour
{
    [Header("전투 씬에 배치된 모든 배경 리스트")]
    public List<GameObject> allBackgrounds;

    void Awake()
    {
        // 컷씬에서 넘겨받은 배경 이름이 있다면?
        if (!string.IsNullOrEmpty(BattleContext.currentStageID))
        {
            SetActiveBackgroundByName(BattleContext.currentStageID);
        }
    }

    // 이름이 일치하는 배경만 켜고 나머지는 끕니다. (컷씬 인계용)
    public void SetActiveBackgroundByName(string stageID)
    {
        foreach (var bg in allBackgrounds)
        {
            if (bg != null) bg.SetActive(bg.name == stageID);
        }
    }

    // 💥 웨이브 전환처럼 코드에서 직접 특정 배경 오브젝트를 지정해 켤 때 씁니다.
    //    target이 allBackgrounds 목록에 없어도(웨이브 전용 배경이라도) 상관없이 그것만 켜고 나머지는 끕니다.
    public void SetActiveBackground(GameObject target)
    {
        if (target == null) return;

        foreach (var bg in allBackgrounds)
        {
            if (bg != null) bg.SetActive(bg == target);
        }

        // 목록에 등록 안 된 배경이면(웨이브 전용으로 따로 넣어둔 오브젝트) 직접 켜줍니다.
        if (!allBackgrounds.Contains(target)) target.SetActive(true);
    }
}
