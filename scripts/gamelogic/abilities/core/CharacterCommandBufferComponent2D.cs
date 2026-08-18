using Godot;

namespace GameLogic
{
    [GlobalClass]
    public partial class CharacterCommandBufferComponent2D : Component2D
    {
        public override int Priority => ComponentPriority.Input;

        private CharacterCommand2D _pending = CharacterCommand2D.None;
        private CharacterActionRequest _pendingAction;
        private int _sourcePriority = int.MinValue;

        public CharacterCommand2D Pending => _pending;
        public CharacterActionRequest PendingAction => _pendingAction;

        public void Submit(CharacterCommand2D command, int sourcePriority = 0)
        {
            if (sourcePriority < _sourcePriority)
                return;

            _pending = command;
            _sourcePriority = sourcePriority;
        }

        public CharacterCommand2D Consume()
        {
            CharacterCommand2D command = _pending;
            _pending = CharacterCommand2D.None;
            _sourcePriority = int.MinValue;
            return command;
        }

        public void SubmitAction(CharacterActionRequest request)
        {
            if (!request.IsValid ||
                (_pendingAction.IsValid && request.Priority < _pendingAction.Priority))
                return;

            _pendingAction = request;
        }

        public CharacterActionRequest ConsumeAction()
        {
            CharacterActionRequest request = _pendingAction;
            _pendingAction = default;
            return request;
        }

        public override void OnDestroy()
        {
            _pending = CharacterCommand2D.None;
            _pendingAction = default;
            _sourcePriority = int.MinValue;
        }
    }
}
