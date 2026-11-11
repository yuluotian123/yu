using System.Collections.Generic;
using System.Linq;
using Godot;

namespace GameLogic
{
    public class CharacterRegisterAbilityNodeData : GraphNodeData
    {
        public string SourcePath { get; set; } = string.Empty;
        public override List<string> GetGraphTypes() => new() { CharacterGraphAsset.CharacterGraphTypeName };
        public override string GetMenuName() => "注册技能（技能 / 武器）";
        public override string GetCategory() => "角色 / 技能";
        public override string GetDisplayName() => "Register: " + SourcePath.GetFile().GetBaseName();
        public override int GetInputCount() => 0;
        public override int GetOutputCount() => 0;
        public override bool CanBePrime() => false;
        public IEnumerable<AbilityResource> GetAbilities()
        {
            if (string.IsNullOrWhiteSpace(SourcePath) || !ResourceLoader.Exists(SourcePath))
                return System.Array.Empty<AbilityResource>();
            return ResourceLoader.Load(SourcePath) switch
            {
                AbilityResource ability => new[] { ability },
                WeaponDefinition weapon => weapon.GetPrimaryAttacks().Where(ability => ability != null).Distinct(),
                _ => System.Array.Empty<AbilityResource>()
            };
        }
        public override void Validate(GraphAsset graph, GraphValidationResult result)
        {
            var abilities = GetAbilities().ToArray();
            if (abilities.Length == 0 || abilities.Any(ability => ability.Graph == null))
                result.AddError("Register Ability requires an AbilityResource or WeaponDefinition with skills.", Id);
        }
        public override void CreateNodeUI(GraphEditorContext context) =>
            context.GraphNode.AddChild(new Label { Text = GetDisplayName() });
#if TOOLS
        public override Control CreateInspectorUI(GraphEditorContext context)
        {
            var root = new VBoxContainer();
            var skills = new VBoxContainer();
            root.AddChild(new Label { Text = "AbilityResource 或 WeaponDefinition；武器自动展开全部架势技能。" });
            root.AddChild(new GraphResourcePathField(typeof(Resource), SourcePath, value =>
            {
                SourcePath = value;
                context?.CurrentGraph?.MarkDirty();
                RefreshSkills();
                Callable.From(() =>
                {
                    if (GodotObject.IsInstanceValid(context?.GraphNode) && !context.GraphNode.IsQueuedForDeletion())
                        GraphNodeViewBuilder.RefreshNodeUI(this, context.GraphNode, context);
                }).CallDeferred();
            },
                resource => resource switch { AbilityResource ability => ability.DisplayName, WeaponDefinition weapon => weapon.DisplayName, _ => resource.GetType().Name }));
            root.AddChild(skills);
            RefreshSkills();
            return root;

            void RefreshSkills()
            {
                foreach (Node child in skills.GetChildren())
                {
                    skills.RemoveChild(child);
                    child.QueueFree();
                }
                foreach (var ability in GetAbilities())
                {
                    var open = new Button { Text = "Open: " + ability.DisplayName };
                    open.Pressed += () =>
                    {
                        if (ability.Graph != null) GraphPlugin.Instance?.OpenGraphEditor(ability.Graph, ability, ability.Graph.PrimeNode?.Id);
                    };
                    skills.AddChild(open);
                }
            }
        }
#endif
    }
}
