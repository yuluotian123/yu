using System.Collections.Generic;

namespace GameLogic.Input
{
    /// <summary>
    /// 输入缓冲记录。
    /// 记录动作按下的时间戳，用于实现输入缓冲（Input Buffer）。
    /// </summary>
    internal class InputBufferEntry
    {
        public string Action;
        public double Timestamp;
    }

    /// <summary>
    /// 输入缓冲管理器。
    /// 动作游戏核心功能：允许玩家提前输入，在一定时间窗口内仍然有效。
    /// </summary>
    public class InputBuffer
    {
        private readonly List<InputBufferEntry> _buffer = new List<InputBufferEntry>();
        private const int MaxBufferSize = 32;

        /// <summary>
        /// 记录动作按下。
        /// </summary>
        public void RecordPress(string action, double currentTime)
        {
            // 移除旧的同名动作
            _buffer.RemoveAll(e => e.Action == action);

            // 添加新记录
            _buffer.Add(new InputBufferEntry
            {
                Action = action,
                Timestamp = currentTime
            });

            // 限制缓冲区大小
            if (_buffer.Count > MaxBufferSize)
            {
                _buffer.RemoveAt(0);
            }
        }

        /// <summary>
        /// 检查动作是否在缓冲时间内。
        /// </summary>
        public bool IsBuffered(string action, float bufferTime, double currentTime)
        {
            foreach (var entry in _buffer)
            {
                if (entry.Action == action)
                {
                    double elapsed = currentTime - entry.Timestamp;
                    return elapsed <= bufferTime;
                }
            }
            return false;
        }

        /// <summary>
        /// 消费缓冲的动作（使其失效）。
        /// </summary>
        public void Consume(string action)
        {
            _buffer.RemoveAll(e => e.Action == action);
        }

        /// <summary>
        /// 清理过期的缓冲记录。
        /// </summary>
        public void CleanExpired(double currentTime, float maxBufferTime)
        {
            _buffer.RemoveAll(e => currentTime - e.Timestamp > maxBufferTime);
        }

        /// <summary>
        /// 清空所有缓冲。
        /// </summary>
        public void Clear()
        {
            _buffer.Clear();
        }
    }
}
