using System.Text.Json.Serialization;
using Framework;
using Godot;

public enum LogType
{
    Error,
    Warning,
    Log
}

public class DebugLog : ActionBase
{
    [JsonInclude]
    public string message;
    public LogType logType {get;set;}= LogType.Log;

    public override void Execute()
    {
        switch (logType)
            {
                case LogType.Error:
                    Debugger.Error(message);
                    break;
                case LogType.Warning:
                     Debugger.Warn(message);
                    break;
                case LogType.Log:
                    Debugger.Info(message);
                    break;
            }
    }

    public override string Description => logType.ToString() + message;

    public override Control CreateEditUI()
    {
        var hbox = new HBoxContainer();

        var execOption = new OptionButton();
        execOption.AddItem("错误", (int)LogType.Error);
        execOption.AddItem("警告", (int)LogType.Warning);
        execOption.AddItem("信息", (int)LogType.Log);
        execOption.Selected = (int)logType;
        execOption.ItemSelected += (idx) => logType = (LogType)(int)idx;
        hbox.AddChild(execOption);

        var messageInput = new LineEdit
        {
            Text = message ?? "",
            PlaceholderText = "输入日志消息",
            CustomMinimumSize = new Vector2(200, 0)
        };
        messageInput.TextChanged += (text) => message = text;
        hbox.AddChild(messageInput);

        return hbox;
    }
}