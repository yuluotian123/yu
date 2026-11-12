using System;
using System.Collections.Generic;
using Godot;

namespace GameLogic
{
    public sealed class AbilityEffectPlayer3D : IDisposable
    {
        private readonly GameObject3D _owner;
        private readonly Dictionary<string, ParticleScene> _scenes = new(StringComparer.Ordinal);

        public AbilityEffectPlayer3D(GameObject3D owner) => _owner = owner;

        public bool IsScenePlaying(string key) => _scenes.TryGetValue(key, out var scene) && scene.Root.Visible;

        public void PlayParticles(string key, PackedScene packedScene, object ownerKey, double duration)
        {
            if (packedScene == null || string.IsNullOrWhiteSpace(key)) return;
            if (!_scenes.TryGetValue(key, out ParticleScene scene) || scene.Path != packedScene.ResourcePath)
            {
                scene?.Dispose();
                scene = new ParticleScene(packedScene.Instantiate<Node3D>(), packedScene.ResourcePath);
                scene.Root.Name = "AbilityEffect_" + key;
                _owner.AddChild(scene.Root);
                _scenes[key] = scene;
            }
            scene.OwnerKey = ownerKey;
            scene.Root.Visible = true;
            foreach (CpuParticles3D particles in scene.Particles)
            {
                particles.Lifetime = Math.Max(.01, duration);
                particles.Restart();
                particles.Emitting = true;
            }
        }

        public void StopParticles(string key, object ownerKey)
        {
            if (_scenes.TryGetValue(key, out ParticleScene scene) && scene.OwnerKey == ownerKey) scene.Stop();
        }

        public void Clear()
        {
            foreach (ParticleScene scene in _scenes.Values) scene.Stop();
        }

        public void Dispose()
        {
            foreach (ParticleScene scene in _scenes.Values) scene.Dispose();
            _scenes.Clear();
        }

        private sealed class ParticleScene : IDisposable
        {
            public Node3D Root { get; }
            public string Path { get; }
            public object OwnerKey { get; set; }
            public List<CpuParticles3D> Particles { get; } = new();
            public ParticleScene(Node3D root, string path)
            {
                Root = root; Path = path;
                Collect(root);
            }
            private void Collect(Node root)
            {
                if (root is CpuParticles3D particles) Particles.Add(particles);
                foreach (Node child in root.GetChildren()) Collect(child);
            }
            public void Stop()
            {
                if (!GodotObject.IsInstanceValid(Root)) return;
                Root.Visible = false;
                foreach (CpuParticles3D particles in Particles) particles.Emitting = false;
            }
            public void Dispose()
            {
                if (!GodotObject.IsInstanceValid(Root)) return;
                Stop();
                Root.GetParent()?.RemoveChild(Root);
                Root.QueueFree();
            }
        }
    }
}
