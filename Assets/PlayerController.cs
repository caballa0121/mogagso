using UnityEngine;
using UnityEngine.Rendering;

public class PlayerController : MonoBehaviour
{
    [Header("이동 설정")]
    public float moveSpeed = 5f;

    [Header("점프 및 중력 설정")]
    public float jumpForce = 5f;       
    public float gravity = 9.81f;      

    [Header("대쉬 설정")]
    [Tooltip("대쉬 중 이동 속도입니다.")]
    public float dashSpeed = 15f;       

    [Tooltip("대쉬가 유지되는 시간(초)입니다.")]
    public float dashDuration = 0.2f;   

    [Tooltip("대쉬 재사용 대기 시간(초)입니다.")]
    public float dashCooldown = 1.0f;   
    
    private CharacterController controller;
    private Vector3 moveDirection;     
    private float verticalVelocity;     

    // 대쉬 관련 내부 변수
    private bool isDashing = false;
    private float dashTimeLeft;
    private float cooldownTimer;
    private Vector3 dashDirection;
    private Vector3 lastDashDirection = Vector3.forward; // [수정] 대쉬용 3D 마지막 방향

    // 애니메이션 관련 변수
    private Animator animator;
    private Vector2 lastLookDirection = new Vector2(0, -1); // [수정] 애니메이션용 2D 시선 방향 (기본 정면)

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        // 유니티 6 2.5D 탑뷰 정렬 축 설정
        GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;
        GraphicsSettings.transparencySortAxis = new Vector3(0f, 1f, 1f);
    }

    void Update()
    {
        // --- [추가] 대화 중일 경우 플레이어 이동 및 애니메이션 멈춤 ---
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
        {
            // 대화 시작 시 이동 속도를 0으로 바꾸고 Idle 애니메이션 상태 고정
            animator.SetFloat("Speed", 0f);
            return; // 이하 이동/점프/대쉬 입력 스킵
        }

        // 1. 이동 입력 축 접수
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputZ = Input.GetAxisRaw("Vertical"); // 2.5D XZ 평면 이동

        // 2. 애니메이션 방향 제어 로직
        Vector2 inputDir = new Vector2(inputX, inputZ);

        if (inputDir.magnitude > 0)
        {
            // 상하/좌우 중 더 강하게 누른 축의 시선 방향 지정
            if (Mathf.Abs(inputDir.x) > Mathf.Abs(inputDir.y))
            {
                lastLookDirection = new Vector2(Mathf.Sign(inputDir.x), 0);
            }
            else
            {
                lastLookDirection = new Vector2(0, Mathf.Sign(inputDir.y));
            }

            animator.SetFloat("MoveX", lastLookDirection.x);
            animator.SetFloat("MoveY", lastLookDirection.y);
            animator.SetFloat("Speed", inputDir.sqrMagnitude);
        }
        else
        {
            // 멈췄을 때 방금 바라보던 방향 고정
            animator.SetFloat("MoveX", lastLookDirection.x);
            animator.SetFloat("MoveY", lastLookDirection.y);
            animator.SetFloat("Speed", 0f);
        }

        // 3. 대쉬 쿨타임 타이머 감소
        if (cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
        }

        // 4. 대쉬 중일 때 처리
        if (isDashing)
        {
            controller.Move(dashDirection * dashSpeed * Time.deltaTime);

            dashTimeLeft -= Time.deltaTime;
            if (dashTimeLeft <= 0)
            {
                isDashing = false;
            }
            return; // 대쉬 중일 때는 일반 이동 및 점프 입력을 스킵
        }

        // 5. 일반 이동 벡터 계산 및 대쉬 방향 저장
        moveDirection = new Vector3(inputX, 0f, inputZ).normalized;

        if (moveDirection != Vector3.zero)
        {
            lastDashDirection = moveDirection;
        }

        // 6. Q 키 대쉬 입력 확인
        if (Input.GetKeyDown(KeyCode.Q) && cooldownTimer <= 0)
        {
            isDashing = true;
            dashTimeLeft = dashDuration;
            cooldownTimer = dashCooldown;

            dashDirection = moveDirection != Vector3.zero ? moveDirection : lastDashDirection;
            return;
        }

        // 7. 점프 및 중력 처리
        if (controller.isGrounded)
        {
            verticalVelocity = -0.5f;
            if (Input.GetKeyDown(KeyCode.Space))
            {
                verticalVelocity = jumpForce;
            }
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;
        }

        // 8. 최종 일반 이동 적용
        Vector3 finalVelocity = (moveDirection * moveSpeed);
        finalVelocity.y = verticalVelocity;
        controller.Move(finalVelocity * Time.deltaTime);
    }
}