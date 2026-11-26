using UnityEngine;

namespace MyCity.Diagnostics
{
    public sealed class RuntimeDiagnostics : MonoBehaviour
    {
        public float FramesPerSecond { get; private set; }
        public float FrameTimeMilliseconds { get; private set; }
        private int frameCount;
        private float elapsed;

        private void Update()
        {
            frameCount++;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed < 1f) return;
            FramesPerSecond = frameCount / elapsed;
            FrameTimeMilliseconds = 1000f / Mathf.Max(1f, FramesPerSecond);
            frameCount = 0;
            elapsed = 0f;
        }
    }
}
