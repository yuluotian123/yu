using System.Collections.Generic;
using GameLogic;

public class PlayerState
{
    public GameObject2D PlayerController { get; set; }
    
    public List<GameObject2D> PlayerUnits
    {
        get
        {
            return GetArmyComponent()?.Units as List<GameObject2D> ?? new List<GameObject2D>();
        }
    }

    public SelectableManagerComponent GetSelectableManager()
    {
        return PlayerController?.GetComponent<SelectableManagerComponent>();
    }
    public PlayerArmyComponent GetArmyComponent()
    {
        return PlayerController?.GetComponent<PlayerArmyComponent>();
    }

}