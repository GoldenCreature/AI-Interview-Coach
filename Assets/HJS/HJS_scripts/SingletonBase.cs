using UnityEngine;

namespace HJS
{
    // 모든 매니저가 상속받을 싱글톤 기반 클래스
    // T는 상속받는 매니저 클래스 자신을 넣으면 됨
    // 예: public class GameManager : SingletonBase<GameManager>
    public abstract class SingletonBase<T> : MonoBehaviour where T : MonoBehaviour
    {
        // 싱글톤 인스턴스
        // 어디서든 GameManager.Instance 로 접근 가능
        private static T _instance;

        public static T Instance
        {
            get
            {
                // 인스턴스가 없으면 씬에서 찾아봄
                if (_instance == null)
                {
                    _instance = FindObjectOfType<T>();

                    // 씬에도 없으면 새로 만듦
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject(typeof(T).Name);
                        _instance = obj.AddComponent<T>();
                    }
                }
                return _instance;
            }
        }

        protected virtual void Awake()
        {
            // 이미 인스턴스가 있는데 또 생성되면 삭제
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[{typeof(T).Name}] 중복 인스턴스 삭제");
                Destroy(gameObject);
                return;
            }

            _instance = this as T;

            // 씬이 바뀌어도 삭제되지 않게 설정
            DontDestroyOnLoad(gameObject);
        }
    }
}