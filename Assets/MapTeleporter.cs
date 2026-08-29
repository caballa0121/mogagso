using System.Collections;
using UnityEngine;

public class MapTeleporter : MonoBehaviour
{
    [Header("목적지 설정")]
    public Transform targetSpawnPoint;

    [Header("시네머신 카메라 스위칭")]
    public GameObject currentMapCam; // vcam_Map1
    public GameObject targetMapCam;  // vcam_Map2

    [Header("페이드 연출 설정")]
    public CanvasGroup fadeCanvasGroup; // UI FadePanel의 CanvasGroup
    public float fadeDuration = 0.4f;   // 페이드 인/아웃 속도

    private bool isWarping = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isWarping)
        {
            StartCoroutine(WarpSequence(other.transform));
        }
    }

    private IEnumerator WarpSequence(Transform player)
{
    isWarping = true;

    // 1. 화면 암전 (Fade Out)
    float timer = 0f;
    while (timer < fadeDuration)
    {
        timer += Time.deltaTime;
        if (fadeCanvasGroup != null) 
            fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / fadeDuration);
        yield return null;
    }
    
    // [핵심 1] 확실하게 100% 암전 상태 고정
    if (fadeCanvasGroup != null) fadeCanvasGroup.alpha = 1f;

    // [핵심 2] 화면이 완전히 어두워진 상태에서 0.15초 대기
    yield return new WaitForSeconds(0.15f);

    // 2. 플레이어 위치 이동 및 물리 동기화
    Rigidbody rb = player.GetComponent<Rigidbody>();
    if (rb != null) rb.linearVelocity = Vector3.zero;

    Vector3 beforeWarp = player.position;

    player.position = targetSpawnPoint.position;
    Physics.SyncTransforms();

    // 3. 카메라 스위칭
    //    💥 vcam들이 전부 같은 Priority로 켜져 있어서 SetActive만으로는 전환이 보장되지 않습니다.
    //       CameraSwitcher가 목표 카메라의 우선순위를 확실히 올려줍니다.
    CameraSwitcher.SwitchTo(targetMapCam, currentMapCam);

    // 💥 순간이동임을 카메라에 알려서, Damping 때문에 천천히 따라오지 않게 합니다.
    CameraSwitcher.NotifyWarp(player, player.position - beforeWarp);

    // [핵심 3] 새 맵의 카메라 구도가 완전히 렌더링될 때까지 1프레임 대기
    yield return new WaitForEndOfFrame();
    yield return new WaitForSeconds(0.15f);

    // 4. 화면 밝아짐 (Fade In)
    timer = 0f;
    while (timer < fadeDuration)
    {
        timer += Time.deltaTime;
        if (fadeCanvasGroup != null) 
            fadeCanvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
        yield return null;
    }

    if (fadeCanvasGroup != null) fadeCanvasGroup.alpha = 0f;
    isWarping = false;
}
}
