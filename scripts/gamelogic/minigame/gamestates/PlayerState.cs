using System.Text.Json.Serialization;

public class PlayerState
{
    [JsonInclude]
    public int Hp = 100;
}