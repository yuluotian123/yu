using Framework;
using Godot;

namespace GameLogic
{
    [GraphCallable("Camera Shake", "动画与表现 / 镜头", GraphCallableUsage.All, ChineseName = "镜头震动")]
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

            if (string.IsNullOrWhiteSpace(ShakeProfilePath))
                return;

            CameraShakeProfile profile = ModuleSystem
                .GetModule<IResourceModule>()
                .LoadAssetOnce<CameraShakeProfile>(ShakeProfilePath);
            ModuleSystem.GetModule<ICameraModule>().Shake(profile);
        }

        public override Control CreateEditUI(GraphEditorContext context)
        {
            var root = new VBoxContainer();
            root.AddChild(GraphEditorUi.BuildLineEditRow(
                "Profile Path",
                ShakeProfilePath,
                "res://assets/camera_shakes/light_hit.tres",
                value => ShakeProfilePath = value));
            root.AddChild(new Label { Text = "使用全局相机，无需绑定角色组件。" });
            return root;
        }

        private static bool ShouldSkipForTimelineUpdate(GraphExecutionContext context)
        {
            FlowTimelineContext timeline = context?.GetUserData<FlowTimelineContext>();
            return timeline?.Phase == FlowTimelinePhase.Update;
        }
    }
}
