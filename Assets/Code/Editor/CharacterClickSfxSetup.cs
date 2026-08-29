using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 캐릭터 프리팹들의 '의도 버튼 클릭음'을 한 번에 넣어 주는 도구.
///
/// 캐릭터 프리팹의 버튼은 Button이 아니라 EventTrigger로 연결돼 있어서
/// 일반적인 버튼 도구로는 잡히지 않습니다.
/// 대신 BattleCharacter의 Button Click Sfx 칸을 직접 채웁니다.
/// </summary>
public static class CharacterClickSfxSetup
{
    // 프로젝트 창에서 클립을 고르지 않았을 때 쓸 기본 소리
    private const string DefaultClipPath = "Assets/image/sound/520579__divoljud__clickglass.wav";

    [MenuItem("Tools/사운드/모든 캐릭터 프리팹에 버튼 클릭음 넣기", false, 20)]
    public static void ApplyToAllCharacterPrefabs()
    {
        AudioClip clip = ResolveClip();
        if (clip == null)
        {
            EditorUtility.DisplayDialog("캐릭터 클릭음",
                "넣을 소리를 찾지 못했습니다.\n\n" +
                "프로젝트 창에서 오디오 클립을 하나 선택한 뒤 다시 누르거나,\n" +
                DefaultClipPath + " 가 있는지 확인해 주세요.", "확인");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        var changed = new List<string>();
        var skipped = new List<string>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) continue;

            try
            {
                var characters = root.GetComponentsInChildren<BattleCharacter>(true);
                if (characters.Length == 0) continue;

                bool dirty = false;
                foreach (var c in characters)
                {
                    if (c.buttonClickSfx == null) c.buttonClickSfx = new SoundCue();

                    if (c.buttonClickSfx.clip == clip) { continue; }

                    c.buttonClickSfx.clip = clip;
                    dirty = true;
                }

                if (dirty)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed.Add(System.IO.Path.GetFileNameWithoutExtension(path));
                }
                else
                {
                    skipped.Add(System.IO.Path.GetFileNameWithoutExtension(path));
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (changed.Count == 0 && skipped.Count == 0)
        {
            EditorUtility.DisplayDialog("캐릭터 클릭음",
                "BattleCharacter가 붙은 프리팹을 찾지 못했습니다.", "확인");
            return;
        }

        string msg = $"소리 : {clip.name}\n\n새로 넣음 : {changed.Count}개\n";
        foreach (var n in changed) msg += "  · " + n + "\n";

        if (skipped.Count > 0)
        {
            msg += $"\n이미 같은 소리라 건너뜀 : {skipped.Count}개\n";
            foreach (var n in skipped) msg += "  · " + n + "\n";
        }

        msg += "\n음량·음높이는 프리팹의 BattleCharacter → Button Click Sfx 에서 조절하세요.";

        Debug.Log("[캐릭터 클릭음] " + msg.Replace("\n", " / "));
        EditorUtility.DisplayDialog("캐릭터 클릭음", msg, "확인");
    }

    /// <summary>프로젝트 창에서 고른 클립이 있으면 그걸, 없으면 기본 클립을 씁니다.</summary>
    private static AudioClip ResolveClip()
    {
        foreach (Object o in Selection.GetFiltered(typeof(AudioClip), SelectionMode.Assets))
        {
            if (o is AudioClip picked) return picked;
        }

        return AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultClipPath);
    }
}
