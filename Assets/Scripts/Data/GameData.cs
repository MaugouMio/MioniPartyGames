using System;
using System.Collections.Generic;
using System.Linq;

public enum GameType
{
	GUESS_WORD = 1,
	ARRANGE_NUMBER,
}

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

public abstract class PlayerData
{
	public ushort UID { get; set; } = 0;
	public abstract void Reset();
}

public class GuessWordPlayerData : PlayerData
{
	public string Question { get; set; } = "";
	public bool QuestionLocked { get; set; } = false;
	public short SuccessRound { get; set; } = 0;
	public List<string> GuessHistory { get; set; } = new List<string>();

	public override void Reset()
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

public class ArrangeNumberPlayerData : PlayerData
{
	public List<ushort> LeftNumbers { get; set; } = new List<ushort>();
	public bool IsUrgent = false;

	public override void Reset()
	{
		LeftNumbers.Clear();
		IsUrgent = false;
	}
}

public enum GuessWordState
{
	WAITING,    // 可以加入遊戲的階段
	PREPARING,  // 遊戲剛開始的出題階段
	GUESSING,   // 某個玩家猜題當中
	VOTING,     // 某個玩家猜測一個類別，等待其他人投票是否符合
}

public enum ArrangeNumberState
{
	WAITING,    // 可以加入與設定遊戲的階段
	PLAYING,    // 遊戲進行中
}

public class GuessWordGameData
{
	public GuessWordState CurrentState { get; set; } = GuessWordState.WAITING;
	public List<ushort> PlayerOrder { get; set; } = new List<ushort>();
	public byte GuessingPlayerIndex { get; set; } = 0;
	public string VotingGuess { get; set; } = "";
	public Dictionary<ushort, byte> Votes { get; set; } = new Dictionary<ushort, byte>();

	public void Reset()
	{
		PlayerOrder.Clear();
		GuessingPlayerIndex = 0;
		VotingGuess = "";
		Votes.Clear();
	}

	public ushort GetCurrentPlayerUID()
	{
		if (GuessingPlayerIndex >= PlayerOrder.Count)
			return 0;
		return PlayerOrder[GuessingPlayerIndex];
	}

	public bool IsOthersAllGuessed()
	{
		foreach (var player in GameData.Instance.PlayerDatas.Values)
		{
			if (player is not GuessWordPlayerData)
				continue;

			GuessWordPlayerData gwPlayer = player as GuessWordPlayerData;
			if (gwPlayer.UID != GameData.Instance.SelfUID && gwPlayer.SuccessRound == 0)
				return false;
		}
		return true;
	}
}

public class ArrangeNumberData
{
	public ArrangeNumberState CurrentState { get; set; } = ArrangeNumberState.WAITING;
	public ushort MaxNumber { get; set; } = 100;
	public byte NumberGroupCount { get; set; } = 1;
	public byte NumberPerPlayer { get; set; } = 1;

	public ushort LastPlayerUID = 0;
	public ushort CurrentNumber = 0;

	public void Reset()
	{
		LastPlayerUID = 0;
		CurrentNumber = 0;
	}

	public string GetLastPlayerName()
	{
		if (!GameData.Instance.UserDatas.TryGetValue(LastPlayerUID, out UserData user))
			return "";
		return user.Name;
	}

	public bool IsAllNumbersPosed()
	{
		// 沒有任何玩家還有剩餘數字
		return !GameData.Instance.PlayerDatas.Values.Any(player => (player is ArrangeNumberPlayerData anPlayer && anPlayer.LeftNumbers.Count > 0));
	}
}

public class GameData
{
	public const uint GAME_VERSION = 4;
	public const uint CLIENT_GAME_SUB_VERSION = 0;

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
	public GameType CurrentGameType { get; set; }
	public int RoomID { get; set; } = 0;
	public Dictionary<ushort, UserData> UserDatas { get; set; } = new Dictionary<ushort, UserData>();
	public Dictionary<ushort, PlayerData> PlayerDatas { get; set; } = new Dictionary<ushort, PlayerData>();
	public bool IsCountingDownStart { get; set; } = false;
	public Queue<string> ChatRecord { get; set; } = new Queue<string>();
	public Queue<string> EventRecord { get; set; } = new Queue<string>();
	public GuessWordGameData GuessWordData { get; set; } = new GuessWordGameData();
	public ArrangeNumberData ArrangeNumberData { get; set; } = new ArrangeNumberData();

	private Dictionary<string, int> userNameCount = new Dictionary<string, int>();

	public void Reset()
	{
		UserDatas.Clear();
		PlayerDatas.Clear();
		ChatRecord.Clear();
		EventRecord.Clear();
		userNameCount.Clear();

		GuessWordData.Reset();
		ArrangeNumberData.Reset();

		ResetGame();
	}

	public void ResetGame()
	{
		GuessWordData.Reset();
		ArrangeNumberData.Reset();
		foreach (var player in PlayerDatas.Values)
			player.Reset();
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

	public bool IsPlayer(ushort uid = 0)
	{
		return PlayerDatas.ContainsKey(uid == 0 ? SelfUID : uid);
	}

	public bool IsCanJoinGameState()
	{
		bool canJoin = false;
		switch (CurrentGameType)
		{
			case GameType.GUESS_WORD:
				canJoin = GuessWordData.CurrentState == GuessWordState.WAITING;
				break;
			case GameType.ARRANGE_NUMBER:
				canJoin = ArrangeNumberData.CurrentState == ArrangeNumberState.WAITING;
				break;
		}
		return canJoin;
	}

	public bool IsUserNameDuplicated(string name)
	{
		if (userNameCount.ContainsKey(name))
			return userNameCount[name] > 1;
		return false;
	}
}
