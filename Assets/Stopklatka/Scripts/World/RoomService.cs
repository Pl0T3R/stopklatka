using System.Threading.Tasks;
using Meta.XR.MRUtilityKit;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace Stopklatka
{
    /// <summary>
    /// Loads every room from Space Setup through MRUK. The scene's EffectMesh turns walls, floor and
    /// furniture into colliders (they stop bullets), and SceneNavigation bakes a NavMesh across all
    /// rooms so enemies walk around real desks. Also answers "is this point inside the scan?" and
    /// "where can an enemy appear?".
    /// </summary>
    public class RoomService : MonoBehaviour
    {
        public static RoomService Instance { get; private set; }

        public bool Ready { get; private set; }
        public bool HasScan { get; private set; }
        public bool NavReady { get; private set; }
        public int NavAgentTypeId { get; private set; }
        public string Problem { get; private set; }

        SceneNavigation _nav;
        Transform _fallbackFloor;

        void Awake() => Instance = this;

        async void Start()
        {
            _nav = FindFirstObjectByType<SceneNavigation>();
            if (_nav != null) _nav.OnNavMeshInitialized.AddListener(OnNavMeshReady);

            if (!await EnsureScenePermission())
            {
                Problem = "Brak zgody na dane o pomieszczeniu. Włącz ją w Ustawieniach > Prywatność.";
                CreateFallbackFloor();
                Ready = true;
                return;
            }

            var mruk = MRUK.Instance;
            if (mruk == null)
            {
                Problem = "Brak MRUK w scenie. Uruchom w Unity: Stopklatka > Build Game.";
                CreateFallbackFloor();
                Ready = true;
                return;
            }

            var result = await mruk.LoadSceneFromDevice(requestSceneCaptureIfNoDataFound: true);
            HasScan = mruk.Rooms.Count > 0;
            Debug.Log($"[Stopklatka] Scene load: {result}, rooms: {mruk.Rooms.Count}");
            if (!HasScan) Problem = "Nie znaleziono skanu. Zrób Space Setup tam, gdzie grasz (Ustawienia > Przestrzeń fizyczna).";

            if (HasScan)
            {
                UpdateFloor(mruk.GetCurrentRoom() ?? mruk.Rooms[0]);

                float until = Time.realtimeSinceStartup + 6f;
                while (!NavReady && Time.realtimeSinceStartup < until) await Task.Yield();
                if (!NavReady) Debug.LogWarning("[Stopklatka] NavMesh not ready, enemies will walk in straight lines.");
            }
            else CreateFallbackFloor();
            Ready = true;
        }

        static readonly int FloorYId = Shader.PropertyToID("_StopklatkaFloorY");

        void Update()
        {
            // Houses have floors: follow the floor height of the room you are in.
            if (HasScan && MRUK.Instance != null) UpdateFloor(MRUK.Instance.GetCurrentRoom());
        }

        void UpdateFloor(MRUKRoom room)
        {
            var floor = FloorOf(room);
            if (floor == null || PlayerRig.Instance == null) return;
            float y = floor.transform.position.y;
            PlayerRig.Instance.FloorY = y;
            Shader.SetGlobalFloat(FloorYId, y);
        }

        /// <summary>The room's (first) floor anchor. MRUK 207 marks the single FloorAnchor property obsolete.</summary>
        static MRUKAnchor FloorOf(MRUKRoom room) =>
            room != null && room.FloorAnchors.Count > 0 ? room.FloorAnchors[0] : null;

        void OnNavMeshReady()
        {
            var surface = _nav != null ? _nav.GetComponent<NavMeshSurface>() : null;
            NavAgentTypeId = surface != null ? surface.agentTypeID : 0;
            NavReady = true;
            Debug.Log($"[Stopklatka] NavMesh ready, agent type {NavAgentTypeId}");
        }

        /// <summary>
        /// Without a scan nothing stops thrown things from falling forever: one big invisible floor
        /// on the room layer at the tracked floor height (y = 0 with floor-level tracking).
        /// The time test scene has its own floor and no RoomService, so there is never a second one.
        /// </summary>
        void CreateFallbackFloor()
        {
            if (_fallbackFloor != null) return;
            var go = new GameObject("FallbackFloor") { layer = Layers.Room };
            const float thickness = 0.2f;
            float floorY = PlayerRig.Instance != null ? PlayerRig.Instance.FloorY : 0f;
            var center = PlayerRig.Instance != null ? PlayerRig.Instance.FloorPosition : Vector3.zero;
            go.transform.position = new Vector3(center.x, floorY - thickness * 0.5f, center.z);
            go.AddComponent<BoxCollider>().size = new Vector3(40f, thickness, 40f);
            _fallbackFloor = go.transform;
            Debug.Log($"[Stopklatka] No room scan: fallback floor at y = {floorY:F2}");
        }

        static async Task<bool> EnsureScenePermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            const string perm = OVRPermissionsRequester.ScenePermission;
            if (Permission.HasUserAuthorizedPermission(perm)) return true;
            var done = new TaskCompletionSource<bool>();
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => done.TrySetResult(true);
            callbacks.PermissionDenied += _ => done.TrySetResult(false);
            Permission.RequestUserPermission(perm, callbacks);
            return await done.Task;
#else
            await Task.Yield();
            return true;
#endif
        }

        /// <summary>True when the point is inside one of the scanned rooms (always true without a scan).</summary>
        public bool IsInsideScan(Vector3 point)
        {
            if (!HasScan || MRUK.Instance == null) return true;
            foreach (var room in MRUK.Instance.Rooms)
                if (room.IsPositionInRoom(point, false)) return true;
            return false;
        }

        MRUKRoom RoomAt(Vector3 point)
        {
            foreach (var room in MRUK.Instance.Rooms)
                if (room.IsPositionInRoom(point, false)) return room;
            return null;
        }

        NavMeshPath _path;   // created on first use (not in a field initializer: it's a native object)

        /// <summary>
        /// Finds a floor spot for an enemy: inside a scanned room, not inside furniture, between
        /// the min and max distance from the player, and connected to the player by the NavMesh.
        /// </summary>
        public bool TryFindEnemySpawn(Vector3 playerFloor, out Vector3 position) =>
            TryFindEnemySpawn(playerFloor, Vector3.zero, out position);

        /// <summary>
        /// Like above, but enemies "walk in": often at a scanned door frame on your floor
        /// (GameConfig enemies.doorSpawnChance), otherwise sometimes out of your view
        /// (outOfViewChance; viewDir is where you look, zero for no preference). The distance limits
        /// and "inside a scanned room" always hold; a second pass with a shorter minimum (never under
        /// 1.2 m) is for tiny rooms.
        /// </summary>
        public bool TryFindEnemySpawn(Vector3 playerFloor, Vector3 viewDir, out Vector3 position)
        {
            float min = Tuning.EnemyMinSpawnDistance;
            if (HasScan && Random.value < Tuning.DoorSpawnChance && TryDoorSpawn(playerFloor, min, out position)) return true;
            var flatView = new Vector3(viewDir.x, 0f, viewDir.z);
            if (flatView.sqrMagnitude > 1e-4f && Random.value < Tuning.OutOfViewChance &&
                TryFindEnemySpawn(playerFloor, min, flatView.normalized, out position)) return true;
            return TryFindEnemySpawn(playerFloor, min, Vector3.zero, out position) ||
                   TryFindEnemySpawn(playerFloor, Mathf.Max(1.2f, min * 0.6f), Vector3.zero, out position);
        }

        bool TryFindEnemySpawn(Vector3 playerFloor, float minDistance, Vector3 avoidView, out Vector3 position)
        {
            position = default;
            var playerRoom = HasScan ? RoomAt(playerFloor + Vector3.up) : null;
            float minAngle = Tuning.OutOfViewAngle;

            for (int attempt = 0; attempt < 40; attempt++)
            {
                Vector3 candidate;
                if (HasScan)
                {
                    var rooms = MRUK.Instance.Rooms;
                    var room = playerRoom != null && Random.value < GameMode.Profile.PlayerRoomChance ? playerRoom : rooms[Random.Range(0, rooms.Count)];
                    if (!room.GenerateRandomPositionOnSurface(MRUK.SurfaceType.FACING_UP, 0.35f,
                            LabelFilter.Included(MRUKAnchor.SceneLabels.FLOOR), out candidate, out _))
                        continue;
                    if (room.IsPositionInSceneVolume(candidate + Vector3.up * 0.4f, 0.25f)) continue;
                    if (Mathf.Abs(candidate.y - playerFloor.y) > 0.6f) continue;   // stay on the player's floor
                }
                else
                {
                    var dir = Random.insideUnitCircle.normalized;
                    candidate = playerFloor + new Vector3(dir.x, 0f, dir.y) * Random.Range(minDistance, Mathf.Max(minDistance + 0.5f, 5f));
                }

                if (avoidView != Vector3.zero)
                {
                    var to = new Vector3(candidate.x - playerFloor.x, 0f, candidate.z - playerFloor.z);
                    if (Vector3.Angle(avoidView, to) < minAngle) continue;
                }
                if (AcceptSpawn(ref candidate, playerFloor, minDistance))
                {
                    position = candidate;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// A spot just inside a scanned door frame on the player's floor, as if the enemy walked in
        /// through it. Both sides of each frame are tried; only a side inside a scanned room counts.
        /// </summary>
        bool TryDoorSpawn(Vector3 playerFloor, float minDistance, out Vector3 position)
        {
            position = default;
            if (MRUK.Instance == null) return false;
            float offset = Mathf.Max(0.3f, Tuning.DoorSpawnOffset);
            int found = 0;
            foreach (var room in MRUK.Instance.Rooms)
            {
                var floor = FloorOf(room);
                if (floor == null) continue;
                float floorY = floor.transform.position.y;
                if (Mathf.Abs(floorY - playerFloor.y) > 0.6f) continue;   // stairs and other floors: no
                foreach (var anchor in room.Anchors)
                {
                    if (anchor == null || !anchor.HasAnyLabel(MRUKAnchor.SceneLabels.DOOR_FRAME)) continue;
                    // a door frame is a plane on a wall; its normal (Z) is horizontal
                    var normal = Vector3.ProjectOnPlane(anchor.transform.forward, Vector3.up);
                    if (normal.sqrMagnitude < 0.25f) continue;
                    normal.Normalize();
                    var foot = anchor.transform.position;
                    foot.y = floorY;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var candidate = foot + normal * (side * offset);
                        var inRoom = RoomAt(candidate + Vector3.up);
                        if (inRoom == null || inRoom.IsPositionInSceneVolume(candidate + Vector3.up * 0.4f, 0.2f)) continue;
                        if (!AcceptSpawn(ref candidate, playerFloor, minDistance)) continue;
                        // pick one of the valid spots at random (reservoir sampling, no list)
                        found++;
                        if (Random.Range(0, found) == 0) position = candidate;
                    }
                }
            }
            return found > 0;
        }

        /// <summary>Distance limits, and with a NavMesh: on it and connected to the player.</summary>
        bool AcceptSpawn(ref Vector3 candidate, Vector3 playerFloor, float minDistance)
        {
            float d = Vector3.Distance(new Vector3(candidate.x, 0f, candidate.z), new Vector3(playerFloor.x, 0f, playerFloor.z));
            if (d < minDistance || d > Tuning.EnemyMaxSpawnDistance) return false;
            if (!NavReady) return true;

            var filter = new NavMeshQueryFilter { agentTypeID = NavAgentTypeId, areaMask = NavMesh.AllAreas };
            if (!NavMesh.SamplePosition(candidate, out var hit, 0.6f, filter)) return false;
            // the sample may slide the spot: check the distance again on the NavMesh position
            float dn = Vector3.Distance(new Vector3(hit.position.x, 0f, hit.position.z), new Vector3(playerFloor.x, 0f, playerFloor.z));
            if (dn < minDistance) return false;
            _path ??= new NavMeshPath();
            if (NavMesh.SamplePosition(playerFloor, out var playerHit, 1.0f, filter) &&
                (!NavMesh.CalculatePath(hit.position, playerHit.position, filter, _path) || _path.status != NavMeshPathStatus.PathComplete))
                return false;
            candidate = hit.position;
            return true;
        }

        /// <summary>A spot for a weapon pickup: a tabletop near the player if there is one, else the floor.</summary>
        public Vector3 FindPickupSpot(Vector3 playerFloor, float maxDistance = 3.5f)
        {
            if (HasScan)
            {
                var surfaces = MRUKAnchor.SceneLabels.TABLE | MRUKAnchor.SceneLabels.STORAGE | MRUKAnchor.SceneLabels.OTHER;
                foreach (var room in MRUK.Instance.Rooms)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        if (!room.GenerateRandomPositionOnSurface(MRUK.SurfaceType.FACING_UP, 0.08f, LabelFilter.Included(surfaces), out var p, out _))
                            break;
                        float d = Vector3.Distance(p, playerFloor);
                        if (d > 0.7f && d < maxDistance && p.y < playerFloor.y + 1.3f) return p + Vector3.up * 0.06f;
                    }
                }
            }
            for (int i = 0; i < 20; i++)
            {
                var dir = Random.insideUnitCircle.normalized;
                var p = playerFloor + new Vector3(dir.x, 0f, dir.y) * Random.Range(0.9f, 2.2f);
                if (IsInsideScan(p + Vector3.up) && !Physics.CheckSphere(p + Vector3.up * 0.3f, 0.2f, Layers.RoomMask))
                    return p + Vector3.up * 0.1f;
            }
            return playerFloor + Vector3.up * 0.1f + Vector3.forward;
        }
    }
}
