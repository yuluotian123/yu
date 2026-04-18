namespace GameLogic
{
    public class PlayerState
    {
        public SerializableGameObject2D PlayerController { get; set; }

        public SelectableManagerComponent GetSelectableManager()
        {
            return PlayerController?.GetComponent<SelectableManagerComponent>();
        }
        public PlayerArmyComponent GetArmyComponent()
        {
            return PlayerController?.GetComponent<PlayerArmyComponent>();
        }

    }
}