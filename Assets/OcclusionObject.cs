using System.Collections;
using UnityEngine;

public class OcclusionObject : MonoBehaviour
{
    [Header("재질 설정")]
    public Material originalMaterial;
    public Material transparentMaterial;

    [Header("페이드 연출 설정")]
    [Tooltip("숫자가 클수록 건물이 빠르게 사라지고 나타납니다.")]
    public float fadeSpeed = 3f; 

    [Header("카메라 거리 감지 설정")]
    [Tooltip("카메라와의 거리 감지를 통해 건물을 사라지게 할지 여부입니다.")]
    public bool useCameraDistanceFade = true;

    [Tooltip("카메라와 건물의 거리가 이 값보다 가까워지면 건물이 사라집니다.")]
    public float fadeDistanceThreshold = 10f;

    private SpriteRenderer spriteRenderer;
    private Coroutine fadeCoroutine;
    private Camera mainCamera;

    private bool isPlayerInTrigger = false;
    private bool isHiddenByCamera = false;
    private float lastTargetOpacity = 1f;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        mainCamera = Camera.main; // Scene의 메인 카메라를 찾아옵니다.

        if (originalMaterial != null && spriteRenderer != null)
        {
            spriteRenderer.material = originalMaterial;
        }
    }

    void Update()
    {
        // 카메라 거리 감지 로직
        if (useCameraDistanceFade && mainCamera != null)
        {
            // 메인 카메라와 건물 사이의 3D 거리를 계산합니다.
            float distanceToCamera = Vector3.Distance(mainCamera.transform.position, transform.position);

            // 지정한 기준 거리보다 카메라가 가까워졌을 때
            if (distanceToCamera < fadeDistanceThreshold && !isHiddenByCamera)
            {
                isHiddenByCamera = true;
                UpdateOcclusionState();
            }
            // 지정한 기준 거리보다 카메라가 멀어졌을 때
            else if (distanceToCamera >= fadeDistanceThreshold && isHiddenByCamera)
            {
                isHiddenByCamera = false;
                UpdateOcclusionState();
            }
        }
    }

    // 플레이어 트리거 진입 감지
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = true;
            UpdateOcclusionState();
        }
    }

    // 플레이어 트리거 이탈 감지
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = false;
            UpdateOcclusionState();
        }
    }

    // 조건(플레이어가 뒤에 있음 OR 카메라인근에 있음)에 따라 최종 상태 결정
    private void UpdateOcclusionState()
    {
        // 둘 중 하나라도 조건에 해당하면 사라짐(0), 둘 다 안 해당하면 나타남(1)
        float targetOpacity = (isPlayerInTrigger || isHiddenByCamera) ? 0f : 1f;

        // 목표 투명도가 달라졌을 때만 페이드 코루틴 실행
        if (!Mathf.Approximately(lastTargetOpacity, targetOpacity))
        {
            lastTargetOpacity = targetOpacity;
            StartFade(targetOpacity);
        }
    }

    private void StartFade(float targetOpacity)
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        fadeCoroutine = StartCoroutine(FadeRoutine(targetOpacity));
    }

    private IEnumerator FadeRoutine(float targetOpacity)
    {
        if (spriteRenderer == null || transparentMaterial == null) yield break;

        if (spriteRenderer.material != transparentMaterial)
        {
            spriteRenderer.material = transparentMaterial;

            if (targetOpacity == 0f)
            {
                spriteRenderer.material.SetFloat("_Opacity", 1f);
            }
        }

        float currentOpacity = spriteRenderer.material.GetFloat("_Opacity");

        while (!Mathf.Approximately(currentOpacity, targetOpacity))
        {
            currentOpacity = Mathf.MoveTowards(currentOpacity, targetOpacity, fadeSpeed * Time.deltaTime);
            spriteRenderer.material.SetFloat("_Opacity", currentOpacity);
            yield return null;
        }

        if (targetOpacity >= 1f && originalMaterial != null)
        {
            spriteRenderer.material = originalMaterial;
        }
    }
}