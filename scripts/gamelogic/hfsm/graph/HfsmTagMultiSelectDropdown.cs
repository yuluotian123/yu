using System;
using System.Collections.Generic;
using Godot;

namespace GameLogic
{
    internal sealed partial class HfsmTagMultiSelectDropdown : VBoxContainer
    {
        private readonly HfsmTagRegistry _registry;
        private readonly Action<string> _onTagsChanged;
        private readonly HashSet<string> _selectedTags;
        private Button _button;
        private PopupPanel _popup;
        private VBoxContainer _popupContent;

        public HfsmTagMultiSelectDropdown(
            HfsmTagRegistry registry,
            string tags,
            Action<string> onTagsChanged)
        {
            _registry = registry;
            _onTagsChanged = onTagsChanged;
            _selectedTags = new HashSet<string>(HfsmTagUtility.ParseTags(tags), StringComparer.OrdinalIgnoreCase);

            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            BuildButton();
            ApplySelection();
        }

        private void BuildButton()
        {
            _button = new Button
            {
                Text = "Tags",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Disabled = _registry == null,
                TooltipText = _registry == null
                    ? $"Global tag registry not found: {HfsmTagRegistry.DefaultResourcePath}"
                    : "Select state tags"
            };
            _button.Pressed += ShowPopup;
            AddChild(_button);
        }

        private void ShowPopup()
        {
            if (_registry == null)
                return;

            EnsurePopup();
            RefreshPopupContent();

            Vector2 screenPosition = _button.GetScreenPosition();
            int width = Mathf.Max(280, (int)_button.Size.X);
            _popup.Position = new Vector2I((int)screenPosition.X, (int)(screenPosition.Y + _button.Size.Y));
            _popup.Size = new Vector2I(width, 320);
            _popup.Popup();
        }

        private void EnsurePopup()
        {
            if (_popup != null && GodotObject.IsInstanceValid(_popup))
                return;

            _popup = new PopupPanel
            {
                Transient = true,
                Exclusive = false
            };
            AddChild(_popup);

            var root = new VBoxContainer();
            root.AddThemeConstantOverride("separation", 6);
            _popup.AddChild(root);

            var scroll = new ScrollContainer
            {
                CustomMinimumSize = new Vector2(280f, 260f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            root.AddChild(scroll);

            _popupContent = new VBoxContainer();
            _popupContent.AddThemeConstantOverride("separation", 4);
            scroll.AddChild(_popupContent);

            var closeButton = new Button
            {
                Text = "Close",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            closeButton.Pressed += () => _popup.Hide();
            root.AddChild(closeButton);
        }

        private void RefreshPopupContent()
        {
            ClearContainer(_popupContent);

            foreach (string layer in _registry.GetLayerNames())
                AddLayerSection(layer);

            List<HfsmTagDefinition> plainTags = _registry.GetPlainTags();
            if (plainTags.Count > 0)
                AddTagSection("Flags", plainTags, false);

            List<string> unknownTags = GetUnknownSelectedTags();
            if (unknownTags.Count > 0)
                AddUnknownSection(unknownTags);
        }

        private void AddLayerSection(string layer)
        {
            List<HfsmTagDefinition> tags = _registry.GetLayerTags(layer);
            if (tags.Count == 0)
                return;

            AddSectionLabel(layer);
            foreach (HfsmTagDefinition tag in tags)
                AddTagCheckBox(tag, true);
        }

        private void AddTagSection(string title, List<HfsmTagDefinition> tags, bool isLayerTag)
        {
            AddSectionLabel(title);
            foreach (HfsmTagDefinition tag in tags)
                AddTagCheckBox(tag, isLayerTag);
        }

        private void AddUnknownSection(List<string> tags)
        {
            AddSectionLabel("Unregistered");
            foreach (string tag in tags)
            {
                var check = new CheckBox
                {
                    Text = tag,
                    ButtonPressed = true
                };
                check.Toggled += value =>
                {
                    if (!value)
                    {
                        _selectedTags.Remove(tag);
                        ApplySelection();
                        RefreshPopupContent();
                    }
                };
                _popupContent.AddChild(check);
            }
        }

        private void AddSectionLabel(string text)
        {
            var label = new Label { Text = text };
            label.AddThemeColorOverride("font_color", new Color(0.72f, 0.72f, 0.72f));
            _popupContent.AddChild(label);
        }

        private void AddTagCheckBox(HfsmTagDefinition tag, bool isLayerTag)
        {
            var check = new CheckBox
            {
                Text = tag.DisplayText,
                ButtonPressed = _selectedTags.Contains(tag.Key),
                TooltipText = tag.Description ?? string.Empty
            };

            check.Toggled += value =>
            {
                if (value)
                {
                    if (isLayerTag)
                    {
                        foreach (HfsmTagDefinition layerTag in _registry.GetLayerTags(tag.Layer))
                            _selectedTags.Remove(layerTag.Key);
                    }

                    _selectedTags.Add(tag.Key);
                }
                else
                {
                    _selectedTags.Remove(tag.Key);
                }

                ApplySelection();
                RefreshPopupContent();
            };

            _popupContent.AddChild(check);
        }

        private void ApplySelection()
        {
            List<string> normalized = _registry != null
                ? _registry.NormalizeTagList(_selectedTags)
                : HfsmTagUtility.DistinctTags(_selectedTags);

            string tags = HfsmTagUtility.FormatTags(normalized);
            _onTagsChanged?.Invoke(tags);
            UpdateButton(tags);
        }

        private void UpdateButton(string tags)
        {
            int count = HfsmTagUtility.ParseTags(tags).Count;
            _button.Text = count == 0 ? "Tags" : $"Tags ({count})";
            _button.TooltipText = string.IsNullOrWhiteSpace(tags) ? "Select state tags" : tags;
        }

        private List<string> GetUnknownSelectedTags()
        {
            var unknownTags = new List<string>();
            foreach (string tag in HfsmTagUtility.DistinctTags(_selectedTags))
            {
                if (_registry.FindTag(tag) == null)
                    unknownTags.Add(tag);
            }

            return unknownTags;
        }

        private static void ClearContainer(Container container)
        {
            while (container.GetChildCount() > 0)
            {
                Node child = container.GetChild(0);
                container.RemoveChild(child);
                child.QueueFree();
            }
        }
    }
}
