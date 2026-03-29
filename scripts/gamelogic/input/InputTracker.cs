using System.Collections.Generic;

namespace GameLogic.Input
{
    /// <summary>
    /// 统一的输入记录，包含按下时间和释放时间
    /// </summary>
    internal class InputRecord
    {
        public string Action;
        public double PressTime;      // 按下时间戳
        public double? ReleaseTime;   // 释放时间戳（null 表示仍在按下）
        public double HoldDuration;   // 持续时间（实时更新）
        
        public bool IsPressed => ReleaseTime == null;
    }

    /// <summary>
    /// 统一的输入追踪器（整合 InputBuffer 和 HoldTime 功能）
    /// 解决了原有的时序问题：
    /// - 按键释放时不立即删除记录，而是标记释放时间
    /// - 基于时间戳进行过期清理，确保 GetHoldTime() 在 IsJustReleased() 同帧可用
    /// </summary>
    public class InputTracker
    {
        private readonly Dictionary<string, InputRecord> _records = new Dictionary<string, InputRecord>();
        private readonly List<string> _recordsToClean = new List<string>();
        
        private const float MaxRecordAge = 1f;  // 释放后最大保留时间
        private const float MaxHoldTime = 10f;  // 最大持续时间限制

        /// <summary>
        /// 记录按键按下
        /// </summary>
        public void RecordPress(string action, double currentTime)
        {
            if (_records.TryGetValue(action, out var record))
            {
                // 重新按下，重置记录
                record.PressTime = currentTime;
                record.ReleaseTime = null;
                record.HoldDuration = 0;
            }
            else
            {
                _records[action] = new InputRecord
                {
                    Action = action,
                    PressTime = currentTime,
                    ReleaseTime = null,
                    HoldDuration = 0
                };
            }
        }

        /// <summary>
        /// 记录按键释放
        /// </summary>
        public void RecordRelease(string action, double currentTime)
        {
            if (_records.TryGetValue(action, out var record) && record.IsPressed)
            {
                record.ReleaseTime = currentTime;
                // HoldDuration 已在 Update 中更新，无需重新计算
            }
        }

        /// <summary>
        /// 更新持续时间并清理过期记录
        /// </summary>
        public void Update(double currentTime, double deltaTime)
        {
            _recordsToClean.Clear();

            foreach (var kvp in _records)
            {
                var record = kvp.Value;

                if (record.IsPressed)
                {
                    // 仍在按下，更新持续时间
                    record.HoldDuration = System.Math.Min(
                        record.HoldDuration + deltaTime, 
                        MaxHoldTime
                    );
                }
                else if (record.ReleaseTime.HasValue)
                {
                    // 已释放，检查是否过期
                    double timeSinceRelease = currentTime - record.ReleaseTime.Value;
                    if (timeSinceRelease > MaxRecordAge)
                    {
                        _recordsToClean.Add(kvp.Key);
                    }
                }
            }

            // 清理过期记录
            foreach (var action in _recordsToClean)
            {
                _records.Remove(action);
            }
        }

        /// <summary>
        /// 检查是否在缓冲时间内按下（输入缓冲功能）
        /// </summary>
        public bool IsBuffered(string action, float bufferTime, double currentTime)
        {
            if (_records.TryGetValue(action, out var record))
            {
                double elapsed = currentTime - record.PressTime;
                return elapsed <= bufferTime;
            }
            return false;
        }

        /// <summary>
        /// 获取持续按下时间
        /// 关键：即使按键已释放，在同一帧内仍可获取正确的 HoldDuration
        /// </summary>
        public float GetHoldTime(string action)
        {
            if (_records.TryGetValue(action, out var record))
            {
                return (float)record.HoldDuration;
            }
            return 0f;
        }

        /// <summary>
        /// 消费缓冲的动作（使其失效）
        /// </summary>
        public void Consume(string action)
        {
            _records.Remove(action);
        }

        /// <summary>
        /// 清空所有记录
        /// </summary>
        public void Clear()
        {
            _records.Clear();
        }
    }
}
