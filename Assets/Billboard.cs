using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void LateUpdate()
    {
        // 스프라이트가 항상 Mac 화면(카메라)을 똑바로 바라보게 만듭니다.
        transform.rotation = mainCamera.transform.rotation;
    }
}