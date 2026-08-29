using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 씬 안의 버튼들에 효과음 컴포넌트(UIButtonSfx)를 한 번에 붙여 주는 도구.
///
/// 붙이기만 하고 클립은 비워둡니다.
/// 클립은 버튼마다 다르게 넣으셔도 되고, 하나를 설정한 뒤 복사해 붙이셔도 됩니다.
/// </summary>
public static class ButtonSfxSetup
{
    [MenuItem("Tools/사운드/씬의 모든 버튼에 효과음 컴포넌트 붙이기", false, 10)]
    public static void AddToAllButtons()
    {
        var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (buttons.Length == 0)
        {
            EditorUtility.DisplayDialog("버튼 효과음", "열려 있는 씬에서 Button을 찾지 못했습니다.", "확인");
            return;
        }

        var added = new List<string>();
        int skipped = 0;

        foreach (var b in buttons)
        {
            if (b.GetComponent<UIButtonSfx>() != null) { skipped++; continue; }

            Undo.AddComponent<UIButtonSfx>(b.gameObject);
            added.Add(b.gameObject.name);
        }

        if (added.Count > 0)
        {
            EditorSceneManager.MarkSceneDirty(buttons[0].gameObject.scene);
        }

        string msg = $"버튼 {buttons.Length}개를 찾았습니다.\n\n" +
                     $"새로 붙임 : {added.Count}개\n" +
                     $"이미 있어서 건너뜀 : {skipped}개\n\n";

        if (added.Count > 0)
        {
            msg += "붙인 버튼:\n";
            for (int i = 0; i < added.Count && i < 12; i++) msg += "  · " + added[i] + "\n";
            if (added.Count > 12) msg += $"  … 외 {added.Count - 12}개\n";
            msg += "\n각 버튼의 '버튼 효과음' 항목에 클립을 넣고 Ctrl+S 하세요.";
        }

        EditorUtility.DisplayDialog("버튼 효과음", msg, "확인");
    }

    [MenuItem("Tools/사운드/선택한 오브젝트에만 효과음 컴포넌트 붙이기", false, 11)]
    public static void AddToSelection()
    {
        var targets = Selection.gameObjects;
        if (targets == null || targets.Length == 0)
        {
            EditorUtility.DisplayDialog("버튼 효과음", "하이어라키에서 버튼을 먼저 선택해 주세요.", "확인");
            return;
        }

        int count = 0;
        foreach (var go in targets)
        {
            if (go.GetComponent<UIButtonSfx>() != null) continue;

            Undo.AddComponent<UIButtonSfx>(go);
            count++;
        }

        if (count > 0) EditorSceneManager.MarkSceneDirty(targets[0].scene);

        EditorUtility.DisplayDialog("버튼 효과음", count + "개에 붙였습니다.", "확인");
    }
}
