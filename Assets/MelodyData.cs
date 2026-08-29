using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 튜토리얼과 보스가 공용으로 쓰는 "정답 멜로디" 데이터.
///
/// [건반 인덱스 표] PianoManager.pianoKeys 리스트 순서 = 낮은 음부터 반음 단위
///   0:도  1:도#  2:레  3:레#  4:미  5:파  6:파#  7:솔  8:솔#  9:라  10:라#  11:시
///  12:도(2옥타브) 13:도# ... 23:시(2옥타브)
///
/// noteSequence 예시 : "0,2,4,5,7"  ->  도 레 미 파 솔
/// </summary>
[CreateAssetMenu(fileName = "NewMelody", menuName = "Piano/Melody Data")]
public class MelodyData : ScriptableObject
{
    [Header("표시 정보")]
    public string melodyName = "새 멜로디";
    [TextArea(2, 3)]
    public string hint = "도-레-미 순서로 눌러보세요";

    [Header("음 배열 (쉼표로 구분, 0~23)")]
    [Tooltip("0:도 2:레 4:미 5:파 7:솔 9:라 11:시 / 12부터는 2옥타브")]
    public string noteSequence = "0,2,4";

    [Header("시연 재생 속도")]
    [Tooltip("한 음이 울리고 유지되는 시간(초)")]
    public float noteDuration = 0.35f;
    [Tooltip("다음 음까지의 간격(초)")]
    public float noteGap = 0.12f;

    private int[] cached;
    private string cachedSource;

    /// <summary>noteSequence 문자열을 int 배열로 변환합니다.</summary>
    public int[] GetSequence()
    {
        if (cached != null && cachedSource == noteSequence)
            return cached;

        List<int> list = new List<int>();

        if (!string.IsNullOrEmpty(noteSequence))
        {
            string[] parts = noteSequence.Split(new char[] { ',', ' ', '-' },
                                                System.StringSplitOptions.RemoveEmptyEntries);
            foreach (string p in parts)
            {
                int v;
                if (int.TryParse(p.Trim(), out v))
                {
                    list.Add(v);
                }
                else
                {
                    Debug.LogWarning($"[MelodyData:{name}] 숫자로 변환할 수 없는 값입니다: '{p}'");
                }
            }
        }

        cached = list.ToArray();
        cachedSource = noteSequence;
        return cached;
    }

    public int NoteCount => GetSequence().Length;

    private void OnValidate()
    {
        cached = null; // 인스펙터에서 수정하면 캐시 갱신
        if (noteDuration < 0.05f) noteDuration = 0.05f;
        if (noteGap < 0f) noteGap = 0f;
    }
}
