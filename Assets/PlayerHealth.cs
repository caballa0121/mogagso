using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 보스전에서 쓰는 플레이어 체력.
/// 플레이어 오브젝트(PlayerController가 붙은 곳)에 붙입니다.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance;

    [Header("체력")]
    public int maxHP = 5;

    [Header("피격 후 무적 시간(초)")]
    public float invincibleTime = 1.2f;

    [Header("피격 연출")]
    [Tooltip("깜빡일 스프라이트. 비워두면 자식에서 자동으로 찾습니다.")]
    public SpriteRenderer[] blinkRenderers;
    public float blinkInterval = 0.1f;

    private int currentHP;
    private bool invincible = false;
    private Coroutine blinkRoutine;

    public int CurrentHP => currentHP;
    public int MaxHP => maxHP;
    public bool IsInvincible => invincible;
    public bool IsDead => currentHP <= 0;

    /// <summary>체력이 바뀔 때마다 (현재HP) 를 넘겨줍니다.</summary>
    public event Action<int> OnHPChanged;
    /// <summary>체력이 0이 되었을 때.</summary>
    public event Action OnDeath;

    private void Awake()
    {
        Instance = this;
        currentHP = maxHP;

        if (blinkRenderers == null || blinkRenderers.Length == 0)
        {
            blinkRenderers = GetComponentsInChildren<SpriteRenderer>();
        }
    }

    public void TakeDamage(int amount)
    {
        if (invincible || IsDead) return;

        currentHP -= Mathf.Max(1, amount);
        if (currentHP < 0) currentHP = 0;

        OnHPChanged?.Invoke(currentHP);

        if (PianoFX.Instance != null)
        {
            PianoFX.Instance.Flash(Color.red, 0.45f, 0.3f);
        }

        if (currentHP <= 0)
        {
            OnDeath?.Invoke();
            return;
        }

        StartInvincible();
    }

    /// <summary>웨이브 재시작 시 체력을 되돌립니다.</summary>
    public void ResetHP()
    {
        currentHP = maxHP;
        invincible = false;
        StopBlink();
        OnHPChanged?.Invoke(currentHP);
    }

    /// <summary>대사/연출 중 무적 처리용</summary>
    public void SetInvincible(bool on)
    {
        invincible = on;
        if (!on) StopBlink();
    }

    private void StartInvincible()
    {
        invincible = true;
        if (blinkRoutine != null) StopCoroutine(blinkRoutine);
        blinkRoutine = StartCoroutine(InvincibleRoutine());
    }

    private IEnumerator InvincibleRoutine()
    {
        float elapsed = 0f;
        bool visible = true;

        while (elapsed < invincibleTime)
        {
            visible = !visible;
            SetRenderersVisible(visible);

            yield return new WaitForSeconds(blinkInterval);
            elapsed += blinkInterval;
        }

        SetRenderersVisible(true);
        invincible = false;
        blinkRoutine = null;
    }

    private void StopBlink()
    {
        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }
        SetRenderersVisible(true);
    }

    private void SetRenderersVisible(bool visible)
    {
        if (blinkRenderers == null) return;
        foreach (SpriteRenderer sr in blinkRenderers)
        {
            if (sr != null) sr.enabled = visible;
        }
    }
}
