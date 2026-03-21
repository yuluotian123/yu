using System;
using System.Collections.Generic;
using Godot;

namespace Framework
{
    /// <summary>
    /// AI生成：仿照Unity Time设计的游戏时间管理类，提供类似 Unity Time 的接口，内部使用 Godot Engine 的时间系统实现。
    /// </summary>
    public class GameTime
    {
        // ===== 类似 Unity Time 的公开属性 =====
        /// <summary>
        /// 类似 Unity Time.time
        /// 游戏开始后的逻辑时间，受 timeScale 影响
        /// </summary>
        public double Time { get; private set; }
        /// <summary>
        /// 类似 Unity Time.deltaTime
        /// 当前普通帧 delta，受 timeScale 影响
        /// </summary>
        public double DeltaTime { get; private set; }
        /// <summary>
        /// 类似 Unity Time.unscaledDeltaTime
        /// 当前普通帧真实 delta，不受 timeScale 影响
        /// </summary>
        public double UnscaledDeltaTime { get; private set; }
        /// <summary>
        /// 类似 Unity Time.unscaledTime
        /// 不受 timeScale 影响的累计时间
        /// </summary>
        public double UnscaledTime { get; private set; }
        /// <summary>
        /// 类似 Unity Time.realtimeSinceStartup
        /// 程序启动后的真实运行时间
        /// </summary>
        public double RealtimeSinceStartup { get; private set; }
        /// <summary>
        /// 类似 Unity Time.fixedDeltaTime
        /// 固定物理帧步长
        /// </summary>
        public double FixedDeltaTime { get; private set; }
        /// <summary>
        /// 类似 Unity Time.fixedTime
        /// 物理时间累计，受 timeScale 影响
        /// </summary>
        public double FixedTime { get; private set; }
        /// <summary>
        /// 类似 Unity Time.fixedUnscaledTime
        /// 不受 timeScale 影响的物理累计时间
        /// </summary>
        public double FixedUnscaledTime { get; private set; }
        /// <summary>
        /// 类似 Unity Time.smoothDeltaTime
        /// 平滑后的 delta
        /// </summary>
        public double SmoothDeltaTime { get; private set; }
        /// <summary>
        /// 类似 Unity Time.frameCount
        /// 普通帧数
        /// </summary>
        public ulong FrameCount => Engine.GetProcessFrames();
        /// <summary>
        /// 物理帧数
        /// </summary>
        public ulong PhysicsFrameCount => Engine.GetPhysicsFrames();
        /// <summary>
        /// 类似 Unity Time.inFixedTimeStep
        /// 当前是否正处于 PhysicsProcess 调用中
        /// </summary>
        public bool InFixedTimeStep { get; private set; }
        /// <summary>
        /// 类似 Unity Time.timeScale
        /// 实际读写 Godot Engine.TimeScale
        /// </summary>
        public double TimeScale
        {
            get => Engine.TimeScale;
            set => Engine.TimeScale = value;
        }
        // ===== 内部字段 =====
        private double _startRealtime;
        private double _lastRealtime;
        private readonly Queue<double> _deltaSamples = new();
        private const int SmoothSampleCount = 10;

        public GameTime()
        {
            _startRealtime = GetRealtimeSeconds();
            _lastRealtime = _startRealtime;
            FixedDeltaTime = 1.0 / (double)ProjectSettings.GetSetting("physics/common/physics_ticks_per_second");
            RealtimeSinceStartup = 0.0;
            UnscaledTime = 0.0;
            Time = 0.0;
            FixedTime = 0.0;
            FixedUnscaledTime = 0.0;
            DeltaTime = 0.0;
            UnscaledDeltaTime = 0.0;
            SmoothDeltaTime = 0.0;
            InFixedTimeStep = false;
        }

        public void OnProcess(double delta)
        {
            InFixedTimeStep = false;
            // 真实时间差，不受 Engine.TimeScale 影响
            double now = GetRealtimeSeconds();
            double realDelta = now - _lastRealtime;
            _lastRealtime = now;
            // RealtimeSinceStartup / UnscaledTime
            RealtimeSinceStartup = now - _startRealtime;
            UnscaledDeltaTime = realDelta;
            UnscaledTime += realDelta;
            // 逻辑时间（受 timeScale 影响）
            DeltaTime = delta;
            Time += delta;
            // 平滑 delta
            PushDeltaSample(delta);
            SmoothDeltaTime = CalculateSmoothDelta();
        }

        public void OnPhysicsProcess(double delta)
        {
            InFixedTimeStep = true;
            FixedDeltaTime = delta;
            FixedTime += delta;
            // 物理未缩放时间
            // 由于 Godot 没直接提供 fixed unscaled delta，
            // 这里使用 physics ticks per second 和当前 timeScale 估算并不准确。
            // 更可靠的方式是直接用真实时间差估计。
            // 为了避免 _PhysicsProcess 和 _Process 竞争真实时间，我们采用理论物理步长估算真实步长：
            double currentTimeScale = Math.Max(Engine.TimeScale, 0.000001);
            double unscaledFixedDelta = delta / currentTimeScale;
            FixedUnscaledTime += unscaledFixedDelta;
            InFixedTimeStep = false;
        }

        /// <summary>
        /// 获取真实运行时间（秒）
        /// 等价于 Godot Time.GetTicksUsec() / 1_000_000.0
        /// </summary>
        public static double GetRealtimeSeconds()
        {
            return Godot.Time.GetTicksUsec() / 1_000_000.0;
        }
        /// <summary>
        /// 手动重置计时器
        /// </summary>
        public void ResetTimers()
        {
            _startRealtime = GetRealtimeSeconds();
            _lastRealtime = _startRealtime;
            RealtimeSinceStartup = 0.0;
            UnscaledTime = 0.0;
            Time = 0.0;
            FixedTime = 0.0;
            FixedUnscaledTime = 0.0;
            DeltaTime = 0.0;
            UnscaledDeltaTime = 0.0;
            SmoothDeltaTime = 0.0;
            _deltaSamples.Clear();
        }
        private void PushDeltaSample(double delta)
        {
            _deltaSamples.Enqueue(delta);
            while (_deltaSamples.Count > SmoothSampleCount)
            {
                _deltaSamples.Dequeue();
            }
        }
        private double CalculateSmoothDelta()
        {
            if (_deltaSamples.Count == 0)
                return 0.0;
            double sum = 0.0;
            foreach (var d in _deltaSamples)
            {
                sum += d;
            }
            return sum / _deltaSamples.Count;
        }

    }
}