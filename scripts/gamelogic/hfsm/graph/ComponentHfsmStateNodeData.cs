using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace GameLogic
{
    public class ComponentHfsmStateNodeData : HfsmStateNodeData
    {
        public string ComponentTypeName { get; set; } = string.Empty;
        public bool WarnWhenMissing { get; set; } = true;

        public override Color GetNodeColor() => new(0.2f, 0.68f, 0.86f);

        public override string GetDisplayName()
        {
            string stateName = string.IsNullOrWhiteSpace(StateName) ? "Component State" : StateName;
            return string.IsNullOrWhiteSpace(ComponentTypeName)
                ? stateName
                : $"{stateName} [{ComponentTypeName}]";
        }

        public override void OnEnter(HfsmRuntime runtime)
        {
            base.OnEnter(runtime);
            ResolveHandler(runtime, true)?.OnHfsmStateEnter(runtime, this);
        }

        public override void OnUpdate(HfsmRuntime runtime, double delta)
        {
            ResolveHandler(runtime, false)?.OnHfsmStateUpdate(runtime, this, delta);
        }

        public override void OnExit(HfsmRuntime runtime)
        {
            ResolveHandler(runtime, true)?.OnHfsmStateExit(runtime, this);
            base.OnExit(runtime);
        }

        protected override void AddExtraFields(VBoxContainer root)
        {
            root.AddChild(new HSeparator());

            root.AddChild(new Label { Text = "Component Handler" });

            var typeEdit = new LineEdit
            {
                PlaceholderText = "Component type",
                Text = ComponentTypeName
            };
            typeEdit.TextChanged += value => ComponentTypeName = value.Trim();
            root.AddChild(typeEdit);

            List<string> handlerTypes = GetHandlerComponentTypeNames();
            if (handlerTypes.Count > 0)
            {
                var typePicker = new OptionButton();
                typePicker.AddItem("Select handler");

                for (int i = 0; i < handlerTypes.Count; i++)
                {
                    typePicker.AddItem(handlerTypes[i]);
                    if (string.Equals(handlerTypes[i], ComponentTypeName, StringComparison.Ordinal))
                        typePicker.Select(i + 1);
                }

                typePicker.ItemSelected += index =>
                {
                    int handlerIndex = (int)index - 1;
                    if (handlerIndex < 0 || handlerIndex >= handlerTypes.Count)
                        return;

                    ComponentTypeName = handlerTypes[handlerIndex];
                    typeEdit.Text = ComponentTypeName;
                };
                root.AddChild(typePicker);
            }

            var warnCheck = new CheckBox
            {
                Text = "Warn if missing",
                ButtonPressed = WarnWhenMissing
            };
            warnCheck.Toggled += value => WarnWhenMissing = value;
            root.AddChild(warnCheck);
        }

        private IHfsmStateHandler ResolveHandler(HfsmRuntime runtime, bool shouldWarn)
        {
            if (runtime == null)
                return null;

            if (string.IsNullOrWhiteSpace(ComponentTypeName))
            {
                Warn(shouldWarn, "Component type is empty.");
                return null;
            }

            Component2D component = runtime.GetComponent(ComponentTypeName);
            if (component == null)
            {
                Warn(shouldWarn, $"Missing component: {ComponentTypeName}.");
                return null;
            }

            if (component is IHfsmStateHandler handler)
                return handler;

            Warn(shouldWarn, $"{ComponentTypeName} does not implement IHfsmStateHandler.");
            return null;
        }

        private void Warn(bool shouldWarn, string message)
        {
            if (shouldWarn && WarnWhenMissing)
                GD.PushWarning($"[ComponentHfsmStateNodeData:{StateName}] {message}");
        }

        private static List<string> GetHandlerComponentTypeNames()
        {
            var result = new List<string>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(type => type != null).ToArray();
                }

                foreach (Type type in types)
                {
                    if (type == null ||
                        type.IsAbstract ||
                        !typeof(Component2D).IsAssignableFrom(type) ||
                        !typeof(IHfsmStateHandler).IsAssignableFrom(type))
                    {
                        continue;
                    }

                    result.Add(type.Name);
                }
            }

            return result
                .Distinct(StringComparer.Ordinal)
                .OrderBy(typeName => typeName, StringComparer.Ordinal)
                .ToList();
        }
    }
}
