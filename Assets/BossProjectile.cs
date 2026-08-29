using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스가 날리는 투사체.
///
/// ★ 콜라이더를 쓰지 않고 '거리'로 피격을 판정합니다.
///   CharacterController + 2D/3D 혼용 환경에서 트리거가 안 먹는 문제를 원천 차단합니다.
///
/// 프리팹 구성 예시:
///   ProjectilePrefab (빈 오브젝트)
///     └ Sprite (SpriteRenderer + Billboard.cs)
/// </summary>
public class BossProjectile : MonoBehaviour
{
    // 웨이브 종료 시 한 번에 정리하기 위한 등록부
    private static readonly List<BossProjectile> active = new List<BossProjectile>();

    [Header("이동")]
    public float speed = 6f;
    public Vector3 direction = Vector3.forward;

    [Header("판정")]
    [Tooltip("플레이어와 이 거리 안으로 들어오면 피격")]
    public float hitRadius = 0.7f;
    public int damage = 1;

    [Header("수명")]
    public float lifetime = 10f;
    [Tooltip("이 Y좌표보다 아래로 내려가면 자동 삭제")]
    public float killY = -20f;

    [Header("연출")]
    [Tooltip("진행 방향으로 회전시킬지 여부")]
    public bool rotateToDirection = false;
    public float spinSpeed = 0f;

    private float age;

    private void OnEnable() { active.Add(this); }
    private void OnDisable() { active.Remove(this); }

    /// <summary>스포너가 발사 직후 호출합니다.</summary>
    public void Launch(Vector3 dir, float spd, int dmg)
    {
        direction = dir.normalized;
        speed = spd;
        damage = dmg;
        age = 0f;

        if (rotateToDirection && direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    private void Update()
    {
        // 1) 이동
        transform.position += direction * speed * Time.deltaTime;

        if (spinSpeed != 0f)
        {
            transform.Rotate(Vector3.forward, spinSpeed * Time.deltaTime, Space.Self);
        }

        // 2) 수명 / 낙하 체크
        age += Time.deltaTime;
        if (age >= lifetime || transform.position.y < killY)
        {
            Destroy(gameObject);
            return;
        }

        // 3) 피격 판정 (거리 기반)
        PlayerHealth player = PlayerHealth.Instance;
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist <= hitRadius)
        {
            if (!player.IsInvincible && !player.IsDead)
            {
                player.TakeDamage(damage);
                Destroy(gameObject);
            }
        }
    }

    /// <summary>씬에 남아있는 모든 투사체를 제거합니다. (웨이브 종료/사망 시)</summary>
    public static void ClearAll()
    {
        BossProjectile[] copy = active.ToArray();
        foreach (BossProjectile p in copy)
        {
            if (p != null) Destroy(p.gameObject);
        }
        active.Clear();
    }

    public static int ActiveCount => active.Count;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, hitRadius);
    }
}
