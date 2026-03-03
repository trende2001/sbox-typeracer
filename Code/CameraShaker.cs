using System;

public enum ZoomDirection
{
	None,
	In,
	Out
}

[Title( "Camera Shaker" )]
[Category( "Type Racer" )]
[Icon( "vibration" )]
public sealed class CameraShaker : Component
{
	public static CameraShaker Instance { get; private set; }

	[Property, Range( 0.01f, 1f )]
	public float DefaultDuration { get; set; } = 0.15f;

	[Property, Range( 1f, 50f )]
	public float DefaultIntensity { get; set; } = 8f;

	[Property, Range( 10f, 100f )]
	public float ShakeFrequency { get; set; } = 30f;

	[Property, Range( 1f, 200f )]
	public float DefaultZoomIntensity { get; set; } = 50f;

	private CameraComponent _camera;
	private Vector3 _originalPosition;
	private float _originalFov;
	private float _shakeEndTime;
	private float _currentIntensity;
	private float _currentZoomIntensity;
	private ZoomDirection _zoomDirection;
	private float _effectDuration;
	private bool _isActive;

	protected override void OnAwake()
	{
		Instance = this;
	}

	protected override void OnStart()
	{
		_camera = GameObject.GetComponent<CameraComponent>();
	}

	protected override void OnUpdate()
	{
		if ( !_isActive )
			return;

		if ( Time.Now >= _shakeEndTime )
		{
			StopEffect();
			return;
		}

		ApplyEffect();
	}

	public void Shake()
	{
		Shake( DefaultIntensity, DefaultDuration );
	}

	public void Shake( float intensity )
	{
		Shake( intensity, DefaultDuration );
	}

	public void Shake( float intensity, float duration )
	{
		StartEffect( intensity, 0f, ZoomDirection.None, duration );
	}

	public void Zoom( ZoomDirection direction )
	{
		Zoom( direction, DefaultZoomIntensity, DefaultDuration );
	}

	public void Zoom( ZoomDirection direction, float intensity )
	{
		Zoom( direction, intensity, DefaultDuration );
	}

	public void Zoom( ZoomDirection direction, float intensity, float duration )
	{
		StartEffect( 0f, intensity, direction, duration );
	}

	public void ShakeAndZoom( ZoomDirection direction )
	{
		ShakeAndZoom( DefaultIntensity, direction, DefaultZoomIntensity, DefaultDuration );
	}

	public void ShakeAndZoom( float shakeIntensity, ZoomDirection direction, float zoomIntensity, float duration )
	{
		StartEffect( shakeIntensity, zoomIntensity, direction, duration );
	}

	private void StartEffect( float shakeIntensity, float zoomIntensity, ZoomDirection direction, float duration )
	{
		if ( !_isActive )
		{
			_originalPosition = GameObject.LocalPosition;
			if ( _camera is not null )
				_originalFov = _camera.FieldOfView;
		}

		_currentIntensity = shakeIntensity;
		_currentZoomIntensity = zoomIntensity;
		_zoomDirection = direction;
		_effectDuration = duration;
		_shakeEndTime = Time.Now + duration;
		_isActive = true;
	}

	private void ApplyEffect()
	{
		float remainingTime = _shakeEndTime - Time.Now;
		float progress = 1f - (remainingTime / _effectDuration);
		float falloff = 1f - progress;

		ApplyShake( falloff );
		ApplyZoom( progress, falloff );
	}

	private void ApplyShake( float falloff )
	{
		if ( _currentIntensity <= 0f )
			return;

		float timeScale = Time.Now * ShakeFrequency;
		float offsetX = (MathF.Sin( timeScale * 1.1f ) + MathF.Sin( timeScale * 2.3f )) * 0.5f;
		float offsetY = (MathF.Cos( timeScale * 1.7f ) + MathF.Cos( timeScale * 2.9f )) * 0.5f;

		var shakeOffset = new Vector3( offsetX, offsetY, 0f ) * _currentIntensity * falloff;
		GameObject.LocalPosition = _originalPosition + shakeOffset;
	}

	private void ApplyZoom( float progress, float falloff )
	{
		if ( _camera is null || _zoomDirection == ZoomDirection.None )
			return;

		float zoomCurve = MathF.Sin( progress * MathF.PI );
		float zoomMultiplier = _zoomDirection == ZoomDirection.Out ? 1f : -1f;
		float fovOffset = zoomCurve * _currentZoomIntensity * zoomMultiplier;

		_camera.FieldOfView = _originalFov + fovOffset;
	}

	private void StopEffect()
	{
		_isActive = false;
		GameObject.LocalPosition = _originalPosition;

		if ( _camera is not null )
			_camera.FieldOfView = _originalFov;
	}
}
