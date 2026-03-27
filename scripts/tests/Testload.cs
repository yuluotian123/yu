using System;
using Godot;
public partial class Testload : Node
{
    private PackedScene _levelPacked;
    private Node _levelInstance;
    public override void _Ready()
    {
        _levelPacked = GD.Load<PackedScene>("res://assets/minigame/scenes/level.tscn");
        _levelInstance = _levelPacked.Instantiate();
        AddChild(_levelInstance);
        GD.Print($"Loaded PackedScene RefCount = {_levelPacked.GetReferenceCount()}");
    }
    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed("ui_cancel"))
        {
            if (_levelInstance != null)
            {
                _levelInstance.QueueFree();
                _levelInstance = null;
            }
            _levelPacked = null;
            GD.Print("Released local references.");

            //GC.Collect();
        }
    }
}