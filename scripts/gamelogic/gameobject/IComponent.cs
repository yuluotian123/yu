namespace GameLogic
{
    public static class ComponentPriority
    {
        public const int Input = 100;
        public const int AI = 100;
        public const int State = 90;
        public const int Animation = 80;
        public const int Combat = 60;
        public const int Movement = 50;
        public const int Physics = 45;
        public const int Health = 40;
        public const int Motor = 35;
        public const int Interaction = 30;
        public const int VFX = 10;
        public const int Default = 0;
    }

    public interface IComponent
    {
        int Priority { get; }
        bool IsActive { get; set; }

        void OnInit();
        void OnUpdate(double delta);
        void OnPhysicsUpdate(double delta);
        void OnDestroy();
    }
}
