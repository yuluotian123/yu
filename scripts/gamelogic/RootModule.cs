using System;
using Framework;
using Framework.UI;
using GameLogic.Input;
using GameLogic.Mission;
using GameLogic.Save;
using Godot;


/// <summary>
/// 游戏根模块，负责管理游戏的全局状态和核心系统。
/// </summary>
public sealed partial class RootModule : Node
{
    public static RootModule Instance { get; private set; }

    public GameTime GameTime { get; private set; }


    private const int DEFAULT_DPI = 96; // default windows dpi

    private float _gameSpeedBeforePause = 1f;

    [Export]
    private int frameRate = 120;

    [Export]
    private float gameSpeed = 1f;

    [Export]
    public Settings settings;

    /// <summary>
    /// 获取或设置游戏帧率。
    /// </summary>
    public int FrameRate
    {
        get => frameRate;
        set => Engine.MaxFps = frameRate = value;
    }

    /// <summary>
    /// 获取或设置游戏速度。
    /// </summary>
    public float GameSpeed
    {
        get => gameSpeed;
        set => GameTime.TimeScale = gameSpeed = value >= 0f ? value : 0f;
    }

    /// <summary>
    /// 获取游戏是否暂停。
    /// </summary>
    public bool IsGamePaused => gameSpeed <= 0f;

    /// <summary>
    /// 获取是否正常游戏速度。
    /// </summary>
    public bool IsNormalGameSpeed => Math.Abs(gameSpeed - 1f) < 0.01f;


    public override void _Ready()
    {
        Instance = this;

        Debugger.Info("======= Init CommonVariables =======");
        GameTime = new GameTime();
        Engine.MaxFps = frameRate;
        Engine.TimeScale = gameSpeed;

        Debugger.Info("======= Init Module =======");

        GameTime = new GameTime();

        //初始化各个模块，现在没有先后次序区别
        ModuleSystem.GetModule<IFsmModule>();
        ModuleSystem.GetModule<IUIModule>();
        ModuleSystem.GetModule<ISaveModule>();
        ModuleSystem.GetModule<IMissionModule>();
        ModuleSystem.GetModule<IEventModule>();
        ModuleSystem.GetModule<IResourceModule>();
        ModuleSystem.GetModule<IConfigModule>();
        ModuleSystem.GetModule<IInputModule>();
        ModuleSystem.GetModule<IObjectPoolModule>();

        var procedureModule = ModuleSystem.GetModule<IProcedureModule>();

        Debugger.Info("======= Entrance GameEntry =======");
        ProcedureBase[] gameProcedures = [new PreloadProcedure(), new MainMenuProcedure(), new LevelProcedure()];
        procedureModule.Initialize(ModuleSystem.GetModule<IFsmModule>(), gameProcedures);
        procedureModule.StartProcedure<MainMenuProcedure>();
    }

    public override void _Process(double delta)
    {
        GameTime.OnProcess(delta);
        ModuleSystem.Process(GameTime.DeltaTime, GameTime.UnscaledDeltaTime);
    }

    public override void _PhysicsProcess(double delta)
    {
        GameTime.OnPhysicsProcess(delta);
    }

    public override void _ExitTree()
    {
        ModuleSystem.Shutdown();

        Debugger.Info("======= Exit GameEntry =======");

        if (Instance == this) Instance = null;
    }

    internal void Shutdown()
    {
        QueueFree();
    }

    /// <summary>
    /// 暂停游戏。
    /// </summary>
    public void PauseGame()
    {
        if (IsGamePaused)
        {
            return;
        }

        _gameSpeedBeforePause = GameSpeed;
        GameSpeed = 0f;
    }

    /// <summary>
    /// 恢复游戏。
    /// </summary>
    public void ResumeGame()
    {
        if (!IsGamePaused)
        {
            return;
        }

        GameSpeed = _gameSpeedBeforePause;
    }

    /// <summary>
    /// 重置为正常游戏速度。
    /// </summary>
    public void ResetNormalGameSpeed()
    {
        if (IsNormalGameSpeed)
        {
            return;
        }

        GameSpeed = 1f;
    }






}
