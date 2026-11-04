using System;

namespace GameLogic;

public interface IDialogueModule
{
    DialogueSession Active { get; }
    bool IsActive { get; }
    string LastError { get; }
    event Action<DialogueSession> Finished;
    bool TryStart(GameObject3D npc, GameObject3D player, string entryId = "talk");
    bool TryStart(DialogueGraphAsset graph, GameObject3D npc, GameObject3D player);
    bool TryReadMemory(GameObject3D npc, GameObject3D participant, string key, out bool value);
    bool WriteMemory(GameObject3D npc, GameObject3D participant, string key, bool value);
    void Cancel();
    void ResetSession();
}
