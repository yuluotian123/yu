using System;
using System.Linq;
using Godot;

namespace GameLogic
{
    // Owned and updated by AbilitySystemComponent3D; Timeline actions control its lifetime.
    public sealed class WeaponTrailPlayer3D : IDisposable
    {
        private readonly GameObject3D Owner;
        private readonly PackedScene EffectScene;
        public string ScenePath => EffectScene.ResourcePath;
        public bool IsPlaying => _trail != null;
        public bool IsEmitting => _emitting;
        public int PlayedTrails { get; private set; }
        public int AttackFacing { get; private set; } = 1;
        public int EmittedBursts { get; private set; }
        public int EmittedAccents { get; private set; }

        private const int PoolSize = 24;
        private const double SampleInterval = 1.0 / 120;
        private const double TailLifetime = 0.18;
        private WeaponEquipmentComponent3D _equipment;
        private CharacterAnimationComponent3D _animation;
        private CharacterMovementComponent3D _movement;
        private Node3D _visual;
        private readonly Node3D[] _bursts = new Node3D[PoolSize];
        private readonly CpuParticles3D[] _cores = new CpuParticles3D[PoolSize];
        private readonly CpuParticles3D[] _sparks = new CpuParticles3D[PoolSize];
        private readonly CpuParticles3D[] _finishes = new CpuParticles3D[PoolSize];
        private readonly double[] _expires = new double[PoolSize];
        private string _requestKey = string.Empty;
        private bool _emitting;
        private WeaponAttackTrail _trail;
        private double[] _frameTimes;
        private double[] _keyTimes;
        private double _nextSample;
        private int _poolIndex;
        private bool _finishHandled;

        public WeaponTrailPlayer3D(GameObject3D owner, PackedScene scene)
        {
            Owner = owner;
            EffectScene = scene;
            _equipment = Owner.GetComponent<WeaponEquipmentComponent3D>();
            _animation = Owner.GetComponent<CharacterAnimationComponent3D>();
            _movement = Owner.GetComponent<CharacterMovementComponent3D>();
            if (_equipment == null || _animation?.Sprite == null || EffectScene == null) return;
            _visual = new Node3D { Name = "WeaponTrail", Visible = false };
            Owner.AddChild(_visual);
            for (int i = 0; i < PoolSize; i++)
            {
                var burst = EffectScene.Instantiate<Node3D>();
                _visual.AddChild(burst);
                burst.TopLevel = true;
                burst.Visible = false;
                _bursts[i] = burst;
                _cores[i] = burst.GetNode<CpuParticles3D>("Blade");
                _sparks[i] = burst.GetNode<CpuParticles3D>("Sparks");
                _finishes[i] = burst.GetNode<CpuParticles3D>("Finish");
            }
        }

        public bool PlayTrail(WeaponAttackTrail trail, string requestKey)
        {
            if (trail?.IsValid != true || !GodotObject.IsInstanceValid(_visual) || string.IsNullOrEmpty(requestKey)) return false;
            Stop();
            if (!SetTrail(trail)) return false;
            _requestKey = requestKey;
            _emitting = true;
            AttackFacing = _movement?.Facing ?? 1;
            EmittedBursts = 0;
            EmittedAccents = 0;
            PlayedTrails++;
            _visual.Visible = true;
            return true;
        }

        public void EndTrail(string requestKey, bool cancelled)
        {
            if (_requestKey != requestKey) return;
            if (cancelled) Stop();
            else _emitting = false;
        }

        private bool SetTrail(WeaponAttackTrail trail)
        {
            SpriteFrames frames = _animation.Sprite.SpriteFrames;
            if (!frames.HasAnimation(trail.Animation)) return false;
            int count = frames.GetFrameCount(trail.Animation);
            if (trail.Frames[0] < 0 || trail.Frames[^1] >= count) return false;
            _frameTimes = new double[count + 1];
            for (int i = 0; i < count; i++)
                _frameTimes[i + 1] = _frameTimes[i] + frames.GetFrameDuration(trail.Animation, i) / frames.GetAnimationSpeed(trail.Animation);
            _keyTimes = trail.Frames.Select(FrameTime).ToArray();
            for (int i = 1; i < _keyTimes.Length; i++) if (_keyTimes[i] <= _keyTimes[i - 1]) return false;
            _trail = trail;
            _finishHandled = false;
            _nextSample = _keyTimes[0];
            return true;
        }

        public void Update(double delta)
        {
            if (_trail == null) return;
            bool hasParticles = false;
            for (int i = 0; i < PoolSize; i++)
            {
                _expires[i] -= delta;
                if (_expires[i] <= 0) _bursts[i].Visible = false;
                else hasParticles = true;
            }
            if (!_emitting)
            {
                if (!hasParticles) Stop();
                return;
            }
            AnimatedSprite3D sprite = _animation.Sprite;
            if (sprite.Animation != _trail.Animation) return;
            double time = FrameTime(sprite.Frame + sprite.FrameProgress);
            // Fill gaps at low render rates; emission follows the animation, not wall time.
            while (_nextSample <= time && _nextSample <= _keyTimes[^1])
            {
                if (time - _nextSample < TailLifetime) EmitBlade(_nextSample, sprite);
                _nextSample += SampleInterval;
            }
            // One short punctuation at the stop, then let it decay through the held pose.
            if (!_finishHandled && time >= _keyTimes[^1])
            {
                _finishHandled = true;
                if (time - _keyTimes[^1] < TailLifetime) EmitBlade(_keyTimes[^1], sprite, true);
            }
        }

        private double FrameTime(float frame)
        {
            int index = Mathf.Clamp((int)frame, 0, _frameTimes.Length - 2);
            return Mathf.Lerp(_frameTimes[index], _frameTimes[index + 1], Mathf.Clamp(frame - index, 0, 1));
        }

        private Vector4 BladePose(double time)
        {
            int key = 0;
            while (key + 1 < _keyTimes.Length - 1 && _keyTimes[key + 1] < time) key++;
            float weight = (float)((time - _keyTimes[key]) / (_keyTimes[key + 1] - _keyTimes[key]));
            return _trail.BladePoses[key].Lerp(_trail.BladePoses[key + 1], weight);
        }

        private void EmitBlade(double time, AnimatedSprite3D sprite, bool finishing = false)
        {
            Vector4 pose = BladePose(time);
            Vector4 previous = BladePose(Math.Max(_keyTimes[0], time - SampleInterval));
            var profile = _equipment.CurrentWeapon.AnimationProfile;
            Vector2 size = profile.FrameSizeFor(_trail.Animation);
            float scale = sprite.PixelSize * profile.VisualScale;
            Vector3 Pixel(float x, float y) => new((x - size.X * .5f) * scale * AttackFacing, (size.Y * .5f - y) * scale, .015f);
            Vector3 start = Pixel(pose.X, pose.Y);
            Vector3 end = Pixel(pose.Z, pose.W);
            // Fill the swept outer edge, not separated full-blade afterimages.
            var points = new Vector3[64];
            for (int i = 0; i < points.Length; i++)
            {
                // Even coverage without visible rows of identical diamonds.
                float along = (i + .5f) / points.Length;
                float across = Mathf.PosMod(i * .618034f, 1f);
                Vector4 swept = previous.Lerp(pose, along);
                Vector3 bladeBase = Pixel(swept.X, swept.Y);
                Vector3 bladeTip = Pixel(swept.Z, swept.W);
                points[i] = bladeBase.Lerp(bladeTip, .78f + .22f * across);
            }
            Node3D burst = _bursts[_poolIndex];
            Camera3D camera = Owner.GetViewport().GetCamera3D();
            Vector3 right = camera?.GlobalBasis.X ?? Vector3.Right;
            right.Y = 0;
            right = right.LengthSquared() > .0001f ? right.Normalized() : Vector3.Right;
            burst.GlobalTransform = new Transform3D(new Basis(right, Vector3.Up, right.Cross(Vector3.Up)), sprite.GlobalPosition);
            burst.Visible = true;
            CpuParticles3D core = _cores[_poolIndex];
            CpuParticles3D sparks = _sparks[_poolIndex];
            core.EmissionPoints = points;
            sparks.EmissionPoints = new[] { start.Lerp(end, .6f), end };
            Vector3 tipTravel = end - Pixel(previous.Z, previous.W);
            sparks.Direction = tipTravel.LengthSquared() > .000001f
                ? tipTravel.Normalized() : (end - start).Normalized();
            core.Restart();
            core.Emitting = true;
            sparks.Restart();
            sparks.Emitting = true;
            CpuParticles3D finish = _finishes[_poolIndex];
            finish.Emitting = false;
            if (finishing)
            {
                Vector4 first = _trail.BladePoses[0];
                Vector3 travel = end - Pixel(first.Z, first.W);
                finish.Direction = travel.LengthSquared() > .000001f ? travel.Normalized() : (end - start).Normalized();
                finish.EmissionPoints = new[] { start.Lerp(end, .92f), end };
                float strength = Mathf.Clamp(_trail.FinishStrength, .5f, 1.5f);
                finish.Amount = Mathf.RoundToInt(18 * strength);
                finish.InitialVelocityMin = 1.1f * strength;
                finish.InitialVelocityMax = 2.2f * strength;
                finish.Restart();
                finish.Emitting = true;
                EmittedAccents++;
            }
            _expires[_poolIndex] = TailLifetime;
            _poolIndex = (_poolIndex + 1) % PoolSize;
            EmittedBursts++;
        }

        public void Stop()
        {
            _requestKey = string.Empty;
            _emitting = false;
            _trail = null;
            if (GodotObject.IsInstanceValid(_visual)) _visual.Visible = false;
            for (int i = 0; i < PoolSize; i++)
            {
                if (!GodotObject.IsInstanceValid(_bursts[i])) continue;
                _bursts[i].Visible = false;
                _cores[i].Emitting = _sparks[i].Emitting = _finishes[i].Emitting = false;
                _expires[i] = 0;
            }
        }

        public void Dispose()
        {
            Stop();
            if (GodotObject.IsInstanceValid(_visual))
            {
                _visual.GetParent()?.RemoveChild(_visual);
                _visual.QueueFree();
            }
            _visual = null;
            _equipment = null;
            _animation = null;
            _movement = null;
            _frameTimes = _keyTimes = null;
        }
    }
}
