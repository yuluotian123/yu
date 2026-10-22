using System.Threading;
using System.Threading.Tasks;
using Godot;
namespace GameLogic;

public interface IWorldModule
{
    WorldCatalog Catalog {get;}
    bool IsOpen {get;}
    bool IsSwitching {get;}
    string CurrentLevelId {get;}
    string Status {get;}
    string LastError {get;}
    float Progress {get;}
    GameObject3D Player {get;}
    WorldLevelContextComponent3D CurrentContext {get;}
    Task<bool> OpenAsync(Node parent,WorldDefinition definition,bool resume=true,string slot="default",CancellationToken cancellation=default);
    Task<bool> SwitchAsync(string levelId,string entry="",CancellationToken cancellation=default);
    void Close();
    void CancelTransition();
}
