using System;
using System.Collections.Generic;
using System.Linq;
using Framework.UI;
using Godot;

namespace GameLogic;

[Window(UILayer.Modal, "res://assets/scenes/ui/inventory_window.tscn", fullScreen: false)]
public class InventoryWindow : UIWindow
{
    private Control _canvas;
    private Panel _panel;
    private InventoryComponent3D _inventory;
    private WeaponEquipmentComponent3D _equipment;
    private TextureRect _portrait, _detailIcon;
    private Label _current, _count, _name, _type, _description, _timing, _state, _message;
    private Button _equip, _unequip, _currentWeaponSlot;
    private Panel _dropHighlight;
    private bool _dragActive;
    private readonly List<(Control Control, InventoryDragArea Area, int Index)> _dropTargets = new();
    private readonly List<Button> _cells = new();
    private readonly List<Label> _badges = new();
    private readonly Button[] _slotButtons = new Button[2], _removeButtons = new Button[2];
    private readonly TextureRect[] _slotIcons = new TextureRect[2];
    private readonly Label[] _slotNames = new Label[2], _slotStates = new Label[2];
    private string _selectedId;
    private double _refreshTime;
    public InventoryComponent3D Inventory => _inventory;

    protected override void OnCreate()
    {
        if (Owner == null) return;
        Owner.Theme = InventoryUiSkin.Theme();
        Owner.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        _inventory = UserDatas?.FirstOrDefault() as InventoryComponent3D;
        _equipment = _inventory?.Equipment;
        if (_inventory == null || _equipment == null) return;
        Build();
        _inventory.ContentsChanged += Refresh;
        _equipment.LoadoutChanged += Refresh;
        Owner.Resized += Resize;
        Resize();
        Callable.From(Resize).CallDeferred();
    }

    protected override void OnRefresh() => Refresh();

    protected override void OnUpdate(double delta)
    {
        UpdateDragFeedback();
        _refreshTime += delta;
        if (_refreshTime < 0.12) return;
        _refreshTime = 0;
        Refresh();
    }

    private void Build()
    {
        var dim = new ColorRect { Color = new Color(0.10f, 0.07f, 0.05f, 0.58f), MouseFilter = Control.MouseFilterEnum.Stop };
        Owner.AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _canvas = new Control { Name = "BagCanvas", Size = new Vector2(1040, 650), MouseFilter = Control.MouseFilterEnum.Ignore };
        Owner.AddChild(_canvas);
        var shadow = Add(new Panel(), _canvas, 8, 10, 1040, 650);
        shadow.AddThemeStyleboxOverride("panel", InventoryUiSkin.Box("251b1e", "251b1e", 0));
        _panel = Add(new Panel { Name = "BagPanel" }, _canvas, 0, 0, 1040, 650);
        _panel.AddThemeStyleboxOverride("panel", InventoryUiSkin.Box("805638", "30262a", 4));
        Panel(6, 6, 1028, 638, "e6bb73", "bd8950");
        Panel(12, 12, 1016, 626, "f1dfbc", "9f754a");
        Panel(12, 12, 1016, 78, "4b362e", "805638");
        Picture(_panel, GD.Load<Texture2D>("res://assets/textures/ui/inventory/fox.svg"), 32, 25, 48, 48);
        Text("旅 人 行 囊", 96, 22, 360, 36, 29, new Color("ffe3a0"));
        Text("TRAVELER'S SATCHEL", 98, 60, 340, 18, 11, new Color("c5a178"));
        Text("整理装备，继续下一段旅途。", 570, 31, 350, 32, 16, new Color("e7c99d"));
        var close = Add(InventoryUiSkin.Button("×"), _panel, 962, 30, 42, 38);
        close.Name = "CloseButton";
        close.Pressed += () => _inventory.Close();
        Text("旅人装备", 32, 110, 220, 30, 21);
        Text("黄棕色的冒险者", 32, 141, 220, 22, 13, InventoryUiSkin.Muted);
        _currentWeaponSlot = Add(InventoryUiSkin.Button(""), _panel, 32, 177, 220, 238);
        _currentWeaponSlot.Name = "CurrentWeaponSlot";
        _currentWeaponSlot.AddThemeStyleboxOverride("normal", InventoryUiSkin.Box("d7d3b2", "b29b75"));
        _currentWeaponSlot.AddThemeStyleboxOverride("hover", InventoryUiSkin.Box("dce0ba", "80916e"));
        _currentWeaponSlot.TooltipText = "拖入武器，设为当前使用；也可将当前武器拖回行囊卸下。";
        _currentWeaponSlot.Pressed += () => Select(_equipment.CurrentWeaponId);
        BindDrag(_currentWeaponSlot, InventoryDragArea.CurrentWeapon, -1);
        // Small square accents echo the low-resolution character rather than glossy UI chrome.
        Square(48, 194, 6, "a4b497"); Square(230, 194, 6, "a4b497");
        Square(48, 390, 6, "a4b497"); Square(230, 390, 6, "a4b497");
        _portrait = Picture(_panel, null, 61, 188, 162, 204);
        _current = Text("", 38, 390, 208, 24, 14);
        _current.HorizontalAlignment = HorizontalAlignment.Center;
        for (int slot = 0; slot < 2; slot++)
        {
            int index = slot;
            int y = 433 + 76 * slot;
            var card = Add(InventoryUiSkin.Button(""), _panel, 32, y, 220, 64);
            card.Name = "EquipmentSlot" + slot;
            card.Pressed += () => Select(_equipment.Slots[index]?.WeaponId);
            _slotButtons[slot] = card;
            BindDrag(card, InventoryDragArea.Equipment, slot);
            _slotIcons[slot] = Picture(card, null, 8, 9, 42, 46);
            _slotNames[slot] = ChildText(card, "", 58, 7, 112, 26, 17);
            _slotStates[slot] = ChildText(card, "", 58, 32, 120, 23, 12, InventoryUiSkin.Muted);
            var remove = Add(InventoryUiSkin.Button("－"), card, 178, 17, 30, 30);
            remove.Name = "UnequipSlot" + slot;
            remove.Pressed += () => Unequip(index);
            _removeButtons[slot] = remove;
            BindDrag(remove, InventoryDragArea.Equipment, slot, highlightTarget: false);
        }
        Square(270, 112, 1, "bfa27a", 464);
        Text("行囊", 292, 110, 170, 30, 21);
        _count = Text("", 528, 113, 160, 26, 15, InventoryUiSkin.Muted);
        _count.HorizontalAlignment = HorizontalAlignment.Right;
        Panel(292, 158, 78, 31, "a77943", "926333");
        Text("武器", 306, 160, 52, 26, 15, new Color("fff1d0"));
        Text("点击查看 · 拖拽整理", 488, 160, 200, 26, 13, InventoryUiSkin.Muted);
        var scroll = Add(new ScrollContainer { Name = "ItemScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }, _panel, 292, 203, 404, 324);
        var grid = new GridContainer { Name = "ItemGrid", Columns = 4, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 10);
        scroll.AddChild(grid);
        for (int i = 0; i < Math.Max(12, _inventory.Capacity); i++)
        {
            int index = i;
            var cell = InventoryUiSkin.Button("");
            cell.Name = "ItemCell" + i;
            cell.CustomMinimumSize = new Vector2(88, 92);
            grid.AddChild(cell);
            Picture(cell, null, 18, 12, 52, 52).Name = "ItemIcon";
            var badge = ChildText(cell, "", 5, 67, 78, 20, 12);
            badge.HorizontalAlignment = HorizontalAlignment.Center;
            _badges.Add(badge);
            cell.Pressed += () => Select(_inventory.GetWeaponAtCell(index)?.WeaponId);
            BindDrag(cell, InventoryDragArea.Bag, index);
            _cells.Add(cell);
        }
        Text("拖至装备槽装备 · 拖回行囊卸下", 292, 538, 400, 23, 13, InventoryUiSkin.Muted);
        Text("拖至角色预览区，切换当前武器。", 292, 560, 400, 22, 13, InventoryUiSkin.Muted);
        Square(714, 112, 1, "bfa27a", 464);
        Text("物品详情", 738, 110, 270, 30, 21);
        Panel(738, 158, 268, 116, "e7cfa5", "c2a070");
        _detailIcon = Picture(_panel, null, 820, 166, 96, 96);
        _name = Text("", 738, 288, 268, 36, 25);
        _type = Text("", 738, 329, 268, 24, 14, InventoryUiSkin.Muted);
        _description = Text("", 738, 368, 268, 76, 16);
        _description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _description.VerticalAlignment = VerticalAlignment.Top;
        _timing = Text("", 738, 445, 268, 25, 14, InventoryUiSkin.Muted);
        _state = Text("", 738, 480, 268, 26, 14, InventoryUiSkin.Teal);
        _equip = Add(InventoryUiSkin.Button("装备武器"), _panel, 738, 520, 268, 40);
        _equip.Name = "EquipButton";
        _equip.Pressed += Equip;
        _unequip = Add(InventoryUiSkin.Button("卸下武器"), _panel, 738, 570, 268, 40);
        _unequip.Name = "UnequipButton";
        _unequip.Pressed += () => Unequip(_equipment.FindEquippedSlot(_selectedId));
        _message = Text("", 32, 579, 665, 29, 14, InventoryUiSkin.Teal);
        Square(26, 614, 988, "bca076", 1);
        Text("[ B / Esc ]  关闭背包", 32, 615, 300, 18, 11, InventoryUiSkin.Muted);
        Text("[ Q ]  切换已装备武器     ·     至少保留一把武器", 392, 615, 614, 18, 11, InventoryUiSkin.Muted);
        _dropHighlight = Add(new Panel { Name = "DropHighlight", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false, ZIndex = 20 }, _panel, 0, 0, 0, 0);
    }

    private void Resize()
    {
        if (_canvas == null || Owner == null) return;
        Vector2 viewport = Owner.Size;
        float scale = Math.Min(1f, Math.Min((viewport.X - 40) / 1050f, (viewport.Y - 40) / 660f));
        scale = Math.Max(0.2f, scale);
        _canvas.Scale = Vector2.One * scale;
        _canvas.Position = ((viewport - new Vector2(1040, 650) * scale) * 0.5f).Round();
    }

    private void Select(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        _selectedId = id;
        _message.Text = "";
        Refresh();
    }

    public void Refresh()
    {
        if (_inventory == null || _panel == null) return;
        if (_inventory.Find(_selectedId) == null)
            _selectedId = _equipment.CurrentWeaponId;
        WeaponDefinition selected = _inventory.Find(_selectedId) ?? _inventory.Items.FirstOrDefault();
        _selectedId = selected?.WeaponId;
        _count.Text = $"{_inventory.Items.Count:00} / {_inventory.Capacity:00}";
        _portrait.Texture = _equipment.CurrentWeapon?.Portrait;
        _current.Text = "当前持有 · " + _equipment.CurrentWeapon?.DisplayName;
        int equippedCount = _equipment.Slots.Count(item => item != null);
        for (int i = 0; i < _cells.Count; i++)
        {
            WeaponDefinition item = _inventory.GetWeaponAtCell(i);
            _cells[i].Disabled = i >= _inventory.Capacity;
            _cells[i].TooltipText = item == null ? "拖入武器" : item.DisplayName + " · 拖动可整理或装备";
            _cells[i].GetNode<TextureRect>("ItemIcon").Texture = item?.Icon;
            int slot = item == null ? -1 : _equipment.FindEquippedSlot(item.WeaponId);
            _badges[i].Text = item == null ? "·" : slot < 0 ? "未装备" : slot == _equipment.CurrentSlot ? "使用中" : "已装备";
            _cells[i].AddThemeStyleboxOverride("normal", item?.WeaponId == _selectedId
                ? InventoryUiSkin.Box("ffe5a8", "b17832", 3) : InventoryUiSkin.Box(item == null ? "dfcdaa" : "e6cfa5", "b89a6e"));
        }
        for (int i = 0; i < 2; i++)
        {
            WeaponDefinition weapon = _equipment.Slots[i];
            _slotIcons[i].Texture = weapon?.Icon;
            _slotNames[i].Text = weapon?.DisplayName ?? "空武器槽";
            _slotStates[i].Text = weapon == null ? "从行囊装备武器" : i == _equipment.CurrentSlot ? "正在使用" : "Q 可切换";
            _slotButtons[i].AddThemeStyleboxOverride("normal", i == _equipment.CurrentSlot
                ? InventoryUiSkin.Box("dfd6b2", "688477", 3) : InventoryUiSkin.Box("e6cfa5", "b89a6e"));
            _removeButtons[i].Disabled = weapon == null || equippedCount <= 1 || _equipment.IsBusy;
            _removeButtons[i].TooltipText = equippedCount <= 1 ? "至少保留一把武器" : "卸下到行囊";
        }
        if (selected == null) return;
        int equippedSlot = _equipment.FindEquippedSlot(selected.WeaponId);
        bool current = selected.WeaponId == _equipment.CurrentWeaponId;
        _detailIcon.Texture = selected.Icon;
        _name.Text = selected.DisplayName;
        _type.Text = selected.Category == WeaponCategory.Greatsword ? "双手武器 / 大剑" : "双手武器 / 太刀";
        _description.Text = selected.Description;
        _timing.Text = $"攻击间隔  {selected.PrimaryAttack.Cooldown:0.##} 秒";
        _state.Text = _equipment.IsBusy ? "动作进行中 · 收招后可整理" : current ? "已装备 · 正在使用" : equippedSlot >= 0 ? "已装备 · 可切换使用" : "保存在行囊中";
        _equip.Text = current ? "当前使用中" : equippedSlot >= 0 ? "切换为当前武器" : "装备武器";
        _equip.Disabled = current || _equipment.IsBusy;
        _unequip.Disabled = equippedSlot < 0 || equippedCount <= 1 || _equipment.IsBusy;
        _unequip.Text = equippedSlot >= 0 && equippedCount <= 1 ? "至少保留一把武器" : "卸下武器";
    }

    private void Equip()
    {
        if (_selectedId == null) return;
        bool result = _equipment.EquipWeapon(_selectedId);
        _message.Text = result ? $"已装备 {_inventory.Find(_selectedId)?.DisplayName}。" : "收招后再调整装备。";
        Refresh();
    }

    private void Unequip(int slot)
    {
        if (slot < 0 || slot >= _equipment.Slots.Count) return;
        string name = _equipment.Slots[slot]?.DisplayName;
        bool result = _equipment.UnequipSlot(slot);
        _message.Text = result ? $"{name}已收回行囊。" : _equipment.IsBusy ? "收招后再调整装备。" : "至少保留一把武器。";
        Refresh();
    }

    protected override void OnDestroy()
    {
        if (GodotObject.IsInstanceValid(Owner) && Owner.GetViewport().GuiIsDragging()) Owner.GetViewport().GuiCancelDrag();
        if (_inventory != null) _inventory.ContentsChanged -= Refresh;
        if (_equipment != null) _equipment.LoadoutChanged -= Refresh;
        if (GodotObject.IsInstanceValid(Owner)) Owner.Resized -= Resize;
        _inventory?.WindowDestroyed();
        _inventory = null;
        _equipment = null;
    }

    private void BindDrag(Control control, InventoryDragArea area, int index, bool highlightTarget = true)
    {
        control.SetDragForwarding(
            Callable.From<Vector2, Variant>(_ => BeginDrag(control, area, index)),
            Callable.From<Vector2, Variant, bool>((_, data) => CanDrop(data, area, index)),
            Callable.From<Vector2, Variant>((_, data) => Drop(data, area, index)));
        if (highlightTarget) _dropTargets.Add((control, area, index));
    }

    private Variant BeginDrag(Control source, InventoryDragArea area, int index)
    {
        if (_equipment.IsBusy) { _message.Text = "收招后再调整装备。"; return default; }
        if (area == InventoryDragArea.CurrentWeapon) index = _equipment.CurrentSlot;
        WeaponDefinition weapon = area == InventoryDragArea.Bag ? _inventory.GetWeaponAtCell(index)
            : index >= 0 && index < _equipment.Slots.Count ? _equipment.Slots[index] : null;
        if (weapon == null) return default;
        Select(weapon.WeaponId);
        var preview = new Panel { Size = new Vector2(116, 104), MouseFilter = Control.MouseFilterEnum.Ignore, Theme = Owner.Theme,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest };
        preview.AddThemeStyleboxOverride("panel", InventoryUiSkin.Box("f5dfb7", "875d33", 3));
        Picture(preview, weapon.Icon, 28, 5, 60, 64);
        var name = ChildText(preview, weapon.DisplayName, 4, 72, 108, 26, 16);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        source.SetDragPreview(preview);
        return new Godot.Collections.Dictionary
        {
            ["type"] = "inventory_weapon", ["inventory"] = _inventory, ["weapon_id"] = weapon.WeaponId,
            ["source_area"] = (int)area, ["source_index"] = index
        };
    }

    private bool ReadDrag(Variant data, out string weaponId, out InventoryDragArea source, out int index)
    {
        weaponId = null; source = InventoryDragArea.Bag; index = -1;
        if (data.VariantType != Variant.Type.Dictionary) return false;
        var values = data.AsGodotDictionary();
        if (!values.TryGetValue("type", out Variant type) || type.VariantType != Variant.Type.String || type.AsString() != "inventory_weapon" ||
            !values.TryGetValue("inventory", out Variant owner) || owner.VariantType != Variant.Type.Object || owner.AsGodotObject() != _inventory ||
            !values.TryGetValue("weapon_id", out Variant id) || id.VariantType != Variant.Type.String ||
            !values.TryGetValue("source_area", out Variant area) || area.VariantType != Variant.Type.Int ||
            !values.TryGetValue("source_index", out Variant cell) || cell.VariantType != Variant.Type.Int) return false;
        weaponId = id.AsString(); source = (InventoryDragArea)area.AsInt32(); index = cell.AsInt32();
        return true;
    }

    private bool CanDrop(Variant data, InventoryDragArea target, int targetIndex)
        => ReadDrag(data, out string id, out InventoryDragArea source, out int index) &&
           _inventory.CanDropWeapon(id, source, index, target, targetIndex);

    private void Drop(Variant data, InventoryDragArea target, int targetIndex)
    {
        if (!ReadDrag(data, out string id, out InventoryDragArea source, out int index)) return;
        bool success = _inventory.TryDropWeapon(id, source, index, target, targetIndex);
        _selectedId = id;
        _message.Text = success ? target switch
        {
            InventoryDragArea.CurrentWeapon => $"已切换为{_inventory.Find(id)?.DisplayName}。",
            InventoryDragArea.Equipment => "已调整装备位置。",
            _ => source == InventoryDragArea.Bag ? "已调整行囊位置。" : "武器已卸下并收回行囊。"
        } : _equipment.IsBusy ? "收招后再调整装备。" : "至少保留一把武器。";
        Refresh();
    }

    private void UpdateDragFeedback()
    {
        if (Owner == null || _dropHighlight == null) return;
        Viewport viewport = Owner.GetViewport();
        bool dragging = viewport.GuiIsDragging();
        _dropHighlight.Visible = false;
        if (!dragging)
        {
            if (_dragActive && !viewport.GuiIsDragSuccessful()) _message.Text = "已取消拖拽，物品保持原位。";
            _dragActive = false;
            return;
        }
        Variant data = viewport.GuiGetDragData();
        if (!ReadDrag(data, out _, out _, out _)) return;
        _dragActive = true;
        Control hovered = viewport.GuiGetHoveredControl();
        if (hovered == null) return;
        foreach (var target in _dropTargets)
        {
            // Use native GUI hit-testing, including scroll clipping and child buttons.
            if (hovered != target.Control && !target.Control.IsAncestorOf(hovered)) continue;
            bool allowed = CanDrop(data, target.Area, target.Index);
            Transform2D transform = _panel.GetGlobalTransformWithCanvas().AffineInverse() * target.Control.GetGlobalTransformWithCanvas();
            _dropHighlight.Position = transform.Origin;
            _dropHighlight.Size = target.Control.Size * transform.Scale;
            _dropHighlight.AddThemeStyleboxOverride("panel", InventoryUiSkin.Box(allowed ? "71936622" : "b8694a22", allowed ? "658b63" : "b56749", 3));
            _dropHighlight.Visible = true;
            _message.Text = allowed ? target.Area switch
            {
                InventoryDragArea.CurrentWeapon => "松开鼠标，切换当前武器。",
                InventoryDragArea.Equipment => "松开鼠标，装备或交换槽位。",
                _ => "松开鼠标，放入这个行囊格。"
            } : _equipment.IsBusy ? "收招后再调整装备。" : "至少保留一把武器。";
            break;
        }
    }

    private static T Add<T>(T node, Node parent, float x, float y, float w, float h) where T : Control
    {
        parent.AddChild(node);
        node.Position = new Vector2(x, y);
        node.Size = new Vector2(w, h);
        return node;
    }

    private void Panel(float x, float y, float w, float h, string fill, string border)
    {
        var panel = Add(new Panel { MouseFilter = Control.MouseFilterEnum.Ignore }, _panel, x, y, w, h);
        panel.AddThemeStyleboxOverride("panel", InventoryUiSkin.Box(fill, border));
    }

    private Label Text(string text, float x, float y, float w, float h, int size = 18, Color? color = null)
        => ChildText(_panel, text, x, y, w, h, size, color);

    private static Label ChildText(Node parent, string text, float x, float y, float w, float h, int size = 18, Color? color = null)
    {
        var label = Add(new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center }, parent, x, y, w, h);
        label.AddThemeFontSizeOverride("font_size", size);
        if (color.HasValue) label.AddThemeColorOverride("font_color", color.Value);
        return label;
    }

    private static TextureRect Picture(Node parent, Texture2D texture, float x, float y, float w, float h)
        => Add(new TextureRect { Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore, TextureFilter = CanvasItem.TextureFilterEnum.Nearest }, parent, x, y, w, h);

    private void Square(float x, float y, float w, string color, float? h = null)
        => Add(new ColorRect { Color = new Color(color), MouseFilter = Control.MouseFilterEnum.Ignore }, _panel, x, y, w, h ?? w);
}
