using System;

[Title( "Cube Scale Animator" )]
[Category( "Type Racer" )]
[Icon( "animation" )]
public sealed class CubeScaleAnimator : Component
{
	[Property]
	public float TargetScale { get; set; } = 1f;

	[Property]
	public float Speed { get; set; } = 8f;

	[Property]
	public bool DestroyOnComplete { get; set; } = false;

	private bool _isComplete;

	public bool IsComplete => _isComplete;

	protected override void OnUpdate()
	{
		if ( _isComplete )
			return;

		var current = LocalScale.x;
		var next = MathX.Lerp( current, TargetScale, Time.Delta * Speed );
		LocalScale = next;

		if ( MathF.Abs( next - TargetScale ) < 0.01f )
		{
			LocalScale = TargetScale;
			_isComplete = true;

			if ( DestroyOnComplete )
				Destroy();
		}
	}

	public void Reset()
	{
		_isComplete = false;
		LocalScale = 0.01f;
	}
}
