using System.Linq;
using Framework;
using Godot;

namespace GameLogic
{
    public class AbilityCameraShakeAction : GraphActionBase
    {
        public GraphActionComponentReference Camera { get; set; } = new();
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

            Component2D cameraComponent = null;
            string error = string.Empty;
            if (context?.ActionDependencyMode == GraphActionDependencyMode.Reusable)
            {
                cameraComponent = context.GameObject?.GetAllComponents()?.FirstOrDefault(value => value is ICharacterCameraShake2D);
                if (cameraComponent == null)
                    error = $"[{nameof(AbilityCameraShakeAction)}] Reusable action could not find a camera shake component on the current host.";
            }
            else if (!GraphActionComponentResolver.TryResolve(context, Camera, typeof(Component2D), nameof(AbilityCameraShakeAction), out cameraComponent, out error))
            {
                cameraComponent = null;
            }

            if (cameraComponent == null)
            {
                GD.PushError($"[AbilityCameraShakeAction] {error}");
                return;
            }
            ICharacterCameraShake2D camera = cameraComponent as ICharacterCameraShake2D;
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
            root.AddChild(Camera.CreateEditUI("Camera Component", context, () => { }));
            return root;
        }

        private static bool ShouldSkipForTimelineUpdate(GraphExecutionContext context)
        {
            FlowTimelineContext timeline = context?.GetUserData<FlowTimelineContext>();
            return timeline?.Phase == FlowTimelinePhase.Update;
        }
    }
}
