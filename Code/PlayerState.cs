using System;

[Title( "Player State" )]
[Category( "Type Racer" )]
[Icon( "person" )]
public sealed class PlayerState : Component, Component.INetworkSpawn
{
	[Sync, Property] public string DisplayName { get; set; }

	[Sync] public int CurrentIndex { get; set; }

	[Sync, Property] public float WPM { get; set; }

	[Sync, Property] public bool IsFinished { get; set; }

	[Sync( SyncFlags.FromHost )] public int RowIndex { get; set; }

	public float StartTime { get; private set; }
	public bool HasStarted { get; private set; }

	public void OnNetworkSpawn( Connection owner )
	{
		DisplayName = owner.DisplayName;
	}

	public void AdvanceIndex( int totalChars )
	{
		if ( !Network.IsOwner )
			return;

		if ( !HasStarted )
		{
			HasStarted = true;
			StartTime = Time.Now;
		}

		CurrentIndex++;
		UpdateWPM();

		if ( CurrentIndex >= totalChars )
			IsFinished = true;
	}

	public void ResetProgress()
	{
		if ( !Network.IsOwner )
			return;

		CurrentIndex = 0;
		WPM = 0f;
		IsFinished = false;
		HasStarted = false;
		StartTime = 0f;
	}

	public void UpdateWPM()
	{
		if ( !HasStarted || CurrentIndex == 0 )
			return;

		float elapsed = Time.Now - StartTime;
		if ( elapsed <= 0f )
			return;

		WPM = (CurrentIndex / 5.0f) / (elapsed / 60.0f);
	}

	public float GetElapsedTime()
	{
		if ( !HasStarted )
			return 0f;

		return Time.Now - StartTime;
	}
}
