using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WaveData
{
    public string waveName;
    public GameObject[] enemyPrefabs;

    [Tooltip("이 웨이브가 시작될 때 켤 배경. 비워두면(None) 배경을 바꾸지 않고 이전 그대로 둡니다.")]
    public GameObject waveBackground;
}

public class WaveManager : MonoBehaviour
{
    [Header("웨이브 세팅")]
    public List<WaveData> waves; 
    public float timeBetweenWaves = 3f; 

    [Header("적군 소환 위치 (5~6개 등록)")]
    public Transform[] spawnPoints; 

    private int currentWaveIndex = 0;
    private List<GameObject> activeEnemies = new List<GameObject>();

    // 💥 등록된 웨이브를 전부 클리어했는지. BattleManager가 이걸 보고 승리 처리를 합니다.
    public bool AllWavesCleared => currentWaveIndex >= waves.Count;

    // 💥 BattleManager가 씬이 시작될 때 이 함수를 호출하여 진형을 펼칩니다! (기존 Start() 제거됨)
    public IEnumerator SpawnWaveAndMoveToFormation(BattleManager bManager)
    {
        if (currentWaveIndex >= waves.Count)
        {
            Debug.Log("🎉 모든 웨이브 클리어! (게임 승리)");
            yield break; 
        }

        WaveData currentWave = waves[currentWaveIndex];
        Debug.Log($"⚔️ {currentWave.waveName} 시작!");

        // 💥 이 웨이브에 지정된 배경이 있으면 교체합니다. (없으면 이전 배경 그대로 유지)
        if (currentWave.waveBackground != null)
        {
            BattleEnvironment env = FindAnyObjectByType<BattleEnvironment>();
            if (env != null) env.SetActiveBackground(currentWave.waveBackground);
        }

        if (bManager != null)
        {
            bManager.enemyTeam.RemoveAll(e => e == null); // 시체 데이터 깔끔하게 청소
        }

        List<Coroutine> moveCoroutines = new List<Coroutine>();

        // 1. 적군 소환 및 이동 (진형 전개)
        for (int i = 0; i < currentWave.enemyPrefabs.Length; i++)
        {
            if (i >= spawnPoints.Length) break;

            GameObject prefab = currentWave.enemyPrefabs[i];
            BattleCharacter charData = prefab.GetComponent<BattleCharacter>();
            
            // 중앙 출신(컷씬에 있던 적)이면 중앙에, 증원군이면 오른쪽 화면 완전 밖(10f)에 소환!
            Vector3 spawnPos = (charData != null && charData.startInCenter) 
                ? new Vector3(1f, spawnPoints[i].position.y, 0) 
                : spawnPoints[i].position + Vector3.right * 10f; 

            GameObject newEnemy = Instantiate(prefab, spawnPos, spawnPoints[i].rotation);
            activeEnemies.Add(newEnemy);

            BattleCharacter enemyChar = newEnemy.GetComponent<BattleCharacter>();
            if (enemyChar != null)
            {
                if (bManager != null) bManager.enemyTeam.Add(enemyChar);
                
                // 지정된 자기 자리로 스르륵 미끄러져 들어가도록 명령
                moveCoroutines.Add(StartCoroutine(enemyChar.MoveToFormation(spawnPoints[i].position)));
            }
        }
        
        // 2. 아군(주인공 포함)도 흩어지게 하기
        if (bManager != null)
        {
            foreach(var p in bManager.playerTeam)
            {
                if (p == null || !p.IsAlive) continue;
                Vector3 targetPos = p.transform.position; // 에디터에 배치해둔 최종 도착 목표점
                
                // 중앙 출신이면 중앙으로 강제 이동시켰다가 출발시킴
                if (p.startInCenter) p.transform.position = new Vector3(-1f, targetPos.y, 0); 
                else p.transform.position = targetPos + Vector3.left * 10f; 
                
                moveCoroutines.Add(StartCoroutine(p.MoveToFormation(targetPos)));
            }
        }

        // 3. 모든 캐릭터가 제자리에 도착할 때까지(코루틴 종료) 대기!
        foreach(var c in moveCoroutines) yield return c;
    }

    public void EnemyDied(GameObject enemy)
    {
        if (activeEnemies.Contains(enemy))
        {
            activeEnemies.Remove(enemy);
            
            // 적군 전멸 시 = 다음 웨이브 진행
            if (activeEnemies.Count == 0)
            {
                currentWaveIndex++;
                
                // 💥 기존처럼 혼자 StartWave()를 부르지 않고, BattleManager에게 다음 웨이브 세팅을 부탁합니다.
                BattleManager bManager = FindAnyObjectByType<BattleManager>();
                if (bManager != null)
                {
                    bManager.StartCoroutine(bManager.HandleNextWave(this));
                }
            }
        }
    }
}