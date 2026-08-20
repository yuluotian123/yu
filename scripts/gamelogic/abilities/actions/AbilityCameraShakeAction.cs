using Framework;
using Godot;

namespace GameLogic
{
    public class AbilityCameraShakeAction : GraphActionBase
    {
        public string ShakeProfilePath { get; set; } = string.Empty;

        public override string Description
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ShakeProfilePath))
                    return "Camera Shake";

                return $"Camera Shake [{ShakeProfilePath.GetFile().GetBaseName()}]";
            }
        }

        public override void Execute(GraphExecutionContext context)
        {
            if (ShouldSkipForTimelineUpdate(context))
                return;

            GameObject2D owner = AbilityActionRuntimeHelper.GetGameObject(context);
            ICharacterCameraShake2D camera = owner?.GetComponent(typeof(ICharacterCameraShake2D)) as ICharacterCameraShake2D;
            if (camera == null || string.IsNullOrWhiteSpace(ShakeProfilePath))
                return;

            CameraShakeProfile profile = ModuleSystem
                .GetModule<IResourceModule>()
                .LoadAssetOnce<CameraShakeProfile>(ShakeProfilePath);
            camera.Shake(profile);
        }

        public override Control CreateEditUI(GraphEditorContext context)
        {
            var root = new VBoxContainer();
            root.AddChild(GraphEditorUi.BuildLineEditRow(
                "Profile Path",
                ShakeProfilePath,
                "res://assets/camera_shakes/light_hit.tres",
                value => ShakeProfilePath = value));
            return root;
        }

        private static bool ShouldSkipForTimelineUpdate(GraphExecutionContext context)
        {
            FlowTimelineContext timeline = context?.GetUserData<FlowTimelineContext>();
            return timeline?.Phase == FlowTimelinePhase.Update;
        }
    }
}
