using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 열려 있는 씬에 전투 효과음 재생기(BattleSfx)를 넣어 주는 도구.
///
/// 씬 파일을 손으로 고치지 않고 메뉴 한 번으로 추가합니다.
/// 이미 있으면 새로 만들지 않고 그것을 골라줍니다.
/// </summary>
public static class BattleSfxSetup
{
    [MenuItem("Tools/전투/효과음 재생기 추가", false, 10)]
    public static void AddToOpenScene()
    {
        var existing = Object.FindAnyObjectByType<BattleSfx>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);

            EditorUtility.DisplayDialog("전투 효과음",
                "이 씬에는 이미 효과음 재생기가 있습니다.\n\n" +
                "'" + existing.gameObject.name + "' 을(를) 골라 뒀습니다.\n" +
                "인스펙터에서 클립을 넣어주세요.", "확인");
            return;
        }

        var go = new GameObject("BattleSfx");

        var audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.loop = false;
        audio.spatialBlend = 0f; // 위치에 상관없이 항상 같은 크기로 들리게

        var sfx = go.AddComponent<BattleSfx>();
        sfx.source = audio;

        Undo.RegisterCreatedObjectUndo(go, "전투 효과음 재생기 추가");
        EditorSceneManager.MarkSceneDirty(go.scene);

        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);

        EditorUtility.DisplayDialog("전투 효과음",
            "'BattleSfx' 오브젝트를 만들었습니다.\n\n" +
            "인스펙터에서 소리별로 클립을 넣어주세요.\n" +
            "· 타격 (Hit)\n" +
            "· 버튼 클릭 (Button Click)\n" +
            "· 나머지(방어/회피/반격/쓰러짐/마우스 올림)는 비워두면 소리가 안 납니다.\n\n" +
            "다 넣으신 뒤 Ctrl+S로 씬을 저장해 주세요.", "확인");
    }
}
