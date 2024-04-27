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


// This code is not meant to be run as a single application, this is just me doing stuff to get the raw data.

// Ticker images were already extracted with 
// ffmpeg -ss 0:10:55 -i live.mkv -to 6:19:30 -r 0.125 -vf "fps=1,crop=1380:50:530:1030" ticker/ticker_%04d.png

// This generated the ticker_raw_text.txt already added to the project.
//await GenerateTickerRawText();

// foxfire43, ShadowSax

return 0;