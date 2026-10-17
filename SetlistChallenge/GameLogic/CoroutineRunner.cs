using System.Collections;
using UnityEngine;

namespace SetlistChallenge.GameLogic
{
    /// <summary>1.44.1 里没有 SharedCoroutineStarter 了，自己挂一个跑协程的 MonoBehaviour</summary>
    public class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner _instance;

        public static CoroutineRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("SetlistChallenge_Coroutines");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<CoroutineRunner>();
                }
                return _instance;
            }
        }

        public static Coroutine Run(IEnumerator routine) => Instance.StartCoroutine(routine);
    }
}
