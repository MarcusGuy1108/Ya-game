using UnityEngine;
using UKCity.Core;
using UKCity.World;

namespace UKCity.Player
{
    /// <summary>
    /// First-person walker with custom voxel AABB collision (no Unity physics needed).
    /// Position is the centre of the player's feet.
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        public const float HalfWidth = 0.3f;
        public const float Height = 1.8f;
        public const float EyeHeight = 1.62f;

        public VoxelWorld World;
        public Camera Cam;
        public bool Flying;
        public float MouseSensitivity = 2.0f;

        public float Yaw;
        public float Pitch;
        private Vector3 velocity;
        private bool grounded;
        private float lastSpacePress = -1f;
        private bool waitingForGround = true;

        public bool InputEnabled { get; set; }
        public bool Grounded => grounded;

        public void Teleport(Vector3 feet)
        {
            transform.position = feet;
            velocity = Vector3.zero;
            waitingForGround = true;
        }

        private void Update()
        {
            if (World == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);

            if (InputEnabled) Look();
            Cam.transform.localRotation = Quaternion.Euler(Pitch, 0, 0);
            transform.rotation = Quaternion.Euler(0, Yaw, 0);

            var pos = transform.position;
            int bx = Mathf.FloorToInt(pos.x), bz = Mathf.FloorToInt(pos.z);
            if (!World.IsLoaded(bx, bz)) return; // hold still until the ground exists

            if (waitingForGround)
            {
                waitingForGround = false;
                if (Collides(pos))
                {
                    int top = World.TopBlockY(bx, bz);
                    pos.y = top + 1;
                    transform.position = pos;
                }
            }

            Move(dt);
        }

        private void Look()
        {
            var d = GameInput.MouseDelta * MouseSensitivity;
            Yaw += d.x;
            Pitch = Mathf.Clamp(Pitch - d.y, -89f, 89f);
        }

        private void Move(float dt)
        {
            Vector3 wish = Vector3.zero;
            if (InputEnabled)
            {
                if (GameInput.KeyHeld(KeyCode.W)) wish.z += 1;
                if (GameInput.KeyHeld(KeyCode.S)) wish.z -= 1;
                if (GameInput.KeyHeld(KeyCode.D)) wish.x += 1;
                if (GameInput.KeyHeld(KeyCode.A)) wish.x -= 1;

                if (GameInput.KeyDown(KeyCode.F)) ToggleFly();
                if (GameInput.KeyDown(KeyCode.Space))
                {
                    if (Time.time - lastSpacePress < 0.3f) { ToggleFly(); lastSpacePress = -1f; }
                    else lastSpacePress = Time.time;
                }
            }
            wish = transform.rotation * Vector3.ClampMagnitude(wish, 1f);
            bool sprint = InputEnabled && GameInput.KeyHeld(KeyCode.LeftShift);

            if (Flying)
            {
                float speed = sprint ? 24f : 11f;
                float vy = 0;
                if (InputEnabled && GameInput.KeyHeld(KeyCode.Space)) vy += 1;
                if (InputEnabled && GameInput.Ctrl) vy -= 1;
                var target = new Vector3(wish.x * speed, vy * speed * 0.8f, wish.z * speed);
                velocity = Vector3.Lerp(velocity, target, 1f - Mathf.Exp(-12f * dt));
            }
            else
            {
                float speed = sprint ? 6.5f : 4.3f;
                float accel = grounded ? 14f : 3f;
                var horiz = new Vector3(velocity.x, 0, velocity.z);
                horiz = Vector3.Lerp(horiz, wish * speed, 1f - Mathf.Exp(-accel * dt));
                velocity.x = horiz.x;
                velocity.z = horiz.z;
                velocity.y -= 28f * dt;
                if (velocity.y < -60f) velocity.y = -60f;
                if (grounded && InputEnabled && GameInput.KeyHeld(KeyCode.Space)) velocity.y = 8.6f;
            }

            var p = transform.position;
            var delta = velocity * dt;
            grounded = false;

            float my = MoveAxis(ref p, 1, delta.y);
            if (my != delta.y)
            {
                if (delta.y < 0) { grounded = true; if (Flying) Flying = false; }
                velocity.y = 0;
            }
            float mx = MoveAxis(ref p, 0, delta.x);
            if (mx != delta.x) velocity.x = 0;
            float mz = MoveAxis(ref p, 2, delta.z);
            if (mz != delta.z) velocity.z = 0;

            if (p.y < -20) p = new Vector3(p.x, WorldConst.SurfaceY + 30, p.z);
            transform.position = p;
        }

        private void ToggleFly()
        {
            Flying = !Flying;
            velocity.y = 0;
        }

        // ------------------------------------------------------------------ collision

        private readonly System.Collections.Generic.List<Bounds> boxes = new System.Collections.Generic.List<Bounds>();

        public bool Collides(Vector3 feet)
        {
            var min = new Vector3(feet.x - HalfWidth, feet.y, feet.z - HalfWidth);
            var max = new Vector3(feet.x + HalfWidth, feet.y + Height, feet.z + HalfWidth);
            for (int y = Mathf.FloorToInt(min.y); y <= Mathf.FloorToInt(max.y - 0.001f); y++)
                for (int z = Mathf.FloorToInt(min.z); z <= Mathf.FloorToInt(max.z - 0.001f); z++)
                    for (int x = Mathf.FloorToInt(min.x); x <= Mathf.FloorToInt(max.x - 0.001f); x++)
                    {
                        ushort b = World.GetBlock(x, y, z);
                        if (!Blocks.IsSolidForPhysics(b)) continue;
                        boxes.Clear();
                        Blocks.CollisionBoxes(b, x, y, z, boxes);
                        foreach (var bb in boxes)
                            if (min.x < bb.max.x && max.x > bb.min.x && min.y < bb.max.y && max.y > bb.min.y && min.z < bb.max.z && max.z > bb.min.z)
                                return true;
                    }
            return false;
        }

        /// <summary>True if a block at this cell would intersect the player.</summary>
        public bool Overlaps(Vector3Int cell)
        {
            var p = transform.position;
            return cell.x + 1 > p.x - HalfWidth && cell.x < p.x + HalfWidth &&
                   cell.z + 1 > p.z - HalfWidth && cell.z < p.z + HalfWidth &&
                   cell.y + 1 > p.y && cell.y < p.y + Height;
        }

        /// <summary>Moves along one axis, stopping at the first solid block. Returns the distance actually moved.</summary>
        private float MoveAxis(ref Vector3 pos, int axis, float delta)
        {
            if (delta == 0) return 0;
            const float eps = 0.001f;
            var min = new Vector3(pos.x - HalfWidth, pos.y, pos.z - HalfWidth);
            var max = new Vector3(pos.x + HalfWidth, pos.y + Height, pos.z + HalfWidth);

            // Region swept by the move.
            var smin = min; var smax = max;
            if (delta > 0) smax[axis] += delta; else smin[axis] += delta;

            for (int y = Mathf.FloorToInt(smin.y); y <= Mathf.FloorToInt(smax.y); y++)
                for (int z = Mathf.FloorToInt(smin.z); z <= Mathf.FloorToInt(smax.z); z++)
                    for (int x = Mathf.FloorToInt(smin.x); x <= Mathf.FloorToInt(smax.x); x++)
                    {
                        ushort b = World.GetBlock(x, y, z);
                        if (!Blocks.IsSolidForPhysics(b)) continue;
                        boxes.Clear();
                        Blocks.CollisionBoxes(b, x, y, z, boxes);
                        foreach (var bb in boxes)
                        {
                            var bmin = bb.min;
                            var bmax = bb.max;
                            // Must overlap on the other two axes.
                            bool overlap = true;
                            for (int a = 0; a < 3 && overlap; a++)
                            {
                                if (a == axis) continue;
                                if (!(min[a] < bmax[a] - eps && max[a] > bmin[a] + eps)) overlap = false;
                            }
                            if (!overlap) continue;
                            if (delta > 0 && bmin[axis] >= max[axis] - eps) delta = Mathf.Min(delta, Mathf.Max(0f, bmin[axis] - max[axis] - eps));
                            else if (delta < 0 && bmax[axis] <= min[axis] + eps) delta = Mathf.Max(delta, Mathf.Min(0f, bmax[axis] - min[axis] + eps));
                        }
                    }
            if (Mathf.Abs(delta) < eps * 0.5f) delta = 0;
            pos[axis] += delta;
            return delta;
        }
    }
}
