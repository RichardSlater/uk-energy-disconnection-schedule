<Query Kind="Statements">
  <NuGetReference>Newtonsoft.Json</NuGetReference>
  <Namespace>System.Drawing</Namespace>
  <Namespace>Newtonsoft.Json</Namespace>
</Query>

var path = @"C:\Source\uk-energy-disconnection-schedule\source-data\";
var staticOutput = @"C:\Source\uk-energy-disconnection-schedule\";
var files = Directory.GetFiles(path, "*.png");
var highlightPen = new Pen(Color.LightGray, 2);
var pointPen = new Pen(Color.Orange, 2);
var outagePen = new Pen(Color.Red, 2);
var noOutagePen = new Pen(Color.Gray, 2);

var blocks = new List<char> { 'A', 'B', 'C', 'D', 'E', 'G', 'H', 'J', 'K', 'L', 'M', 'N', 'P', 'Q', 'R', 'S', 'T', 'U' };
var days = new List<string> { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

blocks.Dump();

var allOutages = new List<Outage>();
var font = new Font("Arial", 8.0f);
var brush = new SolidBrush(Color.Black);

var warnings = new List<string>();

foreach (var file in files)
{
	var level = Int32.Parse(file.Replace(path, String.Empty).Replace(".png", String.Empty));
	using (var originalImage = new Bitmap(file))
	using (var image = Image.FromFile(file))
	using (var graphics = Graphics.FromImage(image))
	{
		var dayOffsetY = (int)Math.Round(image.Height / 8.5f, 0);;
		var dayWidth = (int)Math.Round(image.Width / 8.473251, 0);
		var dayHeight = (int)Math.Round(image.Height / 1.17, 0);
		var dayOffsetX = (int)Math.Round(image.Width / 15.6, 0);
		var numPeriods = 8;

		var blockOffset = dayHeight / (blocks.Count() - 1);
		var periodOffset = dayWidth / (numPeriods - 1);
		var interDayPadding = dayWidth / 7;

		foreach (var day in days) {
			var dayOfWeekOffset = dayOffsetX + ((days.IndexOf(day)) * (dayWidth + interDayPadding)) ;
			graphics.DrawRectangle(highlightPen, new Rectangle(dayOfWeekOffset, dayOffsetY, dayWidth, dayHeight));
			for (var period = 0; period < numPeriods; period++){
				foreach (var block in blocks) {
					var pointX = dayOfWeekOffset + (periodOffset * period);
					var pointY = dayOffsetY + (blockOffset * blocks.IndexOf(block));
					graphics.DrawRectangle(pointPen, pointX - 1, pointY - 1, 3, 3);
					Outage outage = null;

					if (pointX > image.Width || pointY > image.Height)
					{
						warnings.Add($"Out of range at Level {level} on {day} period {period} for block {block} could not be identified.");
						continue;
					}
						
					if (originalImage.GetPixel(pointX, pointY).R == Color.Black.R)
					{
						graphics.DrawRectangle(outagePen, pointX - 1, pointY - 1, 3, 3);
						outage = new Outage(level, day, block, period + 1, true);
						graphics.DrawString(outage.ToString(), font, brush, pointX + 15, pointY - 7);
					}

					if (originalImage.GetPixel(pointX, pointY).R == Color.White.R)
					{
						graphics.DrawRectangle(noOutagePen, pointX - 1, pointY - 1, 3, 3);
						outage = new Outage(level, day, block, period + 1, false);
					}

					if (outage == null)
					{
						warnings.Add($"Outage at Level {level} on {day} period {period} for block {block} could not be identified.");
					}
					else
					{
						allOutages.Add(outage);
					}
				}
			}
		}

		//image.Dump($"Level {level} ({allOutages.Count(x => x.Level == level && x.IsOutage)} Outages)");
	}
}
	
warnings.Dump();

if (warnings.Count == 0)
{
	var apiPath = Path.Join(staticOutput, "data");
	var byBlockPath = Path.Join(apiPath, "block");
	var byLevelPath = Path.Join(apiPath, "level");
	
	if (Directory.Exists(byBlockPath))
	{
		Directory.Delete(byBlockPath, true);
	}

	if (Directory.Exists(byLevelPath))
	{
		Directory.Delete(byLevelPath, true);
	}

	Directory.CreateDirectory(byBlockPath);
	Directory.CreateDirectory(byLevelPath);

	var allBlocks = allOutages.Where(x => x.IsOutage).GroupBy(x => x.Block);
	var allBlockIndex = Path.Join(byBlockPath, $"index.json");
	using (var file = File.CreateText(allBlockIndex))
	{
		var serializer = new JsonSerializer();
		serializer.Formatting = Newtonsoft.Json.Formatting.Indented;
		serializer.Serialize(file, allBlocks.Select(x => x.Key));
	}
	
	foreach (var block in allBlocks) {
		var blockPath = Path.Join(byBlockPath, block.Key.ToString());
		var blockIndex = Path.Join(byBlockPath, $"{block.Key}/index.json");
		Directory.CreateDirectory(blockPath);

		using (var file = File.CreateText(blockIndex))
		{
			var serializer = new JsonSerializer();
			serializer.Formatting = Newtonsoft.Json.Formatting.Indented;

			serializer.Serialize(file, block);
		}

		foreach (var level in block.GroupBy(x => x.Level))
		{
			var levelPath = Path.Join(blockPath, $"{level.Key}.json");

			using (var file = File.CreateText(levelPath))
			{
				var serializer = new JsonSerializer();
				serializer.Formatting = Newtonsoft.Json.Formatting.Indented;
				
				serializer.Serialize(file, level);
			}
		}
	}

	foreach (var level in allOutages.Where(x => x.IsOutage).GroupBy(x => x.Level))
	{
		var levelPath = Path.Join(byLevelPath, level.Key.ToString());
		var levelIndex = Path.Join(byLevelPath, "index.json");
		
		Directory.CreateDirectory(levelPath);

		using (var file = File.CreateText(levelIndex))
		{
			var serializer = new JsonSerializer();
			serializer.Formatting = Newtonsoft.Json.Formatting.Indented;

			serializer.Serialize(file, level);
		}

		foreach (var block in level.GroupBy(x => x.Block))
		{
			var blockPath = Path.Join(levelPath, $"{block.Key}.json");

			using (var file = File.CreateText(blockPath))
			{
				var serializer = new JsonSerializer();
				serializer.Formatting = Newtonsoft.Json.Formatting.Indented;

				serializer.Serialize(file, level);
			}
		}
	}
}

public class Outage
{
	[JsonProperty("level")]
	public int Level { get; private set; }
	
	[JsonProperty("day")]
	public string Day { get; private set; }
	
	[JsonProperty("block")]
	public char Block { get; private set; }
	
	[JsonProperty("period")]
	public int Period { get; private set; }
	
	[JsonIgnore]
	public bool IsOutage { get; private set; }
	
	[JsonProperty("times")]
	public TimeRange Times
	{
		get { return new TimeRange(Period); }
	}
	
	[JsonProperty("timeframe")]
	public String TimeText
	{
		get { return Times.ToString(); }
	}

	public Outage(int level, string day, char block, int period, bool outage) {
		this.Level = level;
		this.Day = day;
		this.Block = block;
		this.Period = period;
		this.IsOutage = outage;
	}

	public override string ToString()
	{
		return $"{Level}:{Day},{Block},{Period}={IsOutage}";
	}
}

public class TimeRange {

	[JsonProperty("start")]
	public TimeSpan Start { get; set; }
	
	[JsonProperty("end")]
	public TimeSpan End { get; set; }
	
	public TimeRange (int period) {
		Start = TimeSpan.FromHours((period - 1) * 3) + TimeSpan.FromMinutes(30);
		End = Start + TimeSpan.FromHours(3);
	}

	public override string ToString()
	{
		return $"{Start:hh\\:mm}-{End:hh\\:mm}";
	}
}