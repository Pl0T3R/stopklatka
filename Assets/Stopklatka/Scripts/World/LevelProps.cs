using System.Collections;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// The white virtual props of a level in the scanned rooms (PLAN: 3 levels = 3 scenarios in your
    /// own space; STYLE_GUIDE 9):
    ///   GALERIA: plinths with white statues over furniture, half columns and window light on walls,
    ///   ULICA:   cars and kerb blocks over furniture, lamp posts, rails and facade trims on walls,
    ///   HOL:     a reception counter over a table, half columns, elevator frames around door frames.
    /// MR safety (GameConfig props): free-standing props only cover scanned furniture and are never
    /// larger than its footprint; wall props are flat (at most props.maxWallDepth) on wall stretches
    /// free of door frames, windows, other wall anchors and furniture; nothing in open floor space
    /// (the floor light is a translucent decal without a collider), nothing within
    /// props.minPlayerDistance of where you stand at the level start, nothing on other storeys.
    /// Props rise from the floor when the level starts, sink away when it is cleared, are hidden while
    /// the game is paused and removed in the menu. A level plays the same with no props at all.
    /// Far props take on a light cyan haze (Crystal shader _FogStrength, props only).
    /// </summary>
    public class LevelProps : MonoBehaviour
    {
        public static LevelProps Instance { get; private set; }

        /// <summary>The level the current props belong to (0 = none).</summary>
        public int BuiltLevel { get; private set; }

        struct Item
        {
            public GameObject Root;
            public Renderer Renderer;
            public Material Normal, Reveal;   // Reveal null for the translucent ones
        }

        static readonly int RevealYId = Shader.PropertyToID("_RevealY");
        static readonly int RevealColorId = Shader.PropertyToID("_RevealColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int FogColorId = Shader.PropertyToID("_FogColor");
        static readonly int FogStartId = Shader.PropertyToID("_FogStart");
        static readonly int FogEndId = Shader.PropertyToID("_FogEnd");
        static readonly int FogStrengthId = Shader.PropertyToID("_FogStrength");

        readonly List<Item> _items = new();
        readonly PropPlanner.Scan _scan = new();
        readonly List<Vector2> _spans = new();
        readonly List<PropPlanner.Footprint> _furniture = new();
        readonly List<PropPlanner.Wall> _walls = new();

        Material _white, _statue, _whiteReveal, _statueReveal, _window, _floorLight;
        Color _windowColor, _floorLightColor;
        Coroutine _animation;
        Vector3 _player;
        float _floorY;
        int _furnitureCount, _wallCount, _pose, _alternate;
        bool _hidden;

        void Awake() => Instance = this;

        void OnDestroy() => ClearNow();

        /// <summary>
        /// Builds the props of a level around where you stand now and lets them rise from the floor.
        /// Nothing without a scan, with props off, or with theme None.
        /// </summary>
        public void Build(int level, PropTheme theme)
        {
            ClearNow();
            BuiltLevel = level;
            var cfg = Tuning.Props;
            var rooms = RoomService.Instance;
            var rig = PlayerRig.Instance;
            if (!cfg.enabled || theme == PropTheme.None || theme == PropTheme.Auto || rig == null) return;
            if (rooms == null || !rooms.HasScan)
            {
                Debug.Log("[Stopklatka] Props: no scan, the level plays without props.");
                return;
            }

            EnsureMaterials();
            _player = rig.FloorPosition;
            _floorY = _player.y;
            PropPlanner.Collect(_player, _scan);
            _furnitureCount = _wallCount = 0;
            _pose = Random.Range(0, EnemyBody.StatuePoseCount);
            _alternate = 0;

            // nearest first, so the props are around you rather than in a far room
            _furniture.Clear();
            _furniture.AddRange(_scan.Furniture);
            _furniture.Sort((a, b) => a.FlatDistance(_player).CompareTo(b.FlatDistance(_player)));
            _walls.Clear();
            _walls.AddRange(_scan.Walls);
            _walls.Sort((a, b) => WallDistance(a).CompareTo(WallDistance(b)));

            switch (theme)
            {
                case PropTheme.Galeria: BuildGaleria(); break;
                case PropTheme.Ulica: BuildUlica(); break;
                case PropTheme.Hol: BuildHol(); break;
            }
            Debug.Log($"[Stopklatka] Props {theme}: {_furnitureCount} on furniture, {_wallCount} on walls " +
                      $"(scan on your floor: {_scan.Furniture.Count} furniture, {_scan.Walls.Count} walls, {_scan.Doors.Count} doors).");

            if (_hidden) SetActive(false);
            Animate(show: true, cfg.revealSeconds);
        }

        /// <summary>Lets the props sink into the floor, then removes them (a level was cleared).</summary>
        public void FadeOut()
        {
            if (_items.Count == 0) { ClearNow(); return; }
            Animate(show: false, Tuning.Props.fadeSeconds);
        }

        /// <summary>Removes every prop at once (menu, endless waves).</summary>
        public void ClearNow()
        {
            if (_animation != null) StopCoroutine(_animation);
            _animation = null;
            foreach (var item in _items)
                if (item.Root != null) Destroy(item.Root);
            _items.Clear();
            BuiltLevel = 0;
        }

        /// <summary>Hidden while the game is paused (plain passthrough outside the scan).</summary>
        public void SetHidden(bool hidden)
        {
            _hidden = hidden;
            SetActive(!hidden);
        }

        void SetActive(bool active)
        {
            foreach (var item in _items)
                if (item.Root != null && item.Root.activeSelf != active) item.Root.SetActive(active);
        }

        // --- themes ----------------------------------------------------------------------

        void BuildGaleria()
        {
            var cfg = Tuning.Props;
            foreach (var fp in _furniture)
            {
                if (!CanTakeFurniture(fp)) continue;
                AddPlinth(fp);
                // a statue on top if the furniture is low and wide enough
                float statue = Mathf.Min(cfg.statueHeight, cfg.maxHeight - (fp.TopY - _floorY));
                if (fp.MinSide >= 0.28f && statue >= 0.35f)
                    Add(PropKit.Statue(fp, _pose++, statue, _player, _statue), _statue, _statueReveal);
            }

            float panelTop = Mathf.Min(_floorY + cfg.maxHeight, _floorY + 2.5f);
            foreach (var wall in _walls)
            {
                if (_wallCount >= cfg.maxWallProps || TotalFull()) break;
                PropPlanner.FreeSpans(wall, wall.BottomY, wall.TopY, cfg.wallMargin, _spans);
                int onWall = 0;
                foreach (var span in _spans)
                {
                    float x = span.x;
                    while (onWall < MaxPerWall && _wallCount < cfg.maxWallProps && !TotalFull())
                    {
                        bool window = _alternate % 2 == 0;
                        float width = window ? cfg.windowWidth : PropKit.ColumnWidth;
                        if (x + width > span.y) break;
                        if (window ? TryWindow(wall, x, width, Mathf.Min(panelTop, wall.TopY - 0.15f)) : TryColumn(wall, x))
                        {
                            onWall++;
                            _alternate++;
                            x += width + cfg.wallSpacing;
                        }
                        else x += 0.25f;
                    }
                }
            }
        }

        void BuildUlica()
        {
            var cfg = Tuning.Props;
            foreach (var fp in _furniture)
            {
                if (!CanTakeFurniture(fp)) continue;
                bool big = fp.HasAnyLabel(MRUKAnchor.SceneLabels.COUCH | MRUKAnchor.SceneLabels.BED) || fp.Area >= 0.9f;
                if (big && fp.MinSide >= 0.5f && fp.MaxSide >= 1f)
                {
                    Add(PropKit.Car(fp, _white), _white, _whiteReveal);
                    _furnitureCount++;
                }
                else AddPlinth(fp);   // a kerb block
            }

            // facade trims high on the walls, then lamp posts and rails low on them
            int trims = 0;
            foreach (var wall in _walls)
            {
                if (trims >= 3 || _wallCount >= cfg.maxWallProps || TotalFull()) break;
                float y = Mathf.Min(wall.TopY - 0.3f, _floorY + cfg.maxHeight - 0.2f);
                if (y < _floorY + 2f) continue;
                PropPlanner.FreeSpans(wall, y - 0.1f, y + 0.3f, cfg.wallMargin, _spans);
                foreach (var span in _spans)
                {
                    if (span.y - span.x < 0.8f || !FarEnough(wall, span.x, span.y)) continue;
                    Add(PropKit.Trim(wall, span.x, span.y, y, _white), _white, _whiteReveal);
                    _wallCount++;
                    trims++;
                    break;   // one trim per wall
                }
            }
            foreach (var wall in _walls)
            {
                if (_wallCount >= cfg.maxWallProps || TotalFull()) break;
                PropPlanner.FreeSpans(wall, wall.BottomY, _floorY + cfg.maxHeight, cfg.wallMargin, _spans);
                int onWall = 0;
                foreach (var span in _spans)
                {
                    float x = span.x;
                    while (onWall < MaxPerWall && _wallCount < cfg.maxWallProps && !TotalFull())
                    {
                        bool lamp = _alternate % 2 == 0;
                        float width = lamp ? PropKit.LampWidth : Mathf.Min(1.8f, span.y - x);
                        if (width < (lamp ? PropKit.LampWidth : 0.8f) || x + width > span.y) break;
                        if (FarEnough(wall, x, x + width))
                        {
                            float height = Mathf.Min(cfg.maxHeight, wall.TopY - wall.BottomY - 0.1f);
                            if (lamp) Add(PropKit.LampPost(wall, x, height, cfg.maxWallDepth, _white), _white, _whiteReveal);
                            else Add(PropKit.Rail(wall, x, x + width, cfg.maxWallDepth, _white), _white, _whiteReveal);
                            _wallCount++;
                            onWall++;
                            _alternate++;
                            x += width + cfg.wallSpacing;
                        }
                        else x += 0.25f;
                    }
                }
            }
        }

        void BuildHol()
        {
            var cfg = Tuning.Props;
            // the reception counter over the largest table (or the largest furniture)
            int counter = -1;
            for (int i = 0; i < _furniture.Count; i++)
            {
                var fp = _furniture[i];
                if (!CanTakeFurniture(fp) || fp.MinSide < 0.4f) continue;
                bool table = fp.HasAnyLabel(MRUKAnchor.SceneLabels.TABLE);
                if (counter < 0) { counter = i; continue; }
                var best = _furniture[counter];
                bool bestTable = best.HasAnyLabel(MRUKAnchor.SceneLabels.TABLE);
                if ((table && !bestTable) || (table == bestTable && fp.Area > best.Area)) counter = i;
            }
            if (counter >= 0)
            {
                var fp = _furniture[counter];
                Add(PropKit.Counter(fp, Mathf.Min(fp.Height, cfg.maxHeight), _white), _white, _whiteReveal);
                _furnitureCount++;
            }
            for (int i = 0; i < _furniture.Count; i++)
                if (i != counter && CanTakeFurniture(_furniture[i])) AddPlinth(_furniture[i]);

            // elevator frames around the door frames (enemies walk out of them: LevelManager raises the door spawns)
            foreach (var door in _scan.Doors)
            {
                if (_wallCount >= cfg.maxWallProps || TotalFull()) break;
                TryElevator(door);
            }

            foreach (var wall in _walls)
            {
                if (_wallCount >= cfg.maxWallProps || TotalFull()) break;
                PropPlanner.FreeSpans(wall, wall.BottomY, wall.TopY, cfg.wallMargin, _spans);
                int onWall = 0;
                foreach (var span in _spans)
                {
                    float x = span.x;
                    while (onWall < MaxPerWall && _wallCount < cfg.maxWallProps && !TotalFull() && x + PropKit.ColumnWidth <= span.y)
                    {
                        if (TryColumn(wall, x))
                        {
                            onWall++;
                            x += PropKit.ColumnWidth + cfg.wallSpacing * 1.5f;
                        }
                        else x += 0.25f;
                    }
                }
            }
        }

        // --- placing, with the safety rules ----------------------------------------------

        const int MaxPerWall = 2;

        bool TotalFull() => _furnitureCount + _wallCount >= Tuning.Props.maxProps;

        bool CanTakeFurniture(PropPlanner.Footprint fp)
        {
            var cfg = Tuning.Props;
            return _furnitureCount < cfg.maxFurnitureProps && !TotalFull() && fp.MinSide >= 0.25f &&
                   fp.FlatDistance(_player) >= cfg.minPlayerDistance;
        }

        void AddPlinth(PropPlanner.Footprint fp)
        {
            Add(PropKit.Plinth(fp, Mathf.Min(fp.Height, Tuning.Props.maxHeight), _white), _white, _whiteReveal);
            _furnitureCount++;
        }

        bool FarEnough(PropPlanner.Wall wall, float from, float to) =>
            PropPlanner.FlatSegmentDistance(_player, wall.At(from), wall.At(to)) >= Tuning.Props.minPlayerDistance;

        float WallDistance(PropPlanner.Wall wall) => PropPlanner.FlatSegmentDistance(_player, wall.At(0f), wall.At(wall.Length));

        bool TryColumn(PropPlanner.Wall wall, float x)
        {
            var cfg = Tuning.Props;
            float height = Mathf.Min(cfg.maxHeight, wall.TopY - wall.BottomY - 0.05f);
            if (height < 1.8f || !FarEnough(wall, x, x + PropKit.ColumnWidth)) return false;
            Add(PropKit.HalfColumn(wall, x + PropKit.ColumnWidth * 0.5f, height, cfg.maxWallDepth, _white), _white, _whiteReveal);
            _wallCount++;
            return true;
        }

        bool TryWindow(PropPlanner.Wall wall, float x, float width, float top)
        {
            var cfg = Tuning.Props;
            float bottom = wall.BottomY + 0.35f;
            if (top - bottom < 1.2f || !FarEnough(wall, x, x + width)) return false;
            Add(PropKit.WindowLight(wall, x + width * 0.5f, width, bottom, top, _window), _window, null);
            _wallCount++;

            // the pale light on the floor below it: only on free floor inside the room, never near you
            float depth = cfg.floorLightDepth;
            var a = wall.At(x) + wall.Inward * 0.03f;
            var b = wall.At(x + width) + wall.Inward * 0.03f;
            var c = b + wall.Inward * depth;
            var d = a + wall.Inward * depth;
            if (PropPlanner.FlatSegmentDistance(_player, c, d) >= cfg.minPlayerDistance &&
                PropPlanner.FloorPointFree(a) && PropPlanner.FloorPointFree(b) &&
                PropPlanner.FloorPointFree(c) && PropPlanner.FloorPointFree(d) &&
                PropPlanner.FloorPointFree((a + c) * 0.5f))
                Add(PropKit.FloorLight(wall, x + width * 0.5f, width, depth, _floorLight), _floorLight, null);
            return true;
        }

        void TryElevator(PropPlanner.Door door)
        {
            var cfg = Tuning.Props;
            var wall = door.Wall;
            if (wall == null) return;   // no wall found on your side: leave the opening alone
            float outer = door.HalfWidth + PropKit.ElevatorGap + PropKit.ElevatorJamb;
            float maxTop = Mathf.Min(wall.TopY - 0.02f, _floorY + Mathf.Max(cfg.maxHeight, door.Height + 0.2f)) - door.Base.y;
            if (maxTop < door.Height + 0.02f) return;
            float top = door.Base.y + maxTop;
            // both jambs on free wall, the lintel too (nothing hangs above the door), and away from you
            if (!PropPlanner.WallFree(wall, door.S - outer, door.S - door.HalfWidth, wall.BottomY, top, door.Base) ||
                !PropPlanner.WallFree(wall, door.S + door.HalfWidth, door.S + outer, wall.BottomY, top, door.Base) ||
                !PropPlanner.WallFree(wall, door.S - outer, door.S + outer, door.Base.y + door.Height, top, door.Base) ||
                !FarEnough(wall, door.S - outer, door.S + outer))
                return;
            Add(PropKit.ElevatorFrame(door, cfg.maxWallDepth * 0.7f, maxTop, _white), _white, _whiteReveal);
            _wallCount++;
        }

        void Add(GameObject root, Material normal, Material reveal)
        {
            if (root == null) return;
            root.TryGetComponent(out Renderer own);
            var r = own != null ? own : root.GetComponentInChildren<Renderer>();
            _items.Add(new Item { Root = root, Renderer = r, Normal = normal, Reveal = reveal });
        }

        // --- transitions -----------------------------------------------------------------

        void Animate(bool show, float seconds)
        {
            if (_animation != null) StopCoroutine(_animation);
            _animation = StartCoroutine(Sweep(show, Mathf.Max(0.05f, seconds)));
        }

        /// <summary>
        /// Opaque props are cut at a height that sweeps up from the floor (rising, with the glowing
        /// edge of the enemy reveal) or down into it (sinking); translucent ones fade. Real time.
        /// </summary>
        IEnumerator Sweep(bool show, float seconds)
        {
            float low = _floorY - 0.05f, high = _floorY + Tuning.Props.maxHeight + 0.3f;
            foreach (var item in _items)
                if (item.Renderer != null && item.Reveal != null) item.Renderer.sharedMaterial = item.Reveal;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                SetSweep(show ? Mathf.Lerp(low, high, k) : Mathf.Lerp(high, low, k), show ? k : 1f - k);
                yield return null;
            }
            _animation = null;
            if (!show)
            {
                ClearNow();
                yield break;
            }
            SetSweep(high, 1f);
            // back on the plain shared materials (SRP batcher, no reveal variant)
            foreach (var item in _items)
                if (item.Renderer != null) item.Renderer.sharedMaterial = item.Normal;
        }

        void SetSweep(float revealY, float alpha)
        {
            _whiteReveal.SetFloat(RevealYId, revealY);
            _statueReveal.SetFloat(RevealYId, revealY);
            _window.SetColor(BaseColorId, new Color(_windowColor.r, _windowColor.g, _windowColor.b, _windowColor.a * alpha));
            _floorLight.SetColor(BaseColorId, new Color(_floorLightColor.r, _floorLightColor.g, _floorLightColor.b, _floorLightColor.a * alpha));
        }

        /// <summary>Runtime copies of the prop materials, with the haze from GameConfig props.</summary>
        void EnsureMaterials()
        {
            if (_white == null)
            {
                _white = new Material(Mats.Get(Mats.PropWhite)) { name = "PropWhite (level)" };
                _statue = new Material(Mats.Get(Mats.Statue)) { name = "StatueWhite (level)" };
                _whiteReveal = RevealCopy(_white);
                _statueReveal = RevealCopy(_statue);
                _window = new Material(Mats.Get(Mats.WindowLight)) { name = "WindowLight (level)" };
                _floorLight = new Material(Mats.Get(Mats.FloorLight)) { name = "FloorLight (level)" };
                _windowColor = _window.GetColor(BaseColorId);
                _floorLightColor = _floorLight.GetColor(BaseColorId);
            }
            var cfg = Tuning.Props;
            foreach (var m in new[] { _white, _statue, _whiteReveal, _statueReveal })
            {
                m.SetColor(FogColorId, cfg.hazeColor);
                m.SetFloat(FogStartId, cfg.hazeStart);
                m.SetFloat(FogEndId, Mathf.Max(cfg.hazeStart + 0.1f, cfg.hazeEnd));
                m.SetFloat(FogStrengthId, cfg.haze ? cfg.hazeStrength : 0f);
            }
        }

        static Material RevealCopy(Material source)
        {
            // the _REVEAL variant ships because the EnemyReveal material uses it
            var m = new Material(source) { name = source.name + " reveal" };
            m.EnableKeyword("_REVEAL");
            m.SetColor(RevealColorId, Color.white);
            return m;
        }
    }
}
