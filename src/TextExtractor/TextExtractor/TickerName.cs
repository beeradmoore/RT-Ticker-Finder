using System.Text.Json.Serialization;

namespace TextExtractor;

public class TickerName
{
	[JsonPropertyName("name")]
	public string Name { get; set; } = string.Empty;
	
	[JsonPropertyName("index")]
	public int Index { get; set; }
}