using Godot;

namespace GameLogic
{
    [Tool, GlobalClass]
    public partial class StanceSwitchEffectComponent3D : Component3D
    {
        [Export] public PackedScene EffectScene { get; set; }
        [Export(PropertyHint.Range, "0.05,1,0.01")] public float Duration { get; set; } = 0.32f;
        public override int Priority => ComponentPriority.VFX - 1;
        public bool IsPlaying => _remaining > 0;

        private WeaponEquipmentComponent3D _equipment;
        private Node3D _visual;
        private CpuParticles3D _particles;
        private double _remaining;

        public override void OnInit()
        {
            _equipment = Owner.GetComponent<WeaponEquipmentComponent3D>();
            if (_equipment == null || EffectScene == null) return;
            _visual = EffectScene.Instantiate<Node3D>();
            _visual.Name = "StanceSwitchEffect";
            _visual.Visible = false;
            Owner.AddChild(_visual);
            _particles = _visual.GetNode<CpuParticles3D>("Particles");
            _particles.Emitting = false;
            _particles.Lifetime = Mathf.Max(0.01f, Duration);
            _equipment.StanceSwitchStarted += Play;
            _equipment.WeaponChanged += OnWeaponChanged;
        }

        public override void OnUpdate(double delta)
        {
            if (!IsPlaying) return;
            _remaining = System.Math.Max(0, _remaining - delta);
            if (_remaining <= 0) { Stop(); return; }
        }

        public override void OnDestroy()
        {
            Stop();
            if (_equipment != null)
            {
                _equipment.StanceSwitchStarted -= Play;
                _equipment.WeaponChanged -= OnWeaponChanged;
            }
            if (GodotObject.IsInstanceValid(_visual))
            {
                _visual.GetParent()?.RemoveChild(_visual);
                _visual.QueueFree();
            }
            _particles = null;
            _visual = null;
            _equipment = null;
        }

        private void Play(string stance)
        {
            if (!GodotObject.IsInstanceValid(_particles)) return;
            _remaining = Mathf.Max(0.01f, Duration);
            _visual.Visible = true;
            _particles.Restart();
            _particles.Emitting = true;
        }

        private void OnWeaponChanged(WeaponDefinition weapon) => Stop();

        private void Stop()
        {
            _remaining = 0;
            if (GodotObject.IsInstanceValid(_particles)) _particles.Emitting = false;
            if (GodotObject.IsInstanceValid(_visual)) _visual.Visible = false;
        }
    }
}
