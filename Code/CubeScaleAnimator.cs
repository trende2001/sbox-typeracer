using System;
public sealed class CubeScaleAnimator : Component
{
	public float TargetScale { get; set; } = 1f;
	public float Speed { get; set; } = 8f;

	protected override void OnUpdate()
	{
		var current = LocalScale.x;
		var next = MathX.Lerp( current, TargetScale, Time.Delta * Speed );
		LocalScale = next;

		if ( MathF.Abs( next - TargetScale ) < 0.01f )
		{
			LocalScale = TargetScale;
			Destroy();
		}
	}
}
