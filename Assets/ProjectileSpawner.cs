using System.Collections;
using UnityEngine;

public enum SpawnPattern
{
    Straight,   // 정해진 방향으로 직진
    Aimed,      // 발사 순간 플레이어를 조준
    Spread,     // 부채꼴로 여러 발
    Random      // 무작위 방향(복도 축 기준 좌우로 흔들림)
}

/// <summary>웨이브마다 스포너에 넘겨주는 발사 설정</summary>
[System.Serializable]
public class WaveSpawnSettings
{
    [Tooltip("발사 간격(초). 작을수록 촘촘")]
    public float spawnInterval = 0.8f;
    [Tooltip("투사체 속도")]
    public float projectileSpeed = 6f;
    [Tooltip("한 번에 몇 발 나갈지")]
    public int burstCount = 1;
    [Tooltip("Spread일 때 부채꼴 각도(도)")]
    public float spreadAngle = 20f;
    [Tooltip("발사 패턴")]
    public SpawnPattern pattern = SpawnPattern.Aimed;
    [Tooltip("한 발당 피해량")]
    public int damage = 1;
    [Tooltip("첫 발사까지의 여유 시간(초)")]
    public float startDelay = 0.5f;
}

/// <summary>
/// 복도 안쪽(피아노 쪽)에 배치해서 플레이어 쪽으로 투사체를 날립니다.
/// 스포너 오브젝트의 파란 축(+Z, forward)이 발사 기본 방향입니다.
/// </summary>
public class ProjectileSpawner : MonoBehaviour
{
    [Header("투사체 프리팹 (BossProjectile 부착 필수)")]
    public GameObject projectilePrefab;

    [Header("발사구 (비워두면 이 오브젝트 위치에서 발사)")]
    public Transform[] muzzles;

    [Tooltip("체크하면 매번 발사구 하나를 랜덤으로 고릅니다. 해제하면 모든 발사구가 동시 발사")]
    public bool randomizeMuzzle = true;

    [Header("2.5D 설정")]
    [Tooltip("체크하면 상하(Y) 방향을 제거해 XZ 평면으로만 날아갑니다")]
    public bool lockVertical = true;

    [Header("디버그")]
    public bool debugLog = false;

    private Coroutine loop;
    private Transform playerTf;

    /// <summary>웨이브 시작 시 BossSequence가 호출합니다.</summary>
    public void BeginWave(WaveSpawnSettings settings, Transform player)
    {
        StopWave();
        playerTf = player;
        loop = StartCoroutine(SpawnLoop(settings));
    }

    public void StopWave()
    {
        if (loop != null)
        {
            StopCoroutine(loop);
            loop = null;
        }
    }

    private IEnumerator SpawnLoop(WaveSpawnSettings s)
    {
        if (projectilePrefab == null)
        {
            Debug.LogError($"[{name}] Projectile Prefab이 비어있습니다!", this);
            yield break;
        }

        yield return new WaitForSeconds(Mathf.Max(0f, s.startDelay));

        float interval = Mathf.Max(0.05f, s.spawnInterval);

        while (true)
        {
            Fire(s);
            yield return new WaitForSeconds(interval);
        }
    }

    private void Fire(WaveSpawnSettings s)
    {
        if (randomizeMuzzle || muzzles == null || muzzles.Length == 0)
        {
            Transform m = PickMuzzle();
            FireFrom(m, s);
        }
        else
        {
            foreach (Transform m in muzzles)
            {
                if (m != null) FireFrom(m, s);
            }
        }
    }

    private Transform PickMuzzle()
    {
        if (muzzles == null || muzzles.Length == 0) return transform;
        Transform m = muzzles[Random.Range(0, muzzles.Length)];
        return m != null ? m : transform;
    }

    private void FireFrom(Transform muzzle, WaveSpawnSettings s)
    {
        Vector3 baseDir = GetBaseDirection(muzzle, s.pattern);
        int count = Mathf.Max(1, s.burstCount);

        for (int i = 0; i < count; i++)
        {
            Vector3 dir = baseDir;

            if (s.pattern == SpawnPattern.Spread && count > 1)
            {
                // -spreadAngle/2 ~ +spreadAngle/2 사이로 균등 분배
                float t = (float)i / (count - 1);          // 0 ~ 1
                float angle = Mathf.Lerp(-s.spreadAngle * 0.5f, s.spreadAngle * 0.5f, t);
                dir = Quaternion.AngleAxis(angle, Vector3.up) * baseDir;
            }
            else if (s.pattern == SpawnPattern.Random)
            {
                float angle = Random.Range(-s.spreadAngle * 0.5f, s.spreadAngle * 0.5f);
                dir = Quaternion.AngleAxis(angle, Vector3.up) * baseDir;
            }

            if (lockVertical) dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;

            GameObject go = Instantiate(projectilePrefab, muzzle.position, Quaternion.identity);
            BossProjectile bp = go.GetComponent<BossProjectile>();

            if (bp != null)
            {
                bp.Launch(dir, s.projectileSpeed, s.damage);
            }
            else
            {
                Debug.LogError($"[{name}] 프리팹에 BossProjectile이 없습니다!", this);
                Destroy(go);
            }
        }

        if (debugLog) Debug.Log($"[{name}] {count}발 발사 ({s.pattern})");
    }

    private Vector3 GetBaseDirection(Transform muzzle, SpawnPattern pattern)
    {
        if (pattern == SpawnPattern.Aimed && playerTf != null)
        {
            Vector3 toPlayer = playerTf.position - muzzle.position;
            if (lockVertical) toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.0001f) return toPlayer.normalized;
        }

        // 기본값: 스포너의 정면(파란 축)
        Vector3 fwd = muzzle.forward;
        if (lockVertical) fwd.y = 0f;
        return fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f);

        if (muzzles != null && muzzles.Length > 0)
        {
            foreach (Transform m in muzzles)
            {
                if (m == null) continue;
                Gizmos.DrawSphere(m.position, 0.2f);
                Gizmos.DrawRay(m.position, m.forward * 2f);
            }
        }
        else
        {
            Gizmos.DrawSphere(transform.position, 0.2f);
            Gizmos.DrawRay(transform.position, transform.forward * 2f);
        }
    }
}
