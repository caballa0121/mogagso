using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// 시네머신 가상 카메라를 확실하게 바꿔주는 도우미.
///
/// 이 씬의 가상 카메라들은 전부 같은 Priority(10)로 켜져 있습니다.
/// 그 상태에서는 GameObject를 SetActive 하는 것만으로 어느 카메라가 화면을 잡을지 정해지지 않습니다.
///
///  · 우선순위가 같으면 Cinemachine이 등록 순서로 아무거나 하나를 고릅니다.
///  · 이미 켜져 있는 카메라에 SetActive(true)를 불러봐야 아무 일도 일어나지 않습니다.
///
/// 그래서 목표 카메라의 Priority를 살아있는 다른 카메라들보다 확실히 높여서
/// 반드시 그 카메라가 화면을 잡도록 만듭니다.
/// </summary>
public static class CameraSwitcher
{
    /// <summary>
    /// previous를 끄고 target을 켠 뒤, target이 반드시 화면을 잡도록 우선순위를 올립니다.
    /// </summary>
    public static void SwitchTo(GameObject target, GameObject previous = null)
    {
        if (previous != null && previous != target)
        {
            previous.SetActive(false);
        }

        if (target == null) return;

        target.SetActive(true);

        var targetCam = target.GetComponentInChildren<CinemachineVirtualCameraBase>(true);
        if (targetCam == null)
        {
            Debug.LogWarning($"[CameraSwitcher] '{target.name}' 에 시네머신 카메라가 없습니다. " +
                             "GameObject만 켜고 넘어갑니다.");
            return;
        }

        // 지금 살아있는 카메라들 중 가장 높은 우선순위를 찾습니다.
        int highest = targetCam.Priority;

        var all = Object.FindObjectsByType<CinemachineVirtualCameraBase>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (var cam in all)
        {
            if (cam == null || cam == targetCam) continue;

            int p = cam.Priority;
            if (p > highest) highest = p;
        }

        // 동점이면 못 이기므로 한 칸 위로 올려 둡니다.
        int mine = targetCam.Priority;
        if (mine <= highest)
        {
            targetCam.Priority = highest + 1;
        }
    }

    /// <summary>
    /// 순간이동처럼 대상이 확 건너뛴 경우, 카메라가 천천히 따라오지 않고
    /// 같은 거리만큼 즉시 함께 점프하도록 알려줍니다.
    /// </summary>
    public static void NotifyWarp(Transform target, Vector3 positionDelta)
    {
        if (target == null) return;
        if (positionDelta.sqrMagnitude < 0.0001f) return;

        CinemachineCore.OnTargetObjectWarped(target, positionDelta);
    }
}
