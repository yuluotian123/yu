using System.Text.Json.Nodes;
using Framework;
using Godot;

namespace GameLogic
{
    [GlobalClass]
    public partial class CharacterPersistenceComponent2D : Component2D, ISaveSection
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
        public int SchemaVersion => 1;

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
                ["position"] = new JsonObject { ["x"] = Owner.GlobalPosition.X, ["y"] = Owner.GlobalPosition.Y },
                ["rotation"] = Owner.GlobalRotation,
                ["facing"] = Owner.GetComponent<CharacterMovementComponent2D>()?.Facing ?? 1,
                ["flags"] = CaptureFlags()
            };

            SkillManagerComponent2D skills = Owner.GetComponent<SkillManagerComponent2D>();
            if (skills != null)
                state["skills"] = skills.CaptureDurableState();

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
                Owner.GlobalPosition = new Vector2(x, y);
                Owner.GetComponent<CharacterMovementComponent2D>()?.SyncBodyToOwner();
            }

            if (state["rotation"] != null)
                Owner.GlobalRotation = state["rotation"].GetValue<float>();

            Owner.GetComponent<CharacterMovementComponent2D>()?.RestoreFacing(state["facing"]?.GetValue<int>() ?? 1);
            RestoreFlags(state["flags"] as JsonObject);
            Owner.GetComponent<SkillManagerComponent2D>()?.RestoreDurableState(state["skills"] as JsonObject);
        }

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
