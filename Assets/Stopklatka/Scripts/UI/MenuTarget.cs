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
        TextMesh _label, _labelShadow;
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
            _label = MakeText(go, label, Color.white, 1);
            // dark drop shadow behind the label, readable over a white room
            var shadow = new GameObject("Label Shadow");
            shadow.transform.SetParent(go.transform, false);
            shadow.transform.localPosition = new Vector3(0.8f, -0.8f, 1f);
            _labelShadow = MakeText(shadow, label, new Color(0.04f, 0.04f, 0.06f, 0.6f), 0);
        }

        static TextMesh MakeText(GameObject go, string text, Color color, int order)
        {
            var tm = go.AddComponent<TextMesh>();
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.fontSize = 120;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.text = text;
            var r = go.GetComponent<MeshRenderer>();
            r.sortingOrder = order;
            // shares the HUD material, which follows font texture rebuilds
            r.sharedMaterial = Hud.Instance != null
                ? Hud.Instance.TextMaterial
                : new Material(Mats.Get(Mats.Text)) { mainTexture = tm.font.material.mainTexture };
            return tm;
        }

        public void SetLabel(string text)
        {
            _label.text = text;
            _labelShadow.text = text;
        }

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
