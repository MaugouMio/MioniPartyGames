using System;
using System.Collections.Generic;

public class UserData
{
	public ushort UID { get; set; } = 0;
	public string Name { get; set; } = "";
}

public class PlayerData
{
	public ushort UID { get; set; } = 0;
	public string Question { get; set; } = "";
	public ushort SuccessRound { get; set; } = 0;
	public List<string> GuessHistory { get; set; } = new List<string>();

	public void Reset()
	{
		Question = "";
		SuccessRound = 0;
		GuessHistory.Clear();
	}

	public void AddGuessRecord(string guess, byte result)
	{
		if (result == 1)
			GuessHistory.Add($"<color=#00ba00>✔</color> 是{guess}");
		else
			GuessHistory.Add($"<color=#ba0000>✘</color> 不是{guess}");
	}
}

public enum GameState
{
	WAITING,    // 可以加入遊戲的階段
	PREPARING,  // 遊戲剛開始的出題階段
	GUESSING,   // 某個玩家猜題當中
	VOTING,     // 某個玩家猜測一個類別，等待其他人投票是否符合
}

public class GameData
{
	public const int MAX_EVENT_RECORD = 30;

	private static GameData instance;
	public static GameData Instance
	{
		get
		{
			if (instance == null)
				instance = new GameData();
			return instance;
		}
	}

	public ushort SelfUID { get; set; } = 0;
	public Dictionary<ushort, UserData> UserDatas { get; set; } = new Dictionary<ushort, UserData>();
	public Dictionary<ushort, PlayerData> PlayerDatas { get; set; } = new Dictionary<ushort, PlayerData>();
	public bool IsCountingDownStart { get; set; } = false;
	public GameState CurrentState { get; set; } = GameState.WAITING;
	public List<ushort> PlayerOrder { get; set; } = new List<ushort>();
	public byte GuessingPlayerIndex { get; set; } = 0;
	public string VotingGuess { get; set; } = "";
	public Dictionary<ushort, byte> Votes { get; set; } = new Dictionary<ushort, byte>();
	public Queue<string> EventRecord { get; set; } = new Queue<string>();

	public void Reset()
	{
		SelfUID = 0;
		UserDatas.Clear();
		PlayerDatas.Clear();
		EventRecord.Clear();
		PlayerOrder.Clear();
		GuessingPlayerIndex = 0;
		VotingGuess = "";
		Votes.Clear();
		ResetGame();
	}
	public void ResetGame()
	{
		IsCountingDownStart = false;
		foreach (var player in PlayerDatas.Values)
			player.Reset();
	}

	public void AddEventRecord(string eventText)
	{
		if (EventRecord.Count >= MAX_EVENT_RECORD)
			EventRecord.Dequeue();

		string timeText = DateTime.Now.ToString("HH:mm:ss");
		EventRecord.Enqueue($"[{timeText}] {eventText}");

		if (GamePage.Instance != null)
			GamePage.Instance.UpdateEventList(true);
	}
}
