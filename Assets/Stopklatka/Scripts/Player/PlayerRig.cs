using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// Finds the OVRCameraRig, exposes the head and controller anchors, and keeps
    /// the player's hitboxes (head sphere + torso capsule) on the tracked body.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class PlayerRig : MonoBehaviour
    {
        public static PlayerRig Instance { get; private set; }

        public OVRCameraRig CameraRig { get; private set; }
        public Transform Head => CameraRig != null ? CameraRig.centerEyeAnchor : transform;
        public Transform LeftController => CameraRig != null ? CameraRig.leftControllerAnchor : transform;
        public Transform RightController => CameraRig != null ? CameraRig.rightControllerAnchor : transform;

        /// <summary>Head position projected onto the floor (y = 0 with the floor-level tracking origin).</summary>
        public Vector3 FloorPosition
        {
            get { var p = Head.position; return new Vector3(p.x, FloorY, p.z); }
        }

        public float FloorY { get; set; }

        Transform _headBox, _torso;
        CapsuleCollider _torsoCollider;

        void Awake()
        {
            Instance = this;
            CameraRig = FindFirstObjectByType<OVRCameraRig>();
            if (CameraRig == null) Debug.LogError("[Stopklatka] No OVRCameraRig in the scene. Run Stopklatka > Build Game.");

            _headBox = new GameObject("PlayerHead").transform;
            _headBox.gameObject.layer = Layers.Player;
            var head = _headBox.gameObject.AddComponent<SphereCollider>();
            head.radius = 0.13f;
            head.isTrigger = true;

            _torso = new GameObject("PlayerTorso").transform;
            _torso.gameObject.layer = Layers.Player;
            _torsoCollider = _torso.gameObject.AddComponent<CapsuleCollider>();
            _torsoCollider.radius = 0.17f;
            _torsoCollider.direction = 1;
            _torsoCollider.isTrigger = true;

            // Kinematic bodies so trigger queries are cheap and stable.
            foreach (var t in new[] { _headBox, _torso })
            {
                var rb = t.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        void LateUpdate()
        {
            var head = Head;
            _headBox.SetPositionAndRotation(head.position, head.rotation);

            // Torso hangs below the head, upright, slightly behind the eyes.
            var flatForward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 1e-4f) flatForward = Vector3.forward;
            flatForward.Normalize();
            float headHeight = head.position.y - FloorY;
            float top = headHeight - 0.2f;
            float bottom = Mathf.Max(0.35f, headHeight * 0.45f);
            float height = Mathf.Max(0.4f, top - bottom);
            _torsoCollider.height = height;
            _torso.position = new Vector3(head.position.x, FloorY + bottom + height * 0.5f, head.position.z) - flatForward * 0.08f;
            _torso.rotation = Quaternion.LookRotation(flatForward, Vector3.up);
        }

        public bool IsPlayerCollider(Collider c) => c != null && (c.transform == _headBox || c.transform == _torso);
    }
}
