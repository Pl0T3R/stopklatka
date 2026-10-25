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
            if (room == null || room.FloorAnchor == null || PlayerRig.Instance == null) return;
            float y = room.FloorAnchor.transform.position.y;
            PlayerRig.Instance.FloorY = y;
            Shader.SetGlobalFloat(FloorYId, y);
        }

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

        /// <summary>
        /// Finds a floor spot for an enemy: inside a scanned room, not inside furniture, between
        /// the min and max distance from the player, and connected to the player by the NavMesh.
        /// </summary>
        public bool TryFindEnemySpawn(Vector3 playerFloor, out Vector3 position)
        {
            // second pass with a shorter minimum distance, for small rooms
            float min = Tuning.EnemyMinSpawnDistance;
            return TryFindEnemySpawn(playerFloor, min, out position) ||
                   TryFindEnemySpawn(playerFloor, Mathf.Max(1.2f, min * 0.6f), out position);
        }

        bool TryFindEnemySpawn(Vector3 playerFloor, float minDistance, out Vector3 position)
        {
            position = default;
            var filter = new NavMeshQueryFilter { agentTypeID = NavAgentTypeId, areaMask = NavMesh.AllAreas };
            NavMeshHit playerHit = default;
            bool playerOnNav = NavReady && NavMesh.SamplePosition(playerFloor, out playerHit, 1.0f, filter);
            var path = new NavMeshPath();
            var playerRoom = HasScan ? RoomAt(playerFloor + Vector3.up) : null;

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

                float d = Vector3.Distance(new Vector3(candidate.x, 0f, candidate.z), new Vector3(playerFloor.x, 0f, playerFloor.z));
                if (d < minDistance || d > Tuning.EnemyMaxSpawnDistance) continue;

                if (NavReady)
                {
                    if (!NavMesh.SamplePosition(candidate, out var hit, 0.6f, filter)) continue;
                    candidate = hit.position;
                    if (playerOnNav && (!NavMesh.CalculatePath(candidate, playerHit.position, filter, path) || path.status != NavMeshPathStatus.PathComplete))
                        continue;
                }
                position = candidate;
                return true;
            }
            return false;
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
