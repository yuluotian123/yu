using System.Linq;
using Godot;

namespace GameLogic
{
    [GlobalClass]
    public partial class CharacterGraphComponent2D : Component2D
    {
        [Export] public CharacterGraphAsset CharacterGraph { get; set; }

        public override int Priority => ComponentPriority.State;
        public CharacterGraphRuntime Runtime { get; private set; }

        public override void OnInit()
        {
            if (CharacterGraph == null)
            {
                GD.PushWarning("[CharacterGraphComponent2D] CharacterGraph is not assigned.");
                return;
            }

            ICharacterInputProvider input = Owner?.GetAllComponents()
                .OfType<ICharacterInputProvider>()
                .FirstOrDefault();
            Runtime = new CharacterGraphRuntime(CharacterGraph, Owner, input);
        }

        public override void OnUpdate(double delta)
        {
            if (Runtime == null)
                return;
            GraphComponentBindingRuntime.SyncFromComponents(Runtime.Context);
            Runtime.Update(delta, physics: false);
            GraphComponentBindingRuntime.SyncToComponents(Runtime.Context);
        }

        public override void OnPhysicsUpdate(double delta)
        {
            if (Runtime == null)
                return;
            GraphComponentBindingRuntime.SyncFromComponents(Runtime.Context);
            Runtime.Update(delta, physics: true);
            GraphComponentBindingRuntime.SyncToComponents(Runtime.Context);
        }

        public override void OnDestroy()
        {
            Runtime?.Stop();
            Runtime = null;
        }

        public void PublishEvent(string eventName) => Runtime?.PublishEvent(eventName);
    }
}
