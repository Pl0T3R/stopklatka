using System;
using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// A floating crystal with a label. Shoot it, cut it, or touch it with a controller to activate.
    /// This is the whole menu: no laser pointers, no flat panels.
    /// </summary>
    public class MenuTarget : MonoBehaviour
    {
        public Action OnActivated;
        public Renderer Visual;
        TextMesh _label;
        Vector3 _base;
        bool _done;

        public void Setup(string label, Renderer visual, Action onActivated)
        {
            Visual = visual;
            OnActivated = onActivated;
            _base = transform.position;
            var go = new GameObject("Label");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.24f, 0f);
            go.transform.localScale = Vector3.one * 0.0028f;
            _label = go.AddComponent<TextMesh>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 120;
            _label.anchor = TextAnchor.LowerCenter;
            _label.alignment = TextAlignment.Center;
            _label.color = Color.white;
            _label.text = label;
            // shares the HUD material, which follows font texture rebuilds
            go.GetComponent<MeshRenderer>().sharedMaterial = Hud.Instance != null
                ? Hud.Instance.TextMaterial
                : new Material(Mats.Get(Mats.Text)) { mainTexture = _label.font.material.mainTexture };
        }

        public void SetLabel(string text) => _label.text = text;

        void Update()
        {
            float t = Time.unscaledTime;
            transform.position = _base + Vector3.up * Mathf.Sin(t * 1.6f) * 0.02f;
            Visual.transform.Rotate(Vector3.up, 40f * Time.unscaledDeltaTime, Space.World);

            var rig = PlayerRig.Instance;
            if (rig == null || _done) return;
            _label.transform.rotation = Quaternion.LookRotation(_label.transform.position - rig.Head.position, Vector3.up);
            var c = Visual.transform.position;
            if (Vector3.Distance(rig.LeftController.position, c) < 0.16f || Vector3.Distance(rig.RightController.position, c) < 0.16f)
                Activate(Vector3.up);
        }

        public void Activate(Vector3 direction)
        {
            if (_done) return;
            _done = true;
            ShardSystem.Burst(Visual.transform.position, Visual.sharedMaterial, 22, direction * 1.5f, 1.8f, 0.03f, 0.06f);
            SoundBank.Instance?.Play(Sfx.Shatter, Visual.transform.position, 0.9f, 1.2f, false);
            var callback = OnActivated;
            Destroy(gameObject);
            callback?.Invoke();
        }
    }
}
