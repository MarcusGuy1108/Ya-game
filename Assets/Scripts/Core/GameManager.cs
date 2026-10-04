using UnityEngine;
using UKCity.City;
using UKCity.Core;
using UKCity.Planner;
using UKCity.Player;
using UKCity.Save;
using UKCity.UI;
using UKCity.World;

namespace UKCity
{
    public enum GameMode { Walk, Plan }

    /// <summary>
    /// Owns every system and switches between first person (Walk) and the 2D city planner (Plan).
    /// Everything is created from code (see Bootstrap), so the project needs no scene setup.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        public const string SaveSlot = "world1";
        private static readonly Color Sky = new Color(0.62f, 0.76f, 0.9f);

        public VoxelWorld World { get; private set; }
        public CityLayer City { get; private set; }
        public PlayerController Player { get; private set; }
        public BlockInteractor Interactor { get; private set; }
        public PlannerController Planner { get; private set; }
        public Hud Hud { get; private set; }
        public Camera WalkCam { get; private set; }
        public Camera PlanCam { get; private set; }
        public GameMode Mode { get; private set; } = GameMode.Walk;
        public TrafficSignals Signals { get; private set; }
        public DynamicFaces Animated { get; private set; }

        public string ToastText { get; private set; }
        public float ToastUntil { get; private set; }

        private LineBatch worldLines;
        private LineBatch overlayLines;

        // ------------------------------------------------------------------ setup

        public void Setup()
        {
            Application.targetFrameRate = 120;
            SaveSystem.LoadCustomTemplates();

            City = new CityLayer();
            Signals = new TrafficSignals(City);
            Animated = new DynamicFaces { Signals = Signals };
            World = new GameObject("World").AddComponent<VoxelWorld>();
            World.transform.SetParent(transform, false);
            World.Animated = Animated;
            World.Init(City, Random.Range(1, int.MaxValue));

            // Player + first person camera.
            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(transform, false);
            Player = playerGo.AddComponent<PlayerController>();
            var camGo = new GameObject("Walk Camera");
            camGo.transform.SetParent(playerGo.transform, false);
            camGo.transform.localPosition = new Vector3(0, PlayerController.EyeHeight, 0);
            WalkCam = camGo.AddComponent<Camera>();
            WalkCam.nearClipPlane = 0.05f;
            WalkCam.farClipPlane = 400f;
            WalkCam.fieldOfView = 75f;
            WalkCam.clearFlags = CameraClearFlags.SolidColor;
            WalkCam.backgroundColor = Sky;
            camGo.AddComponent<AudioListener>();
            Player.World = World;
            Player.Cam = WalkCam;

            Interactor = playerGo.AddComponent<BlockInteractor>();
            Interactor.Game = this;
            Interactor.Player = Player;

            // Planner camera looks straight down; north (+Z) is up the screen.
            var planGo = new GameObject("Planner Camera");
            planGo.transform.SetParent(transform, false);
            planGo.transform.rotation = Quaternion.Euler(90, 0, 0);
            PlanCam = planGo.AddComponent<Camera>();
            PlanCam.orthographic = true;
            PlanCam.nearClipPlane = 1f;
            PlanCam.farClipPlane = 400f;
            PlanCam.clearFlags = CameraClearFlags.SolidColor;
            PlanCam.backgroundColor = new Color(0.2f, 0.26f, 0.2f);
            Planner = planGo.AddComponent<PlannerController>();
            Planner.Game = this;
            Planner.Cam = PlanCam;

            Hud = gameObject.AddComponent<Hud>();
            Hud.Game = this;

            worldLines = new LineBatch(false);
            overlayLines = new LineBatch(true);

            Shader.SetGlobalFloat("_UKDaylight", 1f);
            Shader.SetGlobalColor("_UKFogColor", Sky);

            if (!SaveSystem.Exists(SaveSlot) || !TryLoad()) NewWorld(true);
            SetMode(GameMode.Walk);
        }

        private bool TryLoad()
        {
            try
            {
                bool ok = SaveSystem.Load(this, SaveSlot);
                Signals.Invalidate();
                return ok;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                Toast("Couldn't load the save, starting fresh.");
                return false;
            }
        }

        // ------------------------------------------------------------------ frame loop

        private void Update()
        {
            HandleGlobalKeys();
            Signals.Time += Time.deltaTime;
            Animated.Time = Signals.Time;

            bool menuOpen = Hud.Open != Panel.None;
            if (Mode == GameMode.Walk)
            {
                Player.InputEnabled = !menuOpen;
                Interactor.Tick(!menuOpen);
                World.SetFocus(Player.transform.position, 8);
                Shader.SetGlobalVector("_UKFogParams", new Vector4(90f, 130f, 1f, 0f));
            }
            else
            {
                Player.InputEnabled = false;
                if (!menuOpen) Planner.Tick(Hud.PointerOverUI());
                World.SetFocus(Planner.Focus, Planner.LoadRadius);
                Shader.SetGlobalVector("_UKFogParams", new Vector4(0f, 1f, 0f, 0f));
            }

            bool lockCursor = Mode == GameMode.Walk && !menuOpen;
            Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockCursor;
        }

        private void LateUpdate()
        {
            if (Mode == GameMode.Walk)
            {
                Animated.Draw(WalkCam, WalkCam.transform.position, 140f);
                Interactor.DrawOverlays(worldLines, WalkCam);
                worldLines.Flush(WalkCam);
            }
            else
            {
                Animated.Draw(PlanCam, PlanCam.transform.position, 400f);
                Planner.DrawOverlays(overlayLines);
                overlayLines.Flush(PlanCam);
            }
        }

        private void HandleGlobalKeys()
        {
            if (GameInput.KeyDown(KeyCode.Escape))
            {
                if (Hud.Open != Panel.None) Hud.Open = Panel.None;
                else if (Mode == GameMode.Walk && Interactor.ActiveTemplate != null) Interactor.CancelTemplate();
                else if (Mode == GameMode.Walk && Interactor.CaptureCorner.HasValue) Interactor.CancelCapture();
                else if (Mode == GameMode.Plan && Planner.IsDrawing) Planner.CancelAll();
                else Hud.Open = Panel.Pause;
                return;
            }
            if (GameInput.KeyDown(KeyCode.F1)) Hud.Open = Hud.Open == Panel.Help ? Panel.None : Panel.Help;
            if (GameInput.KeyDown(KeyCode.F5)) SaveGame();
            if (GameInput.KeyDown(KeyCode.F9)) LoadGame();

            if (Hud.Open == Panel.Pause || Hud.Open == Panel.Help) return;

            if (Mode == GameMode.Walk)
            {
                if (GameInput.KeyDown(KeyCode.E)) Hud.Open = Hud.Open == Panel.Blocks ? Panel.None : Panel.Blocks;
                if (GameInput.KeyDown(KeyCode.B)) Hud.Open = Hud.Open == Panel.Buildings ? Panel.None : Panel.Buildings;
            }
            if (GameInput.KeyDown(KeyCode.Tab) && Hud.Open == Panel.None)
                SetMode(Mode == GameMode.Walk ? GameMode.Plan : GameMode.Walk);
        }

        public void SetMode(GameMode m)
        {
            if (m == GameMode.Plan && Mode != GameMode.Plan) Planner.CenterOn(Player.transform.position);
            Mode = m;
            WalkCam.enabled = m == GameMode.Walk;
            PlanCam.enabled = m == GameMode.Plan;
            Planner.CancelAll();
            if (m == GameMode.Plan) Interactor.CancelTemplate();
        }

        private void OnApplicationQuit()
        {
            if (World != null) SaveGame();
        }

        public void Toast(string text, float seconds = 3.5f)
        {
            ToastText = text;
            ToastUntil = Time.unscaledTime + seconds;
        }

        // ------------------------------------------------------------------ actions

        /// <summary>Drop into first person at a point picked on the map.</summary>
        public void WalkHere(Vector2 xz)
        {
            int x = Mathf.FloorToInt(xz.x), z = Mathf.FloorToInt(xz.y);
            int top = World.TopBlockY(x, z);
            float y = top >= 0 ? top + 1 : WorldConst.SurfaceY + 1;
            Player.Teleport(new Vector3(x + 0.5f, y, z + 0.5f));
            Player.Flying = false;
            SetMode(GameMode.Walk);
        }

        public void PlaceBuilding(string templateId, Vector3Int origin, int rot)
        {
            var t = BuildingLibrary.Get(templateId);
            if (t == null) return;
            // A fresh building replaces any hand edits inside its volume.
            var fp = t.Footprint(origin, rot);
            World.ClearEdits(new Vector3Int(fp.xMin, origin.y, fp.yMin), new Vector3Int(fp.xMax - 1, origin.y + t.SizeY - 1, fp.yMax - 1));
            City.AddBuilding(templateId, origin, rot);
        }

        public void RemoveBuilding(int id)
        {
            if (!City.Buildings.TryGetValue(id, out var p)) return;
            var t = p.Template;
            if (t != null)
            {
                var fp = t.Footprint(p.Origin, p.Rotation);
                World.ClearEdits(new Vector3Int(fp.xMin, p.Origin.y, fp.yMin), new Vector3Int(fp.xMax - 1, p.Origin.y + t.SizeY - 1, fp.yMax - 1));
            }
            City.RemoveBuilding(id);
        }

        /// <summary>Rotates a placed building 90 degrees about its anchor. Returns the new placement id.</summary>
        public int RotateBuilding(int id)
        {
            if (!City.Buildings.TryGetValue(id, out var p)) return 0;
            string tid = p.TemplateId;
            var origin = p.Origin;
            int rot = (p.Rotation + 1) & 3;
            RemoveBuilding(id);
            var t = BuildingLibrary.Get(tid);
            if (t == null) return 0;
            var fp = t.Footprint(origin, rot);
            World.ClearEdits(new Vector3Int(fp.xMin, origin.y, fp.yMin), new Vector3Int(fp.xMax - 1, origin.y + t.SizeY - 1, fp.yMax - 1));
            return City.AddBuilding(tid, origin, rot)?.Id ?? 0;
        }

        public void SaveGame()
        {
            try
            {
                SaveSystem.Save(this, SaveSlot);
                Toast("Saved.");
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                Toast("Save failed: " + e.Message);
            }
        }

        public void LoadGame()
        {
            if (!SaveSystem.Exists(SaveSlot)) { Toast("No save yet. Press F5 to save."); return; }
            if (TryLoad()) Toast("Loaded.");
        }

        public void NewWorld(bool starterTown)
        {
            City.Clear();
            World.Edits.Clear();
            World.Seed = Random.Range(1, int.MaxValue);
            World.ResetAll();
            Interactor.CancelTemplate();
            if (starterTown) StarterTown.Build(City);
            Signals.Invalidate();
            Player.Teleport(new Vector3(6.5f, WorldConst.SurfaceY + 1, 28.5f));
            Player.Yaw = 0;
            Player.Pitch = 0;
            Player.Flying = false;
            Planner.CenterOn(Player.transform.position);
        }
    }
}
