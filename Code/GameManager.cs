using System;
using System.Threading.Tasks;
using Sandbox.Network;

[Title( "Game Manager" )]
[Category( "Type Racer" )]
[Icon( "sports_esports" )]
public sealed class GameManager : Component, Component.INetworkListener
{
	public static GameManager Instance { get; private set; }

	[Property] public GameObject PlayerStatePrefab { get; set; }

	[Property] public bool StartServer { get; set; } = true;

	[Property, TextArea] public string TargetText { get; set; } = "the quick brown fox jumps over the lazy dog";

	[Property, Range( 50f, 200f )] public float RowSpacing { get; set; } = 80.0f;

	[Sync] public string SyncedTargetText { get; private set; }

	[Sync] public bool RaceStarted { get; private set; }

	[Sync] public float RaceStartTime { get; private set; }

	private List<PlayerState> _players = new();

	protected override void OnAwake()
	{
		Instance = this;
	}

	private int NextFreeRowIndex()
	{
		var usedIndices = new HashSet<int>( _players.Select( p => p.RowIndex ) );
		int slot = 0;
		while ( usedIndices.Contains( slot ) )
			slot++;
		return slot;
	}

	private void CompactRowIndices()
	{
		var sorted = _players
			.Where( p => p.IsValid() )
			.OrderBy( p => p.RowIndex )
			.ToList();

		for ( int i = 0; i < sorted.Count; i++ )
			sorted[i].RowIndex = i;
	}

	protected override async Task OnLoad()
	{
		if ( Scene.IsEditor )
			return;

		if ( !StartServer || Networking.IsActive )
			return;

		LoadingScreen.Title = "Creating Lobby";
		await Task.DelayRealtimeSeconds( 0.1f );
		Networking.CreateLobby( new LobbyConfig() );
	}

	protected override void OnStart()
	{
		if ( !Networking.IsHost )
			return;

		var passage = QuoteManager.Instance?.GetRandomQuote();
		SyncedTargetText = !string.IsNullOrEmpty( passage ) ? passage : TargetText;
	}

	public void OnActive( Connection channel )
	{
		Log.Info( $"Player '{channel.DisplayName}' joined the race" );

		if ( !PlayerStatePrefab.IsValid() )
		{
			Log.Warning( "GameManager: PlayerStatePrefab not set" );
			return;
		}

		int playerIndex = NextFreeRowIndex();
		var spawnPosition = new Transform( WorldPosition );

		var playerGo = PlayerStatePrefab.Clone( spawnPosition, name: $"Player - {channel.DisplayName}" );
		playerGo.NetworkSpawn( channel );

		var playerState = playerGo.GetComponent<PlayerState>();
		if ( playerState is not null )
		{
			playerState.DisplayName = channel.DisplayName;
			playerState.RowIndex = playerIndex;
			_players.Add( playerState );
		}
	}

	public void OnDisconnected( Connection channel )
	{
		Log.Info( $"Player '{channel.DisplayName}' left the race" );

		var playerState = _players.FirstOrDefault( p => p.IsValid() && p.GameObject.Network.OwnerId == channel.Id );
		if ( playerState is not null )
		{
			_players.Remove( playerState );
			playerState.GameObject.Destroy();
			CompactRowIndices();
		}
	}

	[Rpc.Broadcast]
	public void StartRace()
	{
		if ( !Networking.IsHost )
			return;

		var passage = QuoteManager.Instance?.GetRandomQuote();
		if ( !string.IsNullOrEmpty( passage ) )
			SyncedTargetText = passage;

		RaceStarted = true;
		RaceStartTime = Time.Now;

		foreach ( var player in _players )
		{
			if ( player.IsValid() )
				player.ResetProgress();
		}
	}

	public IEnumerable<PlayerState> GetAllPlayers()
	{
		_players.RemoveAll( p => !p.IsValid() );
		return _players;
	}

	public PlayerState GetLocalPlayer()
	{
		return Scene.GetAllComponents<PlayerState>().FirstOrDefault( p => p.Network.IsOwner );
	}
}
