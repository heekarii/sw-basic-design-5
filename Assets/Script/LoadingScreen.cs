using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

public class LoadingScreen : MonoBehaviour
{
    public Slider loadingBar;
    public TextMeshProUGUI loadingText;

    private string _targetScene;
    private LoadSceneMode _loadMode;

    void Start()
    {
        _targetScene = PlayerPrefs.GetString("LOAD_SCENE_NAME");
        _loadMode    = (LoadSceneMode)PlayerPrefs.GetInt("LOAD_SCENE_MODE", 0);

        StartCoroutine(LoadSceneProcess());
    }

    IEnumerator LoadSceneProcess()
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(_targetScene, _loadMode);
        op.allowSceneActivation = false;

        while (!op.isDone)
        {
            float progress = Mathf.Clamp01(op.progress / 0.9f);
            loadingBar.value = progress;
            loadingText.text = $"Loading.. {progress * 100:F0}%";

            if (op.progress >= 0.9f)
            {
                op.allowSceneActivation = true;

                Debug.Log($"[LoadingScreen] allowSceneActivation true -> target={_targetScene}, mode={_loadMode}");

                // 🔥 다음 프레임에서 Additive로 로드된 RepairShopUIscene을 Active로 설정
                // 한 프레임 이상 여유를 주어 Awake/Start/OnEnable이 실행되도록 합니다.
                yield return null;
                yield return null;
                Scene loadedScene = SceneManager.GetSceneByName(_targetScene);
                if (loadedScene.IsValid())
                {
                    SceneManager.SetActiveScene(loadedScene);
                    Debug.Log($"[LoadingScreen] SetActiveScene -> {_targetScene}");
                }

                // Map 씬을 비활성화해 Repair UI가 상호작용을 가로막지 않도록 한다.
                var mapScene = SceneManager.GetSceneByName("Map_SCENE");
                if (mapScene.IsValid())
                {
                    foreach (var root in mapScene.GetRootGameObjects())
                    {
                        try { root.SetActive(false); } catch { }
                    }
                    Debug.Log("[LoadingScreen] Map_SCENE의 루트 오브젝트들을 비활성화했습니다.");
                }

                // (선택) 로딩 씬 언로드
                SceneManager.UnloadSceneAsync("LoadingScene");
                Debug.Log("[LoadingScreen] Unload LoadingScene 호출");
            }


            yield return null;
        }
    }
}