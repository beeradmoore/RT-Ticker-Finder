// See https://aka.ms/new-console-template for more information


using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Buffered;


async Task<int> GenerateTickerRawText()
{
	try
	{
		await Cli.Wrap("tesseract")
			.WithArguments(["--version"])
			.ExecuteBufferedAsync();
	}
	catch (Exception err)
	{
		Console.WriteLine($"Error: Is tesseract installed?\n{err.Message}");
		return 1;
	}

	var tickerFiles = Directory.GetFiles("/Volumes/Storage/RT Archive/final stream/ticker/", "*.png").ToList();
	tickerFiles.Sort();

	var tickerTextResults = new Dictionary<int, string>();
	var tickerNumberRegex = new Regex("^ticker_(?<number>\\d*).png$");
	var tickerIndexes = new List<int>();
	var parallelOptions = new ParallelOptions()
	{
		MaxDegreeOfParallelism = Environment.ProcessorCount,
	};

	var dictionaryLock = new object();
	await Parallel.ForEachAsync(tickerFiles, parallelOptions, async (tickerFile, token) =>
	{

		var fileName = Path.GetFileName(tickerFile);
		var match = tickerNumberRegex.Match(fileName);
		if (match.Success == false)
		{
			throw new Exception($"Could not get ticker index for file {tickerFile}");
		}

		if (int.TryParse(match.Groups["number"].ValueSpan, out int number) == true)
		{
			Console.WriteLine($"Processing: {fileName}");
			var result = await Cli.Wrap("tesseract")
				.WithArguments([tickerFile, "-", "--psm", "7"])
				//.WithWorkingDirectory("/Volumes/Storage/RT Archive/final stream/ticker")
				.ExecuteBufferedAsync();
			if (result.ExitCode != 0)
			{
				throw new Exception("Could not get ticker text.");
			}

			lock (dictionaryLock)
			{
				tickerIndexes.Add(number);
				tickerTextResults[number] = result.StandardOutput;
			}
		}
		else
		{
			throw new Exception("Could not get ticker index.");
		}
	});

	tickerIndexes.Sort();

	File.WriteAllText("ticker_raw_text.txt", "");
	foreach (var index in tickerIndexes)
	{
		File.AppendAllText("ticker_raw_text.txt", tickerTextResults[index]);
	}

	Debugger.Break();

	return 0;
}

async Task<int> GenerateBlueTickerRawText()
{
	try
	{
		await Cli.Wrap("tesseract")
			.WithArguments(["--version"])
			.ExecuteBufferedAsync();
	}
	catch (Exception err)
	{
		Console.WriteLine($"Error: Is tesseract installed?\n{err.Message}");
		return 1;
	}

	var tickerFiles = Directory.GetFiles("/Volumes/Storage/RT Archive/final stream/ticker_blue/", "*.png").ToList();
	tickerFiles.Sort();

	var tickerTextResults = new Dictionary<int, string>();
	var tickerNumberRegex = new Regex("^ticker_blue_(?<number>\\d*).png$");
	var tickerIndexes = new List<int>();
	var parallelOptions = new ParallelOptions()
	{
		MaxDegreeOfParallelism = Environment.ProcessorCount,
	};

	var dictionaryLock = new object();
	await Parallel.ForEachAsync(tickerFiles, parallelOptions, async (tickerFile, token) =>
	{

		var fileName = Path.GetFileName(tickerFile);
		var match = tickerNumberRegex.Match(fileName);
		if (match.Success == false)
		{
			throw new Exception($"Could not get ticker index for file {tickerFile}");
		}

		if (int.TryParse(match.Groups["number"].ValueSpan, out int number) == true)
		{
			Console.WriteLine($"Processing: {fileName}");
			var result = await Cli.Wrap("tesseract")
				.WithArguments([tickerFile, "-", "--psm", "4"]) // 
				.ExecuteBufferedAsync();
			if (result.ExitCode != 0)
			{
				throw new Exception("Could not get ticker text.");
			}

			lock (dictionaryLock)
			{
				tickerIndexes.Add(number);
				tickerTextResults[number] = result.StandardOutput;
			}
		}
		else
		{
			throw new Exception("Could not get ticker index.");
		}
	});

	tickerIndexes.Sort();

	File.WriteAllText("ticker_blue_raw_text.txt", "");
	foreach (var index in tickerIndexes)
	{
		File.AppendAllText("ticker_blue_raw_text.txt", tickerTextResults[index].Replace("\n", " ") + "\n");
	}

	Debugger.Break();

	return 0;
}

async Task<int> ParseTickerRawText()
{
	var tickerRawLines = File.ReadAllLines("ticker_raw_text.txt");
	
	var nameIndexes = new Dictionary<string, List<int>>();

	var nameSplitRegex = new Regex("^(.*?)|(?<names>.*)|");
	
	for (var i = 0; i < tickerRawLines.Length; ++i)
	{
		var line = tickerRawLines[i];
		
		// Replease seperators with our new seperator of |
		line = line.Replace(" = ", "|");
		line = line.Replace("= ", "|");
		line = line.Replace(" =", "|");
		line = line.Replace(" - ", "|");
		line = line.Replace("- ", "|");
		line = line.Replace(" -", "|");
		
		// There should not be any spaces in names so lets also remove them
		line = line.Replace(" ", String.Empty);
		
		// Only check lines with -
		if (line.Contains("|") == false)
		{
			continue;
		}
		
		
		var match = nameSplitRegex.Match(line);
		if (match.Success == false)
		{
			//Debugger.Break();
			continue;
		}

		var names = line.Split("|", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
		
		foreach (var name in names)
		{
			// According to site names need to be less than 3 characters
			if (name.Length < 3)
			{
				//Debugger.Break();
				continue;
			}
			
			// They can also only contain a-z, A-Z, 0-9, ., -, _
			// But according to https://business-service.roosterteeth.com/api/v1/users/validate_attributes
			// it does not appear that they can start or end with a special character.
			var cleanName = name.Trim('.', '_', '-', '!');
			
			if (nameIndexes.ContainsKey(cleanName) == false)
			{
				nameIndexes[cleanName] = new List<int>() { i };
			}
			else
			{
				var list = nameIndexes[cleanName];
				if (list.Contains(i) == false && list.Contains(i - 1) == false && list.Contains(i - 2) == false)
				{
					nameIndexes[cleanName].Add(i);
				}
			}		
		}
	}

	var json = System.Text.Json.JsonSerializer.Serialize(nameIndexes, new System.Text.Json.JsonSerializerOptions() { WriteIndented = true });
	File.WriteAllText("ticker_names.json", json);
	
	Debugger.Break();
	
	return 0;
}

int ParseTickerBlueRawText()
{
	var tickerRawLines = File.ReadAllLines("ticker_blue_raw_text.txt");
	var stringBuffer = new StringBuilder();
	var lastTopLine = string.Empty;
	var tickerNumberRegex = new Regex("^ticker_blue_(?<number>\\d*)");
	var nameRegex = new Regex("^(?<name>.*?):");
	var nameIndexes = new Dictionary<string, List<int>>();

	for (var i = 0; i < tickerRawLines.Length; ++i)
	{
		var line = tickerRawLines[i];
		if (line.StartsWith("ticker_blue_"))
		{
			if (string.IsNullOrEmpty(lastTopLine))
			{
				lastTopLine = line;
			}
			else
			{
				lastTopLine = line;
				var textData = stringBuffer.ToString().Replace("：", ":");
				stringBuffer.Clear();
				var match = tickerNumberRegex.Match(lastTopLine);
				if (match.Success == false)
				{
					throw new Exception("This shouldn't happen...");
				}
				
				var nameMatch = nameRegex.Match(textData);
				if (nameMatch.Success == false)
				{
					throw new Exception("This also shouldn't happen...");
				}
				
				if (int.TryParse(match.Groups["number"].ValueSpan, out int number) == true)
				{
					var cleanName = nameMatch.Groups["name"].Value.Trim().Replace(" ", string.Empty);
					
					if (nameIndexes.ContainsKey(cleanName) == false)
					{
						nameIndexes[cleanName] = new List<int>() { i };
					}
					else
					{
						var list = nameIndexes[cleanName];
						if (list.Contains(i) == false && list.Contains(i - 1) == false && list.Contains(i - 2) == false)
						{
							nameIndexes[cleanName].Add(i);
						}
					}	

				}

			}
		}
		else
		{
			stringBuffer.AppendLine(line);
		}
	}
	
	var json = System.Text.Json.JsonSerializer.Serialize(nameIndexes, new System.Text.Json.JsonSerializerOptions() { WriteIndented = true });
	File.WriteAllText("ticker_blue_names.json", json);
	
	return 1;
}

// This code is not meant to be run as a single application, this is just me doing stuff to get the raw data.

// Ticker images were already extracted with 
// ffmpeg "Why We Were Here [QL4X5aOazR0].mp4" -r 0.125 -vf "fps=1,crop=1380:50:530:1030" ticker/ticker_%04d.png    

// This generated the ticker_raw_text.txt already added to the project.
//await GenerateTickerRawText();

//await ParseTickerRawText();

// Do the same for questions overlaid 
//await GenerateBlueTickerRawText();

// But GenerateBlueTickerRawText sucked, so I did it in siri shortcuts and made ticker_blue_raw_text.txt
ParseTickerBlueRawText();

return 0;