using System;
using System.Globalization;
using System.Linq;
using Framework;
using Framework.UI;
using Godot;

namespace GameLogic;

[Window(UILayer.Modal, "res://assets/scenes/ui/dialogue_window.tscn", fullScreen: true)]
public sealed class DialogueWindow : UIWindow
{
    DialogueModule _module;
    DialogueSession _session;
    IInputModule _input;
    RichTextLabel _body, _historyText;
    Label _title, _hint, _prompt;
    Control _choiceOverlay, _historyOverlay, _toolbar;
    VBoxContainer _choices;
    Button _historyButton, _historyClose, _autoButton, _cancelButton;
    string _page = "", _remainder = "";
    double _visible, _autoWait;
    bool _auto, _renderPending;
    int _total;
    Vector2 _layoutSize;
    public bool IsRevealing => _body != null && _body.VisibleCharacters < _total;
    public string PageText => _body?.Text ?? "";
    public bool HasMorePages => _remainder.Length > 0;
    public int ChoiceCount => _choices?.GetChildCount() ?? 0;
    public override void BindMemberProperty()
    {
        _body = Owner.GetNode<RichTextLabel>("%Body"); _title = Owner.GetNode<Label>("%Title");
        _body.VisibleCharactersBehavior = TextServer.VisibleCharactersBehavior.CharsAfterShaping;
        _hint = Owner.GetNode<Label>("%Hint"); _toolbar = Owner.GetNode<Control>("%Toolbar");
        _choiceOverlay = Owner.GetNode<Control>("%ChoiceOverlay"); _choices = Owner.GetNode<VBoxContainer>("%Choices");
        _prompt = Owner.GetNode<Label>("%ChoicePrompt"); _historyOverlay = Owner.GetNode<Control>("%HistoryOverlay");
        _historyText = Owner.GetNode<RichTextLabel>("%HistoryText");
        _historyButton = Owner.GetNode<Button>("%HistoryButton"); _historyClose = Owner.GetNode<Button>("%HistoryClose");
        _autoButton = Owner.GetNode<Button>("%AutoButton"); _cancelButton = Owner.GetNode<Button>("%CancelButton");
        _input = ModuleSystem.GetModule<IInputModule>();
    }
    public override void RegisterEvent()
    {
        Owner.GuiInput += OnGuiInput;
        _historyButton.Pressed += ToggleHistory; _historyClose.Pressed += ToggleHistory;
        _autoButton.Pressed += ToggleAuto; _cancelButton.Pressed += Cancel;
    }
    protected override void OnRefresh()
    {
        Detach();
        _module = (DialogueModule)UserDatas[0]; _session = (DialogueSession)UserDatas[1];
        _session.Changed += RenderCurrent;
        _title.Text = _session.Title + "  /  " + _session.NpcName;
        _cancelButton.Visible = _session.AllowCancel;
        _renderPending = true;
        Owner.GrabFocus();
    }
    protected override void OnUpdate(double delta)
    {
        if (_session == null || _session.IsComplete) return;
        if (_input.IsJustPressed("ui_cancel", "UI") && _input.TryConsumeJustPressed("ui_cancel", "UI"))
        {
            if (_historyOverlay.Visible) ToggleHistory(); else Cancel();
            return;
        }
        if (Owner.Size != _layoutSize)
        {
            _layoutSize = Owner.Size;
            float scale = Mathf.Clamp(Mathf.Min(Owner.Size.X / 1920, Owner.Size.Y / 1080), .55f, 1.5f);
            int fontSize = Mathf.RoundToInt(30 * scale);
            _body.AddThemeFontSizeOverride("normal_font_size", fontSize);
            _body.AddThemeConstantOverride("line_separation", Mathf.RoundToInt(16 * scale));
            _historyText.AddThemeFontSizeOverride("normal_font_size", Mathf.RoundToInt(24 * scale));
            // Reflow only the unread/current page; history remains the complete source text.
            if (_body.Text.Length > 0 && _body.GetContentHeight() > _body.Size.Y)
            { _remainder = _page + _remainder; _page = ""; FillPage(); }
        }
        if (_renderPending && _body.Size.X > 0 && _body.Size.Y > 0) { _renderPending = false; RenderCurrent(); }
        if (_historyOverlay.Visible || _choiceOverlay.Visible) return;
        if (IsRevealing)
        {
            _visible += delta * _session.CharactersPerSecond;
            _body.VisibleCharacters = Math.Min(_total, (int)_visible);
            _autoWait = 0;
        }
        else if (_auto)
        {
            _autoWait += delta;
            if (_autoWait >= 1.2 + Math.Min(3, _total * .025)) Advance();
        }
        _hint.Text = IsRevealing ? "点击 / 空格  ·  显示全文" : (_remainder.Length > 0 ? "点击 / 空格  ·  翻页  ▽" : "点击 / 空格  ·  继续  ▽");
    }
    void RenderCurrent()
    {
        if (_body.Size.X <= 0 || _body.Size.Y <= 0) { _renderPending = true; return; }
        _autoWait = 0;
        ClearChoices();
        if (_session.Current is DialogueLineNodeData line)
        {
            if (line.NewPage) _page = "";
            string speaker = _session.Resolve(line.Speaker);
            string text = _session.Resolve(line.Text);
            _remainder = (_page.Length > 0 ? "\n\n" : "") + (speaker.Length == 0 ? text : speaker + "　「" + text + "」");
            FillPage();
        }
        else if (_session.Current is DialogueChoiceNodeData choice)
        {
            _prompt.Text = _session.Resolve(choice.Prompt);
            foreach (var option in _session.Options)
            {
                int port = option.Port;
                var button = new Button
                {
                    Name = "Option" + port, Text = option.Text, ThemeTypeVariation = "DialogueChoice",
                    CustomMinimumSize = new(0, 58), FocusMode = Control.FocusModeEnum.All,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart
                };
                button.Pressed += () => _session.Choose(port);
                _choices.AddChild(button);
            }
            _body.Hide(); _choiceOverlay.Show(); _hint.Text = "选择一项继续";
            _choices.GetChildren().OfType<Button>().FirstOrDefault()?.GrabFocus();
        }
    }
    void FillPage()
    {
        int alreadyVisible = _page.EnumerateRunes().Count();
        string candidate = _page + _remainder;
        _body.Text = candidate;
        if (_body.GetContentHeight() > _body.Size.Y && _page.Length > 0)
        {
            _page = ""; alreadyVisible = 0; _remainder = _remainder.TrimStart('\n'); candidate = _remainder;
            _body.Text = candidate;
        }
        if (_body.GetContentHeight() > _body.Size.Y)
        {
            // Use the same shaped label to measure CJK wrapping, preserving grapheme boundaries.
            int[] boundaries = StringInfo.ParseCombiningCharacters(candidate);
            int low = 1, high = boundaries.Length;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                int end = middle == boundaries.Length ? candidate.Length : boundaries[middle];
                _body.Text = candidate[..end];
                if (_body.GetContentHeight() <= _body.Size.Y) low = middle; else high = middle - 1;
            }
            int length = low == boundaries.Length ? candidate.Length : boundaries[low];
            _page = candidate[..length]; _remainder = candidate[length..];
            _body.Text = _page;
        }
        else { _page = candidate; _remainder = ""; }
        _total = _body.GetTotalCharacterCount(); _visible = Math.Min(alreadyVisible, _total);
        _body.VisibleCharacters = (int)_visible; _autoWait = 0;
    }
    public void Advance()
    {
        if (_session == null || _session.IsComplete || _historyOverlay.Visible || _choiceOverlay.Visible) return;
        if (IsRevealing) { _visible = _total; _body.VisibleCharacters = _total; _autoWait = 0; return; }
        if (_remainder.Length > 0) { _page = ""; FillPage(); return; }
        _session.Advance();
    }
    void OnGuiInput(InputEvent input)
    {
        bool advance = input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } ||
            input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space or Key.Enter or Key.KpEnter or Key.Right } ||
            input is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.A };
        if (!advance) return;
        Owner.AcceptEvent(); Advance();
    }
    void ToggleHistory()
    {
        _historyOverlay.Visible = !_historyOverlay.Visible; _autoWait = 0;
        _toolbar.Visible = !_historyOverlay.Visible;
        _choiceOverlay.Visible = !_historyOverlay.Visible && _session.Current is DialogueChoiceNodeData;
        _body.Visible = !_historyOverlay.Visible && !_choiceOverlay.Visible;
        _hint.Visible = !_historyOverlay.Visible;
        if (_historyOverlay.Visible)
        {
            _historyText.Text = string.Join("\n\n", _session.History.Select(h => (h.IsChoice ? "▸ " : "") + (h.Speaker.Length > 0 ? h.Speaker + "\n" : "") + h.Text));
            _historyClose.GrabFocus();
        }
        else if (_choiceOverlay.Visible) _choices.GetChildren().OfType<Button>().FirstOrDefault()?.GrabFocus();
        else Owner.GrabFocus();
    }
    void ToggleAuto()
    {
        _auto = !_auto; _autoWait = 0; _autoButton.Text = _auto ? "自动  ON" : "自动  OFF";
        if (!_choiceOverlay.Visible) Owner.GrabFocus();
    }
    void Cancel() { if (_session?.AllowCancel == true) _module.Cancel(); }
    void ClearChoices()
    {
        _choiceOverlay.Hide();
        _body.Show();
        foreach (var child in _choices.GetChildren()) { _choices.RemoveChild(child); child.QueueFree(); }
        Owner.GrabFocus();
    }
    void Detach() { if (_session != null) _session.Changed -= RenderCurrent; }
    protected override void OnDestroy()
    {
        Detach();
        Owner.GuiInput -= OnGuiInput;
        _historyButton.Pressed -= ToggleHistory; _historyClose.Pressed -= ToggleHistory;
        _autoButton.Pressed -= ToggleAuto; _cancelButton.Pressed -= Cancel;
        _module?.PresentationClosed(_session);
        _session = null; _module = null;
    }
}
