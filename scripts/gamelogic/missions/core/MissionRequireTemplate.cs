using Godot;

namespace GameLogic
{
    public abstract class MissionRequireTemplate : MissionRequire<object>
    {
        public abstract class MissionRequireTemplateHandle : MissionRequireHandle<object>
        {
            protected MissionRequireTemplateHandle(MissionRequireTemplate require) : base(require) { }
        }

        public MissionNode _missionNode;

        /// <summary>条件的描述文字</summary>
        public virtual string Description => "这是一个Condition";

        /// <summary>
        /// 返回该条件的参数编辑 UI，子类可覆盖以提供自定义界面。
        /// 仅编辑器使用。
        /// </summary>
        public virtual Control CreateEditUI() => new Control();

    }
}
