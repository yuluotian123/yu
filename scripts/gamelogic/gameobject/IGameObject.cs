using System;
using System.Collections.Generic;

namespace GameLogic
{
    public interface IGameObject
    {
        string PersistentId { get;set; }

        IReadOnlyList<IComponent> GetAllComponents();
        bool HasComponent(Type componentType);
        IComponent GetComponent(Type componentType);
        void RemoveComponent(Type componentType);
    }
}
