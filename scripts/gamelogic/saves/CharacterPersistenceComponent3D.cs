using System.Text.Json.Nodes;
using Framework;
using Godot;

namespace GameLogic
{
    [GlobalClass]
    public partial class CharacterPersistenceComponent3D : Component3D, ISaveSection
    {
        public override int Priority => int.MinValue + 10;

        [Export] public bool Persist { get; set; } = true;
        [Export] public bool RestorePosition { get; set; } = true;
        [Export] public string PersistentIdOverride { get; set; } = string.Empty;
        [Export] public Godot.Collections.Dictionary<string, bool> PersistentFlags { get; set; } = new();

        public string SectionKey => "characters";
        public string EntryKey => string.IsNullOrWhiteSpace(PersistentIdOverride)
            ? Owner?.PersistentId ?? string.Empty
            : PersistentIdOverride.Trim();
        public int SchemaVersion => 3;

        public override void OnInit()
        {
            if (!Persist || Owner == null)
                return;

            PersistentIdUtility.EnsurePersistentId(Owner);
            if (!string.IsNullOrWhiteSpace(PersistentIdOverride))
                Owner.PersistentId = PersistentIdOverride.Trim();
            ModuleSystem.GetModule<ISaveModule>()?.RegisterSection(this);
        }

        public override void OnDestroy()
        {
            ModuleSystem.GetModule<ISaveModule>()?.UnregisterSection(this);
        }

        public JsonObject Capture()
        {
            if (!Persist || Owner == null)
                return new JsonObject();

            var state = new JsonObject
            {
                ["persistent_id"] = EntryKey,
                ["position"] = new JsonObject { ["x"] = Owner.GlobalPosition.X, ["y"] = Owner.GlobalPosition.Y, ["z"] = Owner.GlobalPosition.Z },
                ["rotation"] = WriteVector(Owner.GlobalRotation),
                ["facing_direction"] = WriteVector(Owner.GetComponent<CharacterMovementComponent3D>()?.FacingDirection ?? Vector3.Right),
                ["facing"] = Owner.GetComponent<CharacterMovementComponent3D>()?.Facing ?? 1,
                ["flags"] = CaptureFlags()
            };

            AbilitySystemComponent3D abilities = Owner.GetComponent<AbilitySystemComponent3D>();
            if (abilities != null)
                state["abilities"] = abilities.CaptureDurableState();

            return state;
        }

        public void Restore(JsonObject state, int schemaVersion)
        {
            if (!Persist || Owner == null || state == null || schemaVersion > SchemaVersion)
                return;

            if (RestorePosition && state["position"] is JsonObject position)
            {
                float x = position["x"]?.GetValue<float>() ?? Owner.GlobalPosition.X;
                float y = position["y"]?.GetValue<float>() ?? Owner.GlobalPosition.Y;
                float z = position["z"]?.GetValue<float>() ?? 0f;
                // Schema 1/2 stored pixels with positive Y downward. Convert once on load.
                Owner.GlobalPosition = schemaVersion < 3 ? new Vector3(x * 0.01f, -y * 0.01f, 0f) : new Vector3(x, y, z);
                Owner.GetComponent<CharacterMovementComponent3D>()?.SyncBodyToOwner();
            }

            if (state["rotation"] != null)
                Owner.GlobalRotation = schemaVersion < 3
                    ? new Vector3(0f, 0f, -state["rotation"].GetValue<float>())
                    : ReadVector(state["rotation"] as JsonObject);

            Owner.GetComponent<CharacterMovementComponent3D>()?.RestoreFacing(state["facing"]?.GetValue<int>() ?? 1);
            if (schemaVersion >= 3 && state["facing_direction"] is JsonObject direction)
                Owner.GetComponent<CharacterMovementComponent3D>()?.RestoreFacingDirection(ReadVector(direction));
            // 位置、旋转与朝向全部恢复后，再重置整棵角色树的插值历史。
            Owner.ResetPhysicsInterpolation();
            RestoreFlags(state["flags"] as JsonObject);
            JsonObject abilityState = state["abilities"] as JsonObject ?? state["skills"] as JsonObject;
            Owner.GetComponent<AbilitySystemComponent3D>()?.RestoreDurableState(abilityState);
        }

        private static JsonObject WriteVector(Vector3 v) => new() { ["x"] = v.X, ["y"] = v.Y, ["z"] = v.Z };
        private static Vector3 ReadVector(JsonObject v) => new(v?["x"]?.GetValue<float>() ?? 0f, v?["y"]?.GetValue<float>() ?? 0f, v?["z"]?.GetValue<float>() ?? 0f);

        private JsonObject CaptureFlags()
        {
            var flags = new JsonObject();
            foreach (var flag in PersistentFlags)
                flags[flag.Key] = flag.Value;
            return flags;
        }

        private void RestoreFlags(JsonObject flags)
        {
            if (flags == null)
                return;

            foreach (var property in flags)
                PersistentFlags[property.Key] = property.Value?.GetValue<bool>() ?? false;
        }
    }
}
