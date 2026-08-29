using System;

/// <summary>
/// 건반 입력을 전역에 알려주는 이벤트 버스.
/// 튜토리얼, 보스, 업적 등 누구든 구독만 하면 연주를 감지할 수 있습니다.
/// keyIndex = PianoManager.pianoKeys 리스트에서의 순서(0~23).
/// </summary>
public static class PianoEvents
{
    public static event Action<int> OnNotePlayed;

    public static void RaiseNote(int keyIndex)
    {
        OnNotePlayed?.Invoke(keyIndex);
    }
}

/// <summary>
/// 튜토리얼/보스가 피아노를 점유 중인지 표시합니다.
/// 자유 연주용 PianoInteract가 UI를 멋대로 여닫는 것을 막습니다.
/// </summary>
public static class PianoSession
{
    public static bool Busy = false;
}

/// <summary>
/// "정답 멜로디를 순서대로 눌렀는가"를 판정하는 클래스. (사이먼 게임 방식)
/// MonoBehaviour가 아니므로 new 로 만들어서 씁니다.
/// 반드시 Stop()으로 구독 해제할 것!
/// </summary>
public class MelodyChecker
{
    private int[] answer;
    private int index;
    private bool running;
    private bool failOnWrong;

    /// <summary>맞춘 음의 인덱스(0부터)를 넘겨줍니다.</summary>
    public event Action<int> OnCorrect;
    /// <summary>틀렸을 때, 기대했던 음의 인덱스를 넘겨줍니다.</summary>
    public event Action<int> OnWrong;
    /// <summary>멜로디를 끝까지 맞췄을 때.</summary>
    public event Action OnComplete;

    public bool Running => running;
    public int Progress => index;
    public int Length => answer != null ? answer.Length : 0;

    /// <param name="failOnWrong">true면 오답 시 즉시 실패(보스용), false면 진행도 유지(튜토리얼용)</param>
    public void Begin(MelodyData melody, bool failOnWrong = false)
    {
        if (melody == null) return;

        answer = melody.GetSequence();
        if (answer == null || answer.Length == 0) return;

        this.failOnWrong = failOnWrong;
        index = 0;
        running = true;

        PianoEvents.OnNotePlayed -= HandleNote; // 중복 구독 방지
        PianoEvents.OnNotePlayed += HandleNote;
    }

    public void Stop()
    {
        running = false;
        PianoEvents.OnNotePlayed -= HandleNote;
    }

    /// <summary>현재 눌러야 할 건반 인덱스. 끝났으면 -1.</summary>
    public int ExpectedKey()
    {
        if (answer == null || index >= answer.Length) return -1;
        return answer[index];
    }

    private void HandleNote(int keyIndex)
    {
        if (!running || answer == null) return;

        if (keyIndex == answer[index])
        {
            OnCorrect?.Invoke(index);
            index++;

            if (index >= answer.Length)
            {
                Stop();
                OnComplete?.Invoke();
            }
        }
        else
        {
            OnWrong?.Invoke(index);

            if (failOnWrong)
            {
                Stop(); // 연타로 오답이 여러 번 날아가는 것 방지
            }
            // 튜토리얼 모드에서는 index를 유지 -> 틀려도 진행도가 안 깎임
        }
    }
}
