using UnityEngine;

namespace Stopklatka
{
    /// <summary>Short-lived flashes. They live in game time, so a muzzle flash lingers in slow motion.</summary>
    public class Flash : MonoBehaviour
    {
        float _until, _size;

        public void Show(float size, float seconds)
        {
            _size = size;
            _until = Time.time + seconds;
            transform.localScale = Vector3.one * size;
        }

        void Update()
        {
            float left = _until - Time.time;
            if (left <= 0f) { Effects.Release(this); return; }
            transform.localScale = Vector3.one * (_size * (0.6f + 0.4f * Random.value));
        }
    }

    public static class Effects
    {
        static Pool<Flash> _flashes;
        static Mesh _flashMesh;

        static Pool<Flash> Flashes => _flashes ??= new Pool<Flash>("Flashes", CreateFlash, 8);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _flashes = null; }

        public static void MuzzleFlash(Vector3 position, Quaternion rotation)
        {
            var f = Flashes.Get(position + rotation * Vector3.forward * 0.03f, rotation * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            f.Show(0.07f, 0.05f);
        }

        public static void Impact(Vector3 point, Vector3 normal)
        {
            var f = Flashes.Get(point + normal * 0.01f, Quaternion.LookRotation(normal));
            f.Show(0.04f, 0.04f);
            ShardSystem.Burst(point + normal * 0.02f, Mats.Get(Mats.Weapon), 3, normal * 1.2f, 0.8f, 0.008f, 0.016f);
        }

        public static void Release(Flash f) => Flashes.Release(f);

        public static void ClearAll() => _flashes?.ReleaseAll();

        static Flash CreateFlash()
        {
            if (_flashMesh == null)
            {
                var b = new MeshKit.Builder();
                b.Ico(Vector3.zero, new Vector3(0.5f, 0.5f, 0.9f), 0.25f, new System.Random(5));
                _flashMesh = b.ToMesh("Flash");
            }
            var go = MeshKit.Part("Flash", null, _flashMesh, Mats.Get(Mats.Flash));
            return go.AddComponent<Flash>();
        }
    }
}
