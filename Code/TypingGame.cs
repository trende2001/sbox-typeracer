using System;

[Title( "Typing Game" )]
[Category( "Type Racer" )]
[Icon( "keyboard" )]
public sealed class TypingGame : Component
{
	[Property, TextArea]
	public string TargetText { get; set; } = "the quick brown fox jumps over the lazy dog";

	[Property, Range( 5f, 100f )]
	public float LetterSpacing { get; set; } = 22.0f;

	[Property, Range( 6f, 128f )]
	public float FontSize { get; set; } = 32f;

	[Property]
	public string CorrectSound { get; set; } = "ui.button.press";

	[Property]
	public string WrongSound { get; set; } = "ui.navigate";

	[Property, Range( 0.05f, 1f )]
	public float ErrorFlashDuration { get; set; } = 0.15f;

	[Property, Range( 5f, 100f )]
	public float CubeSize { get; set; } = 20.0f;

	[Property, Range( 1f, 30f )]
	public float CubeGrowSpeed { get; set; } = 10f;

	[Property]
	public float CubeDepthOffset { get; set; } = -5.0f;

	[Property]
	public Color CompletedCubeColor { get; set; } = new Color( 0.76f, 0.55f, 0.22f );

	[Property]
	public Color CurrentLetterHighlightColor { get; set; } = Color.Cyan;

	[Property, Range( 1f, 20f )]
	public float CameraFollowSpeed { get; set; } = 5.0f;

	[Property]
	public Vector3 CameraOffset { get; set; } = new Vector3( -200f, 0f, 50f );

	public int CurrentIndex { get; private set; }
	public int TotalCharacters => TargetText?.Length ?? 0;
	public float WPM { get; private set; }
	public float ElapsedTime => _started ? Time.Now - _startTime : 0f;
	public bool IsFinished { get; private set; }

	private List<GameObject> _letterObjects = new();
	private List<TextRenderer> _letterRenderers = new();
	private TextRenderer _flashRenderer;
	private float _flashClearTime;
	private float _startTime;
	private bool _started;
	private CameraComponent _camera;

	protected override void OnStart()
	{
		SpawnLetters();
		_camera = Scene.GetAllComponents<CameraComponent>().FirstOrDefault( c => c.IsMainCamera );
		
		var inputPanel = Scene.GetAllComponents<TypingInputPanel>().FirstOrDefault();
		if ( inputPanel is not null )
			inputPanel.OnCharTyped = HandleCharTyped;
	}

	protected override void OnUpdate()
	{
		ClearExpiredErrorFlash();
		UpdateCameraFollow();
		DrawCurrentLetterHighlight();
	}

	protected override void OnDestroy()
	{
		var inputPanel = Scene.GetAllComponents<TypingInputPanel>().FirstOrDefault();
		if ( inputPanel is not null )
			inputPanel.OnCharTyped = null;
	}

	private void SpawnLetters()
	{
		foreach ( var go in _letterObjects )
		{
			if ( go.IsValid() )
				go.Destroy();
		}

		_letterObjects.Clear();
		_letterRenderers.Clear();

		if ( string.IsNullOrEmpty( TargetText ) )
			return;

		for ( int i = 0; i < TargetText.Length; i++ )
		{
			var letterGo = new GameObject( GameObject, true, $"Letter_{i}" );
			letterGo.Flags |= GameObjectFlags.Absolute;

			var renderer = letterGo.AddComponent<TextRenderer>();
			renderer.TextScope = new TextRendering.Scope( TargetText[i].ToString(), Color.White, FontSize, "Poppins", 400 );

			letterGo.WorldPosition = GameObject.WorldPosition + new Vector3(
				0f,
				-i * LetterSpacing,
				0f
			);

			letterGo.WorldRotation = Rotation.Identity;

			letterGo.NetworkSpawn(Connection.Host);
			
			_letterObjects.Add( letterGo );
			_letterRenderers.Add( renderer );
		}
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
		if ( _camera is null || !_camera.IsValid() || _letterObjects.Count == 0 )
			return;

		int targetIndex = Math.Min( CurrentIndex, _letterObjects.Count - 1 );
		var targetGo = _letterObjects[targetIndex];

		if ( !targetGo.IsValid() )
			return;

		var targetPosition = targetGo.WorldPosition + CameraOffset;

		_camera.WorldPosition = Vector3.Lerp( _camera.WorldPosition, targetPosition, Time.Delta * CameraFollowSpeed );
		_camera.WorldRotation = Rotation.LookAt( targetGo.WorldPosition - _camera.WorldPosition, Vector3.Up );
	}

	private void DrawCurrentLetterHighlight()
	{
		if ( IsFinished || CurrentIndex >= _letterObjects.Count )
			return;

		var currentGo = _letterObjects[CurrentIndex];
		if ( !currentGo.IsValid() )
			return;

		var boxCenter = currentGo.WorldPosition + new Vector3( CubeDepthOffset, 0f, 0f );
		var box = BBox.FromPositionAndSize( boxCenter, new Vector3( CubeSize ) );

		DebugOverlay.Box( box, CurrentLetterHighlightColor, duration: 0f );
	}

	private void HandleCharTyped( char typed )
	{
		if ( IsFinished || CurrentIndex >= TargetText.Length )
			return;

		if ( typed == TargetText[CurrentIndex] )
			HandleCorrectKey();
		else
			HandleWrongKey();
	}

	private void HandleCorrectKey()
	{
		if ( !_started )
		{
			_started = true;
			_startTime = Time.Now;
		}

		SpawnTypedCube( CurrentIndex );
		Sound.Play( CorrectSound, _letterObjects[CurrentIndex].WorldPosition );

		CurrentIndex++;

		UpdateWPM();

		if ( CurrentIndex >= TargetText.Length )
			FinishGame();
	}

	private void HandleWrongKey()
	{
		if ( _flashRenderer is not null && _flashRenderer != _letterRenderers[CurrentIndex] )
			_flashRenderer.Color = Color.White;

		_flashRenderer = _letterRenderers[CurrentIndex];
		_flashRenderer.Color = Color.Red;
		_flashClearTime = Time.Now + ErrorFlashDuration;

		Sound.Play( WrongSound, _letterObjects[CurrentIndex].WorldPosition );
	}

	private void SpawnTypedCube( int index )
	{
		var letterGo = _letterObjects[index];
		if ( !letterGo.IsValid() )
			return;

		var cubeGo = new GameObject( letterGo, true, "TypedCube" );
		cubeGo.LocalPosition = new Vector3( CubeDepthOffset, 0f, 0f );
		cubeGo.LocalScale = new Vector3( 0.01f );

		var modelRenderer = cubeGo.AddComponent<ModelRenderer>();
		modelRenderer.Model = Model.Cube;
		modelRenderer.Tint = CompletedCubeColor;

		var animator = cubeGo.AddComponent<CubeScaleAnimator>();
		animator.TargetScale = CubeSize;
		animator.Speed = CubeGrowSpeed;

		cubeGo.NetworkSpawn(Connection.Host);
	}

	private void FinishGame()
	{
		IsFinished = true;
		UpdateWPM();
	}

	private void UpdateWPM()
	{
		if ( !_started || CurrentIndex == 0 )
			return;

		float elapsed = Time.Now - _startTime;
		if ( elapsed <= 0f )
			return;

		WPM = (CurrentIndex / 5.0f) / (elapsed / 60.0f);
	}
}
