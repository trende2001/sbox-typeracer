using System;

[Title( "Typing Game" )]
[Category( "Type Racer" )]
[Icon( "keyboard" )]
public sealed class TypingGame : Component
{
	[Property, Range( 5f, 100f )] public float LetterSpacing { get; set; } = 22.0f;

	[Property, Range( 6f, 128f )] public float FontSize { get; set; } = 32f;

	[Property] public string CorrectSound { get; set; } = "ui.button.press";

	[Property] public string WrongSound { get; set; } = "ui.navigate";

	[Property, Range( 0.05f, 1f )] public float ErrorFlashDuration { get; set; } = 0.15f;

	[Property, Range( 5f, 100f )] public float CubeSize { get; set; } = 20.0f;

	[Property, Range( 1f, 30f )] public float CubeGrowSpeed { get; set; } = 10f;

	[Property] public float CubeDepthOffset { get; set; } = -5.0f;

	[Property] public Color CompletedCubeColor { get; set; } = new Color( 0.76f, 0.55f, 0.22f );

	[Property] public Color CurrentLetterHighlightColor { get; set; } = Color.Cyan;

	[Property] public Color OtherPlayerHighlightColor { get; set; } = new Color( 1f, 0.5f, 0f, 0.5f );

	[Property, Range( 1f, 20f )] public float CameraFollowSpeed { get; set; } = 5.0f;

	[Property] public Vector3 CameraOffset { get; set; } = new Vector3( -200f, 0f, 50f );

	[Property] public GameObject TypedLetterCubePrefab { get; set; }

	private GameManager _gameManager;
	private Dictionary<Guid, PlayerRowData> _playerRows = new();
	private TextRenderer _flashRenderer;
	private float _flashClearTime;
	private CameraComponent _camera;
	private string _currentTargetText;

	private class PlayerRowData
	{
		public List<GameObject> LetterObjects = new();
		public List<TextRenderer> LetterRenderers = new();
		public List<bool> CubeSpawned = new();
		public int LastKnownIndex;
	}

	protected override void OnStart()
	{
		_gameManager = Scene.GetAllComponents<GameManager>().FirstOrDefault();
		_camera = Scene.GetAllComponents<CameraComponent>().FirstOrDefault( c => c.IsMainCamera );

		var inputPanel = Scene.GetAllComponents<TypingInputPanel>().FirstOrDefault();
		if ( inputPanel is not null )
			inputPanel.OnCharTyped = HandleCharTyped;
	}

	protected override void OnUpdate()
	{
		SyncTargetText();
		SyncPlayerRows();
		ClearExpiredErrorFlash();
		UpdateCameraFollow();
		DrawAllPlayerHighlights();
	}

	protected override void OnDestroy()
	{
		var inputPanel = Scene.GetAllComponents<TypingInputPanel>().FirstOrDefault();
		if ( inputPanel is not null )
			inputPanel.OnCharTyped = null;

		ClearAllRows();
	}

	private void SyncTargetText()
	{
		if ( _gameManager is null || !_gameManager.IsValid() )
			return;

		var newText = _gameManager.SyncedTargetText;
		if ( string.IsNullOrEmpty( newText ) || newText == _currentTargetText )
			return;

		_currentTargetText = newText;
		ClearAllRows();
	}

	private void SyncPlayerRows()
	{
		if ( _gameManager is null || !_gameManager.IsValid() )
			return;

		var players = Scene.GetAllComponents<PlayerState>();
		var activePlayerIds = new HashSet<Guid>();

		foreach ( var player in players )
		{
			if ( !player.IsValid() )
				continue;

			var playerId = player.Network.OwnerId;
			activePlayerIds.Add( playerId );

			if ( !_playerRows.ContainsKey( playerId ) )
				SpawnRowForPlayer( player );

			UpdateRowPosition( player );
			UpdatePlayerCubes( player );
		}

		var toRemove = _playerRows.Keys.Where( id => !activePlayerIds.Contains( id ) ).ToList();
		foreach ( var id in toRemove )
			RemovePlayerRow( id );
	}

	private void SpawnRowForPlayer( PlayerState player )
	{
		if ( string.IsNullOrEmpty( _currentTargetText ) || _gameManager is null )
			return;

		var playerId = player.Network.OwnerId;
		var rowData = new PlayerRowData();

		for ( int i = 0; i < _currentTargetText.Length; i++ )
		{
			var letterGo = new GameObject( GameObject, true, $"Letter_{playerId}_{i}" );

			var renderer = letterGo.AddComponent<TextRenderer>();
			renderer.TextScope = new TextRendering.Scope(
				_currentTargetText[i].ToString(),
				Color.White,
				FontSize,
				"Poppins",
				400
			);

			letterGo.WorldRotation = Rotation.Identity;

			rowData.LetterObjects.Add( letterGo );
			rowData.LetterRenderers.Add( renderer );
			rowData.CubeSpawned.Add( false );
		}

		_playerRows[playerId] = rowData;
	}

	private void UpdateRowPosition( PlayerState player )
	{
		if ( _gameManager is null )
			return;

		var playerId = player.Network.OwnerId;
		if ( !_playerRows.TryGetValue( playerId, out var rowData ) )
			return;

		var basePosition = GameObject.WorldPosition;
		var rowOffset = new Vector3( -player.RowIndex * _gameManager.RowSpacing, 0f, 0f );

		for ( int i = 0; i < rowData.LetterObjects.Count; i++ )
		{
			var letterGo = rowData.LetterObjects[i];
			if ( !letterGo.IsValid() )
				continue;

			var targetPosition = basePosition + rowOffset + new Vector3( 0f, -i * LetterSpacing, 0f );
			letterGo.WorldPosition = targetPosition;
		}
	}

	private void UpdatePlayerCubes( PlayerState player )
	{
		var playerId = player.Network.OwnerId;
		if ( !_playerRows.TryGetValue( playerId, out var rowData ) )
			return;

		while ( rowData.LastKnownIndex < player.CurrentIndex && rowData.LastKnownIndex < rowData.LetterObjects.Count )
		{
			int idx = rowData.LastKnownIndex;
			if ( !rowData.CubeSpawned[idx] )
			{
				string letter = idx < _currentTargetText.Length ? _currentTargetText[idx].ToString() : "";
				SpawnTypedCube( rowData.LetterObjects[idx], letter );
				rowData.CubeSpawned[idx] = true;
			}

			rowData.LastKnownIndex++;
		}
	}

	private void RemovePlayerRow( Guid playerId )
	{
		if ( !_playerRows.TryGetValue( playerId, out var rowData ) )
			return;

		foreach ( var go in rowData.LetterObjects )
		{
			if ( go.IsValid() )
				go.Destroy();
		}

		_playerRows.Remove( playerId );
	}

	private void ClearAllRows()
	{
		foreach ( var kvp in _playerRows )
		{
			foreach ( var go in kvp.Value.LetterObjects )
			{
				if ( go.IsValid() )
					go.Destroy();
			}
		}

		_playerRows.Clear();
	}

	private void ClearExpiredErrorFlash()
	{
		if ( _flashRenderer is null || Time.Now < _flashClearTime )
			return;

		_flashRenderer.Color = Color.White;
		_flashRenderer = null;
	}

	private void UpdateCameraFollow()
	{
		if ( _camera is null || !_camera.IsValid() )
			return;

		var localPlayer = _gameManager?.GetLocalPlayer();
		if ( localPlayer is null || !localPlayer.IsValid() )
			return;

		var playerId = localPlayer.Network.OwnerId;
		if ( !_playerRows.TryGetValue( playerId, out var rowData ) || rowData.LetterObjects.Count == 0 )
			return;

		int targetIndex = Math.Min( localPlayer.CurrentIndex, rowData.LetterObjects.Count - 1 );
		var targetGo = rowData.LetterObjects[targetIndex];

		if ( !targetGo.IsValid() )
			return;

		var targetPosition = targetGo.WorldPosition + CameraOffset;
		_camera.WorldPosition = Vector3.Lerp( _camera.WorldPosition, targetPosition, Time.Delta * CameraFollowSpeed );
		_camera.WorldRotation = Rotation.LookAt( targetGo.WorldPosition - _camera.WorldPosition, Vector3.Up );
	}

	private void DrawAllPlayerHighlights()
	{
		var localPlayer = _gameManager?.GetLocalPlayer();
		var localPlayerId = localPlayer?.Network.OwnerId ?? Guid.Empty;

		foreach ( var player in Scene.GetAllComponents<PlayerState>() )
		{
			if ( !player.IsValid() || player.IsFinished )
				continue;

			var playerId = player.Network.OwnerId;
			if ( !_playerRows.TryGetValue( playerId, out var rowData ) )
				continue;

			if ( player.CurrentIndex >= rowData.LetterObjects.Count )
				continue;

			var currentGo = rowData.LetterObjects[player.CurrentIndex];
			if ( !currentGo.IsValid() )
				continue;

			var boxCenter = currentGo.WorldPosition + new Vector3( CubeDepthOffset, 0f, 0f );
			var box = BBox.FromPositionAndSize( boxCenter, new Vector3( 50f ) );

			var color = playerId == localPlayerId ? CurrentLetterHighlightColor : OtherPlayerHighlightColor;
			DebugOverlay.Box( box, color, duration: 0f );
		}
	}

	private void HandleCharTyped( char typed )
	{
		var localPlayer = _gameManager?.GetLocalPlayer();
		if ( localPlayer is null || !localPlayer.IsValid() || localPlayer.IsFinished )
			return;

		if ( string.IsNullOrEmpty( _currentTargetText ) )
			return;

		if ( localPlayer.CurrentIndex >= _currentTargetText.Length )
			return;

		if ( char.ToLowerInvariant( typed ) == char.ToLowerInvariant( _currentTargetText[localPlayer.CurrentIndex] ) )
			HandleCorrectKey( localPlayer );
		else
			HandleWrongKey( localPlayer );
	}

	private void HandleCorrectKey( PlayerState player )
	{
		var playerId = player.Network.OwnerId;
		if ( !_playerRows.TryGetValue( playerId, out var rowData ) )
			return;

		int idx = player.CurrentIndex;
		if ( idx < rowData.LetterObjects.Count )
			Sound.Play( CorrectSound, rowData.LetterObjects[idx].WorldPosition );

		player.AdvanceIndex( _currentTargetText.Length );
	}

	private void HandleWrongKey( PlayerState player )
	{
		var playerId = player.Network.OwnerId;
		if ( !_playerRows.TryGetValue( playerId, out var rowData ) )
			return;

		int idx = player.CurrentIndex;
		if ( idx >= rowData.LetterRenderers.Count )
			return;

		if ( _flashRenderer is not null && _flashRenderer != rowData.LetterRenderers[idx] )
			_flashRenderer.Color = Color.White;

		_flashRenderer = rowData.LetterRenderers[idx];
		_flashRenderer.Color = Color.Red;
		_flashClearTime = Time.Now + ErrorFlashDuration;

		Sound.Play( WrongSound, rowData.LetterObjects[idx].WorldPosition );

		CameraShaker.Instance?.ShakeAndZoom( ZoomDirection.Out );
	}

	private void SpawnTypedCube( GameObject letterGo, string letter )
	{
		if ( !letterGo.IsValid() )
			return;

		if ( TypedLetterCubePrefab.IsValid() )
		{
			var cubeGo = TypedLetterCubePrefab.Clone();
			cubeGo.Parent = letterGo;
			cubeGo.LocalPosition = new Vector3( CubeDepthOffset, 0f, 0f );
			cubeGo.LocalScale = new Vector3( 0.01f );

			var typedCube = cubeGo.GetComponent<TypedLetterCube>();
			typedCube?.Initialize( letter, CompletedCubeColor, CubeSize, CubeGrowSpeed, FontSize );
			return;
		}

		var fallbackGo = new GameObject( letterGo, true, "TypedCube" );
		fallbackGo.LocalPosition = new Vector3( CubeDepthOffset, 0f, 0f );
		fallbackGo.LocalScale = new Vector3( 0.01f );

		var modelRenderer = fallbackGo.AddComponent<ModelRenderer>();
		modelRenderer.Model = Model.Cube;
		modelRenderer.Tint = CompletedCubeColor;

		var animator = fallbackGo.AddComponent<CubeScaleAnimator>();
		animator.TargetScale = CubeSize;
		animator.Speed = CubeGrowSpeed;
	}

	public Vector3 GetLetterWorldPosition( Guid playerId, int index )
	{
		if ( !_playerRows.TryGetValue( playerId, out var rowData ) )
			return Vector3.Zero;

		if ( index < 0 || index >= rowData.LetterObjects.Count )
			return Vector3.Zero;

		var go = rowData.LetterObjects[index];
		return go.IsValid() ? go.WorldPosition : Vector3.Zero;
	}
}
