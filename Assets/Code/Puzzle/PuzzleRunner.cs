using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 퍼즐 씬을 지금 씬 '위에 덧씌워' 띄웠다가 걷어내는 진행자.
///
/// 씬을 갈아엎지 않고 겹쳐 올리기 때문에, 밑에 있는 챕터 씬은
/// 배경/캐릭터 위치/이미 지나간 대사까지 전부 그대로 살아 있습니다.
/// 그래서 퍼즐이 끝나면 AutoDirector가 다음 스텝부터 자연스럽게 이어집니다.
///
/// 쓰는 법 (코루틴 안에서):
///     yield return StartCoroutine(PuzzleRunner.Play(definition));
/// </summary>
public static class PuzzleRunner
{
    public const string PuzzleSceneName = "PuzzleScene";

    /// <summary>퍼즐 씬이 열릴 때 어떤 퍼즐을 풀어야 하는지 알려주는 창구.</summary>
    public static PuzzleDefinition PendingDefinition { get; private set; }

    /// <summary>지금 퍼즐이 돌아가는 중인지.</summary>
    public static bool IsRunning { get; private set; }

    private static bool finished;

    public static IEnumerator Play(PuzzleDefinition definition)
    {
        if (definition == null)
        {
            Debug.LogWarning("[Puzzle] 퍼즐 정의가 비어 있어 건너뜁니다.");
            yield break;
        }

        if (IsRunning)
        {
            Debug.LogWarning("[Puzzle] 이미 퍼즐이 진행 중이라 새 퍼즐을 건너뜁니다.");
            yield break;
        }

        if (!Application.CanStreamedLevelBeLoaded(PuzzleSceneName))
        {
            Debug.LogError($"[Puzzle] '{PuzzleSceneName}' 씬을 찾을 수 없습니다. " +
                           "Tools → 퍼즐 → [퍼즐 씬 만들기]로 씬을 만들고 Build Settings에 등록해 주세요. 퍼즐을 건너뜁니다.");
            yield break;
        }

        PendingDefinition = definition;
        IsRunning = true;
        finished = false;

        // 1. 지금 씬 위에 덧씌우기
        AsyncOperation load = SceneManager.LoadSceneAsync(PuzzleSceneName, LoadSceneMode.Additive);
        if (load == null)
        {
            Debug.LogError("[Puzzle] 퍼즐 씬을 여는 데 실패했습니다. 퍼즐을 건너뜁니다.");
            Cleanup();
            yield break;
        }

        while (!load.isDone) yield return null;

        // 2. PuzzleManager가 다 끝났다고 알려줄 때까지 대기
        while (!finished)
        {
            // 퍼즐 씬이 어떤 이유로든 사라졌다면 영원히 기다리지 않고 빠져나옵니다.
            Scene alive = SceneManager.GetSceneByName(PuzzleSceneName);
            if (!alive.IsValid() || !alive.isLoaded)
            {
                Debug.LogWarning("[Puzzle] 퍼즐 씬이 먼저 닫혀서 대기를 멈춥니다.");
                break;
            }
            yield return null;
        }

        // 3. 걷어내기
        Scene scene = SceneManager.GetSceneByName(PuzzleSceneName);
        if (scene.IsValid() && scene.isLoaded)
        {
            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            while (unload != null && !unload.isDone) yield return null;
        }

        Cleanup();
    }

    /// <summary>퍼즐 씬의 PuzzleManager가 다 끝났을 때 불러줍니다.</summary>
    public static void NotifyFinished()
    {
        finished = true;
    }

    private static void Cleanup()
    {
        PendingDefinition = null;
        IsRunning = false;
        finished = false;
    }
}
