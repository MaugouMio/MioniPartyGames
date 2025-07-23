using System;
using System.Collections.Generic;

public class UserData
{
	public ushort UID { get; set; } = 0;
	private string name = "";
	public string Name
	{
		get
		{
			if (GameData.Instance.IsUserNameDuplicated(name))
				return $"{name}({UID})"; // 如果名字重複，顯示UID以區分
			return name;
		}
		set
		{
			GameData.Instance.RemoveUserName(name);
			GameData.Instance.AddUserName(value);
			name = value;
		}
	}
	public string GetOriginalName() { return name; }
}

public class PlayerData
{
	public ushort UID { get; set; } = 0;
	public string Question { get; set; } = "";
	public bool QuestionLocked { get; set; } = false;
	public short SuccessRound { get; set; } = 0;
	public List<string> GuessHistory { get; set; } = new List<string>();

	public void Reset()
	{
		Question = "";
		QuestionLocked = false;
		SuccessRound = 0;
		GuessHistory.Clear();
	}

	public void AddGuessRecord(string guess, byte result)
	{
		if (result == 1)
			GuessHistory.Add($"<color=#00ba00>✓</color> 是{guess}");
		else
			GuessHistory.Add($"<color=#ba0000>×</color> 不是{guess}");
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
	public const uint GAME_VERSION = 3;
	public const uint CLIENT_GAME_SUB_VERSION = 1;

	public const int MAX_CHAT_RECORD = 30;
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

	public string ServerName { get; set; } = "";


	public ushort SelfUID { get; set; } = 0;
	public int RoomID { get; set; } = 0;
	public Dictionary<ushort, UserData> UserDatas { get; set; } = new Dictionary<ushort, UserData>();
	public Dictionary<ushort, PlayerData> PlayerDatas { get; set; } = new Dictionary<ushort, PlayerData>();
	public bool IsCountingDownStart { get; set; } = false;
	public GameState CurrentState { get; set; } = GameState.WAITING;
	public List<ushort> PlayerOrder { get; set; } = new List<ushort>();
	public byte GuessingPlayerIndex { get; set; } = 0;
	public string VotingGuess { get; set; } = "";
	public Dictionary<ushort, byte> Votes { get; set; } = new Dictionary<ushort, byte>();
	public Queue<string> ChatRecord { get; set; } = new Queue<string>();
	public Queue<string> EventRecord { get; set; } = new Queue<string>();

	private Dictionary<string, int> userNameCount = new Dictionary<string, int>();

	public void Reset()
	{
		SelfUID = 0;
		UserDatas.Clear();
		PlayerDatas.Clear();
		ChatRecord.Clear();
		EventRecord.Clear();
		PlayerOrder.Clear();
		userNameCount.Clear();

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

	public ushort GetCurrentPlayerUID()
	{
		if (GuessingPlayerIndex >= PlayerOrder.Count)
			return 0;
		return PlayerOrder[GuessingPlayerIndex];
	}

	public void AddChatRecord(string message, bool isHidden)
	{
		if (ChatRecord.Count >= MAX_CHAT_RECORD)
			ChatRecord.Dequeue();

		if (isHidden)
			ChatRecord.Enqueue($"<color=red>㊙︎</color>{message}");
		else
			ChatRecord.Enqueue(message);

		if (GamePage.Instance != null)
			GamePage.Instance.UpdateChatList(true);
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

	public void AddUserName(string name)
	{
		if (userNameCount.ContainsKey(name))
			userNameCount[name]++;
		else
			userNameCount[name] = 1;
	}

	public void RemoveUserName(string name)
	{
		if (userNameCount.ContainsKey(name))
		{
			if (--userNameCount[name] <= 0)
				userNameCount.Remove(name);
		}
	}

	public bool IsPlayer()
	{
		return PlayerDatas.ContainsKey(SelfUID);
	}

	public bool IsUserNameDuplicated(string name)
	{
		if (userNameCount.ContainsKey(name))
			return userNameCount[name] > 1;
		return false;
	}

	public bool IsOthersAllGuessed()
	{
		foreach (var player in PlayerDatas.Values)
		{
			if (player.UID != SelfUID && player.SuccessRound == 0)
				return false;
		}
		return true;
	}
}
