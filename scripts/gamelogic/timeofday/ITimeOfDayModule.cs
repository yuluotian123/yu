using Godot;

namespace GameLogic;

public interface ITimeOfDayModule
{
    WorldClock Clock { get; }
    bool HasEnvironment { get; }
    void Attach(Node owner, TimeOfDayProfile profile);
    void Detach(Node owner);
}
