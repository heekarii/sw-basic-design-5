using UnityEngine;

public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;

    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                // 씬에 있는 모든 인스턴스를 찾아서 DontDestroyOnLoad에 있는(영구화된) 인스턴스를 우선적으로 선택
                var all = FindObjectsOfType<T>();
                if (all != null && all.Length > 0)
                {
                    foreach (var o in all)
                    {
                        if (o.gameObject.scene.name == "DontDestroyOnLoad")
                        {
                            _instance = o;
                            break;
                        }
                    }

                    if (_instance == null)
                    {
                        // DontDestroyOnLoad에 없는 경우 첫 번째를 사용
                        _instance = all[0];
                    }
                }

                if (_instance == null)
                {
                    var obj = new GameObject(typeof(T).Name);
                    _instance = obj.AddComponent<T>();
                    // 새로 생성한 인스턴스는 영구화
                    UnityEngine.Object.DontDestroyOnLoad(obj);
                }
            }
            return _instance;
        }
    }

    protected virtual void Awake()
    {
        // 중복 인스턴스 방지
        if (_instance == null)
        {
            _instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }
}