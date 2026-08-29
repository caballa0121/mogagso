using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("이동 수치")]
    public float moveSpeed = 5f;

    private Rigidbody2D rb;
    private float moveInput;

    private Animator anim;
    private SpriteRenderer spriteRenderer;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        // 1. 좌우 방향 전환 (입력값에 따라 스프라이트 뒤집기)
        if (moveInput > 0)
        {
            spriteRenderer.flipX = false; // 오른쪽
        }
        else if (moveInput < 0)
        {
            spriteRenderer.flipX = true;  // 왼쪽
        }

        // 2. 걷기 애니메이션 파라미터 전달
        if (anim != null)
        {
            // 이동 중인지 체크 (절댓값이 0보다 크면 걷는 중)
            bool isMoving = Mathf.Abs(moveInput) > 0.1f;
            anim.SetBool("IsWalking", isMoving);
        }
    }

    void FixedUpdate()
    {
        // y축 속도는 유지하고 x축만 조작
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
    }

    private void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>().x;
    }
}