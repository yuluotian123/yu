using System;
using System.Linq;
using Godot;

namespace GameLogic
{
    [Tool, GlobalClass]
    public partial class WeaponAttackEffectComponent3D : Component3D
    {
        [Export] public PackedScene EffectScene { get; set; }
        public override int Priority => ComponentPriority.VFX - 2;
        public bool IsPlaying => _runtime != null;
        public int AttackFacing { get; private set; } = 1;
        public int EmittedBursts { get; private set; }

        private const int PoolSize = 24;
        private const double SampleInterval = 1.0 / 120;
        private const double TailLifetime = 0.18;
        private WeaponEquipmentComponent3D _equipment;
        private AbilitySystemComponent3D _abilities;
        private CharacterAnimationComponent3D _animation;
        private CharacterMovementComponent3D _movement;
        private Node3D _visual;
        private readonly Node3D[] _bursts = new Node3D[PoolSize];
        private readonly CpuParticles3D[] _cores = new CpuParticles3D[PoolSize];
        private readonly CpuParticles3D[] _sparks = new CpuParticles3D[PoolSize];
        private readonly double[] _expires = new double[PoolSize];
        private AbilityRuntime _runtime;
        private WeaponAttackTrail _trail;
        private double[] _frameTimes;
        private double[] _keyTimes;
        private double _nextSample;
        private int _poolIndex;

        public override void OnInit()
        {
            _equipment = Owner.GetComponent<WeaponEquipmentComponent3D>();
            _abilities = Owner.GetComponent<AbilitySystemComponent3D>();
            _animation = Owner.GetComponent<CharacterAnimationComponent3D>();
            _movement = Owner.GetComponent<CharacterMovementComponent3D>();
            if (_equipment == null || _abilities == null || _animation?.Sprite == null || EffectScene == null) return;
            _visual = new Node3D { Name = "WeaponAttackEffect", Visible = false };
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
            }
            _abilities.AbilityActivated += OnAttackStarted;
            _abilities.AbilityCancelled += OnAttackEnded;
            _abilities.AbilityCompleted += OnAttackEnded;
            _equipment.WeaponChanged += OnWeaponChanged;
        }

        private void OnAttackStarted(AbilityRuntime runtime)
        {
            WeaponAttackTrail trail = _equipment.CurrentWeapon?.AnimationProfile?.AttackTrails
                .FirstOrDefault(candidate => candidate?.IsValid == true && candidate.AbilityId == runtime.AbilityId);
            if (trail == null) return;
            Stop();
            SpriteFrames frames = _animation.Sprite.SpriteFrames;
            if (!frames.HasAnimation(trail.Animation)) return;
            int count = frames.GetFrameCount(trail.Animation);
            if (trail.Frames[0] < 0 || trail.Frames[^1] >= count) return;
            _frameTimes = new double[count + 1];
            for (int i = 0; i < count; i++)
                _frameTimes[i + 1] = _frameTimes[i] + frames.GetFrameDuration(trail.Animation, i) / frames.GetAnimationSpeed(trail.Animation);
            _keyTimes = trail.Frames.Select(FrameTime).ToArray();
            for (int i = 1; i < _keyTimes.Length; i++) if (_keyTimes[i] <= _keyTimes[i - 1]) return;
            _trail = trail;
            _runtime = runtime;
            AttackFacing = _movement?.Facing ?? 1;
            EmittedBursts = 0;
            _nextSample = _keyTimes[0];
            _visual.Visible = true;
        }

        public override void OnUpdate(double delta)
        {
            if (_runtime == null) return;
            if (!_runtime.IsRunning) { Stop(); return; }
            for (int i = 0; i < PoolSize; i++)
            {
                _expires[i] -= delta;
                if (_expires[i] <= 0) _bursts[i].Visible = false;
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

        private void EmitBlade(double time, AnimatedSprite3D sprite)
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
            _expires[_poolIndex] = TailLifetime;
            _poolIndex = (_poolIndex + 1) % PoolSize;
            EmittedBursts++;
        }

        private void OnAttackEnded(AbilityRuntime runtime) { if (ReferenceEquals(runtime, _runtime)) Stop(); }
        private void OnWeaponChanged(WeaponDefinition weapon) => Stop();

        private void Stop()
        {
            _runtime = null;
            _trail = null;
            if (GodotObject.IsInstanceValid(_visual)) _visual.Visible = false;
            for (int i = 0; i < PoolSize; i++)
            {
                if (!GodotObject.IsInstanceValid(_bursts[i])) continue;
                _bursts[i].Visible = false;
                _cores[i].Emitting = _sparks[i].Emitting = false;
                _expires[i] = 0;
            }
        }

        public override void OnDestroy()
        {
            Stop();
            if (_abilities != null)
            {
                _abilities.AbilityActivated -= OnAttackStarted;
                _abilities.AbilityCancelled -= OnAttackEnded;
                _abilities.AbilityCompleted -= OnAttackEnded;
            }
            if (_equipment != null) _equipment.WeaponChanged -= OnWeaponChanged;
            if (GodotObject.IsInstanceValid(_visual))
            {
                _visual.GetParent()?.RemoveChild(_visual);
                _visual.QueueFree();
            }
            _visual = null;
            _equipment = null;
            _abilities = null;
            _animation = null;
            _movement = null;
            _frameTimes = _keyTimes = null;
        }
    }
}
