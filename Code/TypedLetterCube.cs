using System;

[Title( "Typed Letter Cube" )]
[Category( "Type Racer" )]
[Icon( "square" )]
public sealed class TypedLetterCube : Component
{
	[Property] public ModelRenderer CubeRenderer { get; set; }

	[Property] public TextRenderer LetterRenderer { get; set; }

	[Property] public CubeScaleAnimator ScaleAnimator { get; set; }

	public void Initialize( string letter, Color cubeColor, float targetScale, float growSpeed, float fontSize )
	{
		if ( CubeRenderer is not null )
			CubeRenderer.Tint = cubeColor;

		if ( LetterRenderer is not null )
		{
			LetterRenderer.TextScope = new TextRendering.Scope(
				letter,
				LetterRenderer.Color,
				LetterRenderer.FontSize,
				"Poppins",
				400
			);
		}

		if ( ScaleAnimator is not null )
		{
			ScaleAnimator.TargetScale = targetScale;
			ScaleAnimator.Speed = growSpeed;
		}
	}
}
