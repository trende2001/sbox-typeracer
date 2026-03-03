using System.Text.Json;
using System.Text.Json.Serialization;

public enum QuoteDifficulty
{
	Any,
	Short,
	Medium,
	Long,
	Epic
}

[Title( "Quote Manager" )]
[Category( "Type Racer" )]
[Icon( "format_quote" )]
public sealed class QuoteManager : Component
{
	public static QuoteManager Instance { get; private set; }

	[Property]
	public QuoteDifficulty Difficulty { get; set; } = QuoteDifficulty.Any;

	private List<Quote> _allQuotes = new();
	private List<Quote> _filteredQuotes = new();

	private class Quote
	{
		[JsonPropertyName( "text" )]
		public string Text { get; set; }

		[JsonPropertyName( "source" )]
		public string Source { get; set; }

		[JsonPropertyName( "length" )]
		public int Length { get; set; }

		[JsonPropertyName( "id" )]
		public int Id { get; set; }
	}

	private class QuoteFile
	{
		[JsonPropertyName( "quotes" )]
		public List<Quote> Quotes { get; set; }
	}

	protected override void OnAwake()
	{
		Instance = this;
		LoadQuotes();
	}

	private void LoadQuotes()
	{
		var file = FileSystem.Mounted.ReadAllText( "resources/english_quotes.json" );
		if ( string.IsNullOrEmpty( file ) )
		{
			Log.Warning( "QuoteManager: could not read english_quotes.json" );
			return;
		}

		var parsed = JsonSerializer.Deserialize<QuoteFile>( file );
		if ( parsed?.Quotes is null || parsed.Quotes.Count == 0 )
		{
			Log.Warning( "QuoteManager: no quotes found in file" );
			return;
		}

		_allQuotes = parsed.Quotes;
		ApplyDifficultyFilter();

		Log.Info( $"QuoteManager: loaded {_allQuotes.Count} quotes, {_filteredQuotes.Count} match difficulty {Difficulty}" );
	}

	private void ApplyDifficultyFilter()
	{
		_filteredQuotes = Difficulty switch
		{
			QuoteDifficulty.Short  => _allQuotes.Where( q => q.Length <= 100 ).ToList(),
			QuoteDifficulty.Medium => _allQuotes.Where( q => q.Length is > 100 and <= 300 ).ToList(),
			QuoteDifficulty.Long   => _allQuotes.Where( q => q.Length is > 300 and <= 600 ).ToList(),
			QuoteDifficulty.Epic   => _allQuotes.Where( q => q.Length > 600 ).ToList(),
			_                      => _allQuotes.ToList()
		};

		if ( _filteredQuotes.Count == 0 )
		{
			Log.Warning( $"QuoteManager: no quotes matched difficulty {Difficulty}, falling back to all quotes" );
			_filteredQuotes = _allQuotes.ToList();
		}
	}

	public string GetRandomQuote()
	{
		if ( _filteredQuotes.Count == 0 )
			return string.Empty;

		return _filteredQuotes[Game.Random.Int( 0, _filteredQuotes.Count - 1 )].Text;
	}
}
