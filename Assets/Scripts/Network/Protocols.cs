using System;
using System.IO;
using System.Linq;
using UnityEngine;

public enum PROTOCOL_CLIENT
{
	NAME,
	JOIN_GAME,
	LEAVE_GAME,
	START,
	CANCEL_START,
	QUESTION,
	GUESS,
	VOTE,
	CHAT,
	GIVE_UP,
	VERSION,
	CREATE_ROOM,
	JOIN_ROOM,
	LEAVE_ROOM,
	SET_MAX_NUMBER,
	SET_NUMBER_GROUP_COUNT,
	SET_NUMBER_PER_PLAYER,
	POSE_NUMBER,
	SET_URGENT,
}

public enum PROTOCOL_SERVER
{
	INIT,
	CONNECT,
	DISCONNECT,
	NAME,
	JOIN_GAME,
	LEAVE_GAME,
	START_COUNTDOWN,
	START,
	GAMESTATE,
	PLAYER_ORDER,
	QUESTION,
	SUCCESS,
	GUESS,
	VOTE,
	GUESS_AGAIN,
	GUESS_RECORD,
	END,
	CHAT,
	SKIP_GUESS,
	VERSION,
	ROOM_ID,
	SETTINGS,
	UID,
	PLAYER_NUMBERS,
	POSE_NUMBER,
	URGENT_PLAYER,
}

public class NetPacket
{
	public const int HEADER_SIZE = 1;

	public byte protocol;
	public byte[] data;

	public NetPacket(byte protocol, byte[] data)
	{
		this.protocol = protocol;
		this.data = data;
	}

	public byte[] ToBytes()
	{
		byte[] bytes = new byte[HEADER_SIZE + data.Length];
		bytes[0] = protocol;
		Array.Copy(data, 0, bytes, HEADER_SIZE, data.Length);
		return bytes;
	}
}

public class ByteReader
{
	private byte[] data;
	private int offset;
	public ByteReader(byte[] data)
	{
		this.data = data;
		this.offset = 0;
	}

	public byte ReadByte()
	{
		if (offset >= data.Length)
			return 0;
		return data[offset++];
	}
	public short ReadInt16()
	{
		if (offset + sizeof(short) > data.Length)
			return 0;
		short value = BitConverter.ToInt16(data, offset);
		offset += sizeof(short);
		return value;
	}
	public ushort ReadUInt16()
	{
		if (offset + sizeof(ushort) > data.Length)
			return 0;
		ushort value = BitConverter.ToUInt16(data, offset);
		offset += sizeof(ushort);
		return value;
	}
	public int ReadInt32()
	{
		if (offset + sizeof(int) > data.Length)
			return 0;
		int value = BitConverter.ToInt32(data, offset);
		offset += sizeof(int);
		return value;
	}
	public uint ReadUInt32()
	{
		if (offset + sizeof(uint) > data.Length)
			return 0;
		uint value = BitConverter.ToUInt32(data, offset);
		offset += sizeof(uint);
		return value;
	}
	public string ReadString()
	{
		byte length = ReadByte();
		if (length <= 0 || offset + length > data.Length)
			return "";
		string value = System.Text.Encoding.UTF8.GetString(data, offset, length);
		offset += length;
		return value;
	}
}
public class ByteWriter
{
	private MemoryStream stream;
	private BinaryWriter bw;
	public ByteWriter()
	{
		stream = new MemoryStream();
		bw = new BinaryWriter(stream);
	}

	public void WriteByte(byte value) { bw.Write(value); }
	public void WriteInt16(short value) { bw.Write(value); }
	public void WriteUInt16(ushort value) { bw.Write(value); }
	public void WriteInt32(int value) { bw.Write(value); }
	public void WriteUInt32(uint value) { bw.Write(value); }
	public void WriteBytes(byte[] value) { bw.Write(value); }
	public void WriteString(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			bw.Write((byte)0);
			return;
		}
		byte[] bytes = System.Text.Encoding.UTF8.GetBytes(value);
		bw.Write((byte)bytes.Length);
		bw.Write(bytes);
	}

	public byte[] GetBytes() { return stream.ToArray(); }
}

public partial class NetManager
{
	private void ProcessReceive(NetPacket packet)
	{
		switch ((PROTOCOL_SERVER)packet.protocol)
		{
			case PROTOCOL_SERVER.UID:
				OnGetUID(packet);
				break;
			case PROTOCOL_SERVER.VERSION:
				OnVersionCheckResult(packet);
				break;
			case PROTOCOL_SERVER.INIT:
				OnInitData(packet);
				break;
			case PROTOCOL_SERVER.NAME:
				OnUserRename(packet);
				break;
			case PROTOCOL_SERVER.ROOM_ID:
				OnEnterRoomID(packet);
				break;
			case PROTOCOL_SERVER.CONNECT:
				OnUserConnect(packet);
				break;
			case PROTOCOL_SERVER.DISCONNECT:
				OnUserDisconnect(packet);
				break;
			case PROTOCOL_SERVER.JOIN_GAME:
				OnPlayerJoin(packet);
				break;
			case PROTOCOL_SERVER.LEAVE_GAME:
				OnPlayerLeave(packet);
				break;
			case PROTOCOL_SERVER.START_COUNTDOWN:
				OnStartCountdown(packet);
				break;
			case PROTOCOL_SERVER.START:
				OnGameStart(packet);
				break;
			case PROTOCOL_SERVER.GAMESTATE:
				OnGameStateChanged(packet);
				break;
			case PROTOCOL_SERVER.PLAYER_ORDER:
				OnUpdatePlayerOrder(packet);
				break;
			case PROTOCOL_SERVER.QUESTION:
				OnQuestionAssigned(packet);
				break;
			case PROTOCOL_SERVER.SUCCESS:
				OnPlayerSuccess(packet);
				break;
			case PROTOCOL_SERVER.GUESS:
				OnPlayerGuessed(packet);
				break;
			case PROTOCOL_SERVER.VOTE:
				OnPlayerVoted(packet);
				break;
			case PROTOCOL_SERVER.GUESS_AGAIN:
				OnGuessAgainRequired(packet);
				break;
			case PROTOCOL_SERVER.GUESS_RECORD:
				OnGuessRecordAdded(packet);
				break;
			case PROTOCOL_SERVER.SKIP_GUESS:
				OnSkipGuess(packet);
				break;
			case PROTOCOL_SERVER.END:
				OnGameEnd(packet);
				break;
			case PROTOCOL_SERVER.CHAT:
				OnChatMessage(packet);
				break;
			case PROTOCOL_SERVER.SETTINGS:
				OnGetSettings(packet);
				break;
			case PROTOCOL_SERVER.PLAYER_NUMBERS:
				OnGetPlayerNumbers(packet);
				break;
			case PROTOCOL_SERVER.POSE_NUMBER:
				OnPoseNumber(packet);
				break;
			case PROTOCOL_SERVER.URGENT_PLAYER:
				OnUrgentPlayer(packet);
				break;
		}
	}

	private void InitGuessWordData(ByteReader reader)
	{
		// 讀使用者資料
		byte userCount = reader.ReadByte();
		for (int i = 0; i < userCount; i++)
		{
			ushort uid = reader.ReadUInt16();
			string name = reader.ReadString();
			GameData.Instance.UserDatas[uid] = new UserData { UID = uid, Name = name };
		}
		// 讀玩家資料
		byte playerCount = reader.ReadByte();
		for (int i = 0; i < playerCount; i++)
		{
			ushort uid = reader.ReadUInt16();
			GuessWordPlayerData player = new GuessWordPlayerData { UID = uid };
			player.Question = reader.ReadString();
			byte historyCount = reader.ReadByte();
			for (int j = 0; j < historyCount; j++)
			{
				string guess = reader.ReadString();
				byte result = reader.ReadByte();
				player.AddGuessRecord(guess, result);
			}
			player.SuccessRound = reader.ReadInt16();
			GameData.Instance.PlayerDatas[uid] = player;
		}
		// 遊戲階段
		GameData.Instance.GuessWordData.CurrentState = (GuessWordState)reader.ReadByte();
		// 玩家順序
		byte orderCount = reader.ReadByte();
		for (int i = 0; i < orderCount; i++)
		{
			ushort uid = reader.ReadUInt16();
			GameData.Instance.GuessWordData.PlayerOrder.Add(uid);
		}
		GameData.Instance.GuessWordData.GuessingPlayerIndex = reader.ReadByte();
		// 投票狀況
		GameData.Instance.GuessWordData.VotingGuess = reader.ReadString();
		byte voteCount = reader.ReadByte();
		for (int i = 0; i < voteCount; i++)
		{
			ushort uid = reader.ReadUInt16();
			byte vote = reader.ReadByte();
			GameData.Instance.GuessWordData.Votes[uid] = vote;
		}
	}

	private void InitArrangeNumberData(ByteReader reader)
	{
		// 讀使用者資料
		byte userCount = reader.ReadByte();
		for (int i = 0; i < userCount; i++)
		{
			ushort uid = reader.ReadUInt16();
			string name = reader.ReadString();
			GameData.Instance.UserDatas[uid] = new UserData { UID = uid, Name = name };
		}
		// 讀玩家資料
		byte playerCount = reader.ReadByte();
		for (int i = 0; i < playerCount; i++)
		{
			ushort uid = reader.ReadUInt16();
			ArrangeNumberPlayerData player = new ArrangeNumberPlayerData { UID = uid };
			byte numberCount = reader.ReadByte();
			for (int j = 0; j < numberCount; j++)
			{
				ushort number = reader.ReadUInt16();
				player.LeftNumbers.Add(number);
			}
			player.IsUrgent = reader.ReadByte() == 1;
			GameData.Instance.PlayerDatas[uid] = player;
		}
		// 遊戲設定
		GameData.Instance.ArrangeNumberData.MaxNumber = reader.ReadUInt16();
		GameData.Instance.ArrangeNumberData.NumberGroupCount = reader.ReadByte();
		GameData.Instance.ArrangeNumberData.NumberPerPlayer = reader.ReadByte();
		// 遊戲階段
		GameData.Instance.ArrangeNumberData.CurrentState = (ArrangeNumberState)reader.ReadByte();
		// 當前數字
		GameData.Instance.ArrangeNumberData.LastPlayerUID = reader.ReadUInt16();
		GameData.Instance.ArrangeNumberData.CurrentNumber = reader.ReadUInt16();
	}

	private void OnGetUID(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		GameData.Instance.SelfUID = reader.ReadUInt16();
	}

	private void OnInitData(NetPacket packet)
	{
		GameData.Instance.Reset();

		ByteReader reader = new ByteReader(packet.data);
		GameData.Instance.CurrentGameType = (GameType)reader.ReadByte();

		switch (GameData.Instance.CurrentGameType)
		{
			case GameType.GUESS_WORD:
				InitGuessWordData(reader);
				break;
			case GameType.ARRANGE_NUMBER:
				InitArrangeNumberData(reader);
				break;
			default:
				Debug.LogError("OnInitData: 未知的遊戲類型");
				return;
		}

		// 更新介面
		if (GamePage.Instance != null)
			GamePage.Instance.UpdateData();
	}

	private void OnVersionCheckResult(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		uint serverVersion = reader.ReadUInt32();
		if (ConnectPage.Instance != null)
			ConnectPage.Instance.OnVersionCheckResult(serverVersion);
	}
	private void OnUserConnect(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);

		var user = new UserData();
		user.UID = reader.ReadUInt16();
		user.Name = reader.ReadString();
		GameData.Instance.UserDatas[user.UID] = user;

		if (GamePage.Instance != null)
			GamePage.Instance.UpdateData();
	}
	private void OnUserDisconnect(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		ushort uid = reader.ReadUInt16();
		if (GameData.Instance.UserDatas.TryGetValue(uid, out UserData user))
		{
			GameData.Instance.RemoveUserName(user.GetOriginalName());
			GameData.Instance.UserDatas.Remove(uid);
		}

		// 更新使用者名稱
		if (GamePage.Instance != null)
			GamePage.Instance.UpdateData();
	}
	private void OnUserRename(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		ushort uid = reader.ReadUInt16();
		string name = reader.ReadString();
		if (GameData.Instance.UserDatas.ContainsKey(uid))
			GameData.Instance.UserDatas[uid].Name = name;

		if (GamePage.Instance != null)
			GamePage.Instance.UpdateData();
	}
	private void OnPlayerJoin(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		ushort uid = reader.ReadUInt16();
		GameData.Instance.PlayerDatas[uid] = new GuessWordPlayerData { UID = uid };

		GameData.Instance.AddEventRecord($"<color=yellow>{GameData.Instance.UserDatas[uid].Name}</color> 加入了遊戲");

		// 更新介面
		if (GamePage.Instance != null)
		{
			GamePage.Instance.UpdatePlayerInfo();
			GamePage.Instance.PlaySound("pop_up");
		}
	}
	private void OnPlayerLeave(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		ushort uid = reader.ReadUInt16();
		if (GameData.Instance.PlayerDatas.ContainsKey(uid))
			GameData.Instance.PlayerDatas.Remove(uid);
		if (GameData.Instance.GuessWordData.PlayerOrder.Contains(uid))
			GameData.Instance.GuessWordData.PlayerOrder.Remove(uid);

		GameData.Instance.AddEventRecord($"<color=yellow>{GameData.Instance.UserDatas[uid].Name}</color> 離開了遊戲");

		// 更新介面
		if (GamePage.Instance != null)
		{
			GamePage.Instance.UpdateData();
			GamePage.Instance.PlaySound("pop_off");
		}
	}
	private void OnStartCountdown(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		GameData.Instance.IsCountingDownStart = reader.ReadByte() == 1;
		if (GameData.Instance.IsCountingDownStart)
		{
			byte countdownTime = reader.ReadByte();
			if (GamePage.Instance != null)
				GamePage.Instance.StartCountdown(countdownTime);
			GameData.Instance.AddEventRecord($"遊戲將於 {countdownTime} 秒後開始");
		}
		else
		{
			if (GamePage.Instance != null)
				GamePage.Instance.StopCountdown();
			GameData.Instance.AddEventRecord("已取消遊戲開始倒數");
		}

		// 更新介面
		if (GamePage.Instance != null)
			GamePage.Instance.UpdateStartButton();
	}
	private void OnGameStart(NetPacket packet)
	{
		GameData.Instance.ResetGame();
		switch (GameData.Instance.CurrentGameType)
		{
			case GameType.GUESS_WORD:
				GameData.Instance.GuessWordData.CurrentState = GuessWordState.PREPARING;
				break;
			case GameType.ARRANGE_NUMBER:
				GameData.Instance.ArrangeNumberData.CurrentState = ArrangeNumberState.PLAYING;
				break;
			default:
				Debug.LogError("OnGameStart: 未知的遊戲類型");
				return;
		}

		GameData.Instance.AddEventRecord("遊戲開始");

		// 更新介面
		if (GamePage.Instance != null)
		{
			GamePage.Instance.StopCountdown();
			GamePage.Instance.OnStartGame();
			GamePage.Instance.UpdateData();

			GamePage.Instance.PlaySound("ding");
		}
	}
	private void OnGameStateChanged(NetPacket packet)
	{
		GuessWordState originState = GameData.Instance.GuessWordData.CurrentState;

		ByteReader reader = new ByteReader(packet.data);
		GameData.Instance.GuessWordData.CurrentState = (GuessWordState)reader.ReadByte();

		if (GameData.Instance.GuessWordData.CurrentState == GuessWordState.GUESSING)
		{
			ushort guessingPlayerUID = GameData.Instance.GuessWordData.GetCurrentPlayerUID();
			if (GameData.Instance.UserDatas.ContainsKey(guessingPlayerUID))
				GameData.Instance.AddEventRecord($"輪到 <color=yellow>{GameData.Instance.UserDatas[guessingPlayerUID].Name}</color> 進行猜題");
			else
				Debug.LogError($"猜題者 UID 為 {guessingPlayerUID} 的玩家不存在");
		}

		// 更新介面
		if (GuessWordGamePage.Instance != null)
		{
			GuessWordGamePage.Instance.UpdateData();
			if (GameData.Instance.GuessWordData.CurrentState == GuessWordState.GUESSING && GameData.Instance.GuessWordData.GetCurrentPlayerUID() == GameData.Instance.SelfUID)
				GuessWordGamePage.Instance.StartIdleCheck();
			if (originState == GuessWordState.PREPARING && GameData.Instance.GuessWordData.CurrentState == GuessWordState.GUESSING)
				GuessWordGamePage.Instance.PlaySound("ding");
		}
	}
	private void OnUpdatePlayerOrder(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		GameData.Instance.GuessWordData.GuessingPlayerIndex = reader.ReadByte();
		bool needUpdateOrder = reader.ReadByte() == 1;
		if (needUpdateOrder)
		{
			GameData.Instance.GuessWordData.PlayerOrder.Clear();
			byte orderCount = reader.ReadByte();
			for (int i = 0; i < orderCount; i++)
			{
				ushort uid = reader.ReadUInt16();
				GameData.Instance.GuessWordData.PlayerOrder.Add(uid);
			}
		}

		// 更新介面
		if (GuessWordGamePage.Instance != null)
		{
			GuessWordGamePage.Instance.UpdatePlayerInfo();
			// 猜題玩家更換時強制改顯示該玩家的歷史紀錄
			if (GameData.Instance.GuessWordData.GuessingPlayerIndex < GameData.Instance.GuessWordData.PlayerOrder.Count)
			{
				ushort currentPlayerUID = GameData.Instance.GuessWordData.GetCurrentPlayerUID();
				GuessWordGamePage.Instance.ShowPlayerHistoryRecord(currentPlayerUID);
			}
		}
	}
	private void OnQuestionAssigned(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		ushort uid = reader.ReadUInt16();
		bool isLocked = reader.ReadByte() == 1;
		if (uid == GameData.Instance.SelfUID)
		{
			// 提示自身題目已經出好了
			if (GameData.Instance.PlayerDatas.TryGetValue(uid, out PlayerData player))
			{
				GuessWordPlayerData gwPlayer = player as GuessWordPlayerData;

				string colorFormat = isLocked ? "yellow" : "blue";
				gwPlayer.Question = $"<color={colorFormat}>答案已屏蔽</color>";
				gwPlayer.QuestionLocked = isLocked;
			}
		}
		else
		{
			string question = reader.ReadString();
			if (GameData.Instance.PlayerDatas.TryGetValue(uid, out PlayerData player))
			{
				GuessWordPlayerData gwPlayer = player as GuessWordPlayerData;

				gwPlayer.Question = question;
				gwPlayer.QuestionLocked = isLocked;
			}
		}

		// 更新介面
		if (GuessWordGamePage.Instance != null)
		{
			GuessWordGamePage.Instance.UpdatePlayerInfo();
			if (GameData.Instance.UserDatas.TryGetValue(uid, out UserData user))
			{
				if (isLocked)
					GuessWordGamePage.Instance.ShowPopupMessage($"<color=yellow>{user.Name}</color> 的題目已指派");
				else
					GuessWordGamePage.Instance.ShowPopupMessage($"<color=#00aa00>{user.Name}</color> 的題目已宣告");
			}
		}
	}
	private void OnPlayerSuccess(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		ushort uid = reader.ReadUInt16();
		short successRound = reader.ReadInt16();
		string answer = reader.ReadString();
		if (GameData.Instance.PlayerDatas.ContainsKey(uid))
		{
			GuessWordPlayerData player = GameData.Instance.PlayerDatas[uid] as GuessWordPlayerData;
			player.SuccessRound = successRound;
			player.Question = $"<color=yellow>{answer}</color>"; // 更新玩家的名詞為答案
		}

		string successMessage = "";
		if (GameData.Instance.UserDatas.TryGetValue(uid, out UserData user))
		{
			successMessage = successRound > 0 ? $"<color=yellow>{user.Name}</color> 猜出了他的名詞" : $"<color=yellow>{user.Name}</color> 放棄了";
			GameData.Instance.AddEventRecord(successMessage);
		}

		// 更新介面
		if (GuessWordGamePage.Instance != null)
		{
			GuessWordGamePage.Instance.UpdateData();
			GuessWordGamePage.Instance.ShowPopupMessage(successMessage);

			bool isEnding = true;
			foreach (GuessWordPlayerData player in GameData.Instance.PlayerDatas.Values)
			{
				if (player.SuccessRound == 0)
				{
					isEnding = false;
					break;
				}
			}
			// 要結束遊戲時不播特效
			if (!isEnding)
			{
				if (successRound > 0)
				{
					GuessWordGamePage.Instance.PlaySound("boom");
					int randomImage = UnityEngine.Random.Range(1, 4);
					GuessWordGamePage.Instance.ShowPopupImage($"brain{randomImage}");
				}
				else
				{
					GuessWordGamePage.Instance.PlaySound("bye");
				}
			}
		}
	}
	private void OnPlayerGuessed(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		GameData.Instance.GuessWordData.VotingGuess = reader.ReadString();
		GameData.Instance.GuessWordData.Votes.Clear();
		GameData.Instance.GuessWordData.CurrentState = GuessWordState.VOTING;

		ushort guessingPlayerUID = GameData.Instance.GuessWordData.GetCurrentPlayerUID();
		if (GameData.Instance.UserDatas.ContainsKey(guessingPlayerUID))
			GameData.Instance.AddEventRecord($"<color=yellow>{GameData.Instance.UserDatas[guessingPlayerUID].Name}</color> 提問他的名詞是否為 <color=blue>{GameData.Instance.GuessWordData.VotingGuess}</color>");

		// 更新介面
		if (GuessWordGamePage.Instance != null)
		{
			GuessWordGamePage.Instance.UpdateData();
			GuessWordGamePage.Instance.PlaySound("drum");
			if (guessingPlayerUID != GameData.Instance.SelfUID)
				GuessWordGamePage.Instance.StartIdleCheck();
		}
	}
	private void OnPlayerVoted(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		ushort uid = reader.ReadUInt16();
		byte vote = reader.ReadByte();
		GameData.Instance.GuessWordData.Votes[uid] = vote;

		string voteText = vote switch
		{
			1 => "<color=#00ba00>同意</color>",
			2 => "<color=#ba0000>否定</color>",
			0 => "<color=#baba00>棄權</color>",
			_ => ""
		};
		if (GameData.Instance.UserDatas.ContainsKey(uid))
			GameData.Instance.AddEventRecord($"<color=yellow>{GameData.Instance.UserDatas[uid].Name}</color> 表示 {voteText}");

		// 更新介面
		if (GuessWordGamePage.Instance != null)
			GuessWordGamePage.Instance.UpdatePlayerInfo();
	}
	private void OnGuessAgainRequired(NetPacket packet)
	{
		GameData.Instance.AddEventRecord("沒有人表示意見，要求重新提出猜測");
		if (GuessWordGamePage.Instance != null)
			GuessWordGamePage.Instance.PlaySound("huh");
	}
	private void OnGuessRecordAdded(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		ushort uid = reader.ReadUInt16();
		string guess = reader.ReadString();
		byte result = reader.ReadByte();
		if (GameData.Instance.PlayerDatas.ContainsKey(uid))
		{
			GuessWordPlayerData player = GameData.Instance.PlayerDatas[uid] as GuessWordPlayerData;
			player.AddGuessRecord(guess, result);
		}

		string messageText = null;
		if (GameData.Instance.UserDatas.ContainsKey(uid))
		{
			string resultText = result == 1 ? "<color=#00ba00>是</color>" : "<color=#ba0000>不是</color>";
			messageText = $"投票結果：<color=yellow>{GameData.Instance.UserDatas[uid].Name}</color> 的名詞 {resultText} <color=blue>{guess}</color>";
			GameData.Instance.AddEventRecord(messageText);
		}

		// 更新介面
		if (GuessWordGamePage.Instance != null)
		{
			if (uid == GameData.Instance.SelfUID)
				GuessWordGamePage.Instance.UpdateSelfGuessRecord(true);
			GuessWordGamePage.Instance.UpdateCurrentPlayerGuessRecord(true);

			if (messageText != null)
				GuessWordGamePage.Instance.ShowPopupMessage(messageText);

			if (result == 1)
				GuessWordGamePage.Instance.PlaySound("true");
			else
				GuessWordGamePage.Instance.PlaySound("bruh");
		}
	}
	private void OnGameEnd(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		bool isForceEnd = reader.ReadByte() == 1;
		switch (GameData.Instance.CurrentGameType)
		{
			case GameType.GUESS_WORD:
				GameData.Instance.GuessWordData.CurrentState = GuessWordState.WAITING;
				break;
			case GameType.ARRANGE_NUMBER:
				GameData.Instance.ArrangeNumberData.CurrentState = ArrangeNumberState.WAITING;
				break;
			default:
				Debug.LogError("OnGameEnd: Unknown game type.");
				return;
		}

		GameData.Instance.AddEventRecord(isForceEnd ? "遊戲已被中斷" : "遊戲結束");

		// 更新介面
		if (GamePage.Instance != null)
		{
			GamePage.Instance.UpdateData();
			if (!isForceEnd)
				GamePage.Instance.ShowGameResult();
		}
	}

	private void OnChatMessage(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		ushort uid = reader.ReadUInt16();
		string message = reader.ReadString();
		byte isHidden = reader.ReadByte();

		if (GameData.Instance.UserDatas.TryGetValue(uid, out UserData user))
		{
			string fullMessage = $"[<color=yellow>{user.Name}</color>] {message}";
			GameData.Instance.AddChatRecord(fullMessage, isHidden == 1);
			if (GuessWordGamePage.Instance != null)
				GuessWordGamePage.Instance.ShowPopupMessage(fullMessage);
		}
	}

	private void OnSkipGuess(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		ushort uid = reader.ReadUInt16();
		if (GameData.Instance.UserDatas.TryGetValue(uid, out UserData user))
		{
			string message = $"<color=yellow>{user.Name}</color> 跳過猜題";
			GameData.Instance.AddEventRecord(message);
			if (GuessWordGamePage.Instance != null)
				GuessWordGamePage.Instance.ShowPopupMessage(message);
		}
	}

	private void OnEnterRoomID(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		GameData.Instance.RoomID = reader.ReadInt32();

		if (RoomPage.Instance != null)
			RoomPage.Instance.OnEnterRoom();
	}

	private void OnGetSettings(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		GameData.Instance.ArrangeNumberData.MaxNumber = reader.ReadUInt16();
		GameData.Instance.ArrangeNumberData.NumberGroupCount = reader.ReadByte();
		GameData.Instance.ArrangeNumberData.NumberPerPlayer = reader.ReadByte();

		if (ArrangeNumberGamePage.Instance != null)
			ArrangeNumberGamePage.Instance.UpdateSettings();
	}

	private void OnGetPlayerNumbers(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		bool isUpdateAll = reader.ReadByte() == 1;
		if (isUpdateAll)
		{
			byte playerCount = reader.ReadByte();
			for (byte i = 0; i < playerCount; i++)
			{
				ushort uid = reader.ReadUInt16();
				if (!GameData.Instance.PlayerDatas.TryGetValue(uid, out PlayerData player))
					continue;
				if (player is not ArrangeNumberPlayerData)
					continue;

				ArrangeNumberPlayerData arrangeNumberPlayer = player as ArrangeNumberPlayerData;
				arrangeNumberPlayer.LeftNumbers.Clear();
				byte numberCount = reader.ReadByte();
				for (int j = 0; j < numberCount; j++)
					arrangeNumberPlayer.LeftNumbers.Add(reader.ReadUInt16());
			}
		}
		else
		{
			if (!GameData.Instance.PlayerDatas.TryGetValue(GameData.Instance.SelfUID, out PlayerData player))
				return;
			if (player is not ArrangeNumberPlayerData)
				return;
			ArrangeNumberPlayerData arrangeNumberPlayer = player as ArrangeNumberPlayerData;

			byte numberCount = reader.ReadByte();
			for (byte i = 0; i < numberCount; i++)
				arrangeNumberPlayer.LeftNumbers.Add(reader.ReadUInt16());

			// 幫其他玩家設指定數量的未知手牌
			foreach (var p in GameData.Instance.PlayerDatas.Values)
			{
				if (p is not ArrangeNumberPlayerData)
					continue;
				if (p.UID == GameData.Instance.SelfUID)
					continue;

				(p as ArrangeNumberPlayerData).LeftNumbers = Enumerable.Repeat<ushort>(0, numberCount).ToList();
			}
		}
	}

	private void OnPoseNumber(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		ushort uid = reader.ReadUInt16();
		ushort number = reader.ReadUInt16();
		if (GameData.Instance.PlayerDatas.TryGetValue(uid, out PlayerData player))
		{
			if (player is ArrangeNumberPlayerData anPlayer)
				anPlayer.LeftNumbers.RemoveAt(anPlayer.LeftNumbers.Count - 1);
		}

		GameData.Instance.ArrangeNumberData.LastPlayerUID = uid;
		GameData.Instance.ArrangeNumberData.CurrentNumber = number;

		GameData.Instance.AddEventRecord($"<color=yellow>{GameData.Instance.UserDatas[uid].Name}</color> 出了數字 <color=blue>{number}</color>");

		if (ArrangeNumberGamePage.Instance != null)
			ArrangeNumberGamePage.Instance.UpdateData();
	}

	private void OnUrgentPlayer(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		ushort uid = reader.ReadUInt16();
		bool isUrgent = reader.ReadByte() == 1;

		if (GameData.Instance.PlayerDatas.TryGetValue(uid, out PlayerData player))
		{
			if (player is ArrangeNumberPlayerData anPlayer)
				anPlayer.IsUrgent = isUrgent;
		}

		string statusText = isUrgent ? "<color=red>急了</color>" : "<color=green>不急</color>";
		GameData.Instance.AddEventRecord($"<color=yellow>{GameData.Instance.UserDatas[uid].Name}</color> 表示 {statusText}");

		if (ArrangeNumberGamePage.Instance != null)
		{
			ArrangeNumberGamePage.Instance.UpdateData();
			ArrangeNumberGamePage.Instance.ShowPopupImage(isUrgent ? "urgent" : "not_urgent");
			ArrangeNumberGamePage.Instance.PlaySound(isUrgent ? "alert" : "quack");
		}
	}

	// =========================================================

	private void SendVersionCheck()
	{
		ByteWriter writer = new ByteWriter();
		writer.WriteUInt32(GameData.GAME_VERSION);

		byte[] data = writer.GetBytes();
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.VERSION, data);
		SendPacket(packet);
	}
	public void SendName(byte[] encodedName)
	{
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.NAME, encodedName);
		SendPacket(packet);
	}
	public void SendCreateRoom(GameType gameType)
	{
		ByteWriter writer = new ByteWriter();
		writer.WriteByte((byte)gameType);

		byte[] data = writer.GetBytes();
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.CREATE_ROOM, data);
		SendPacket(packet);
	}
	public void SendJoinRoom(uint roomID)
	{
		ByteWriter writer = new ByteWriter();
		writer.WriteUInt32(roomID);

		byte[] data = writer.GetBytes();
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.JOIN_ROOM, data);
		SendPacket(packet);
	}
	public void SendLeaveRoom()
	{
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.LEAVE_ROOM, new byte[0]);
		SendPacket(packet);
	}
	public void SendJoinGame()
	{
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.JOIN_GAME, new byte[0]);
		SendPacket(packet);
	}
	public void SendLeaveGame()
	{
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.LEAVE_GAME, new byte[0]);
		SendPacket(packet);
	}
	public void SendStart()
	{
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.START, new byte[0]);
		SendPacket(packet);
	}
	public void SendCancelStart()
	{
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.CANCEL_START, new byte[0]);
		SendPacket(packet);
	}
	public void SendAssignQuestion(byte[] encodedQuestion, bool isLocked)
	{
		ByteWriter writer = new ByteWriter();
		writer.WriteByte((byte)(isLocked ? 1 : 0));
		writer.WriteBytes(encodedQuestion);

		byte[] data = writer.GetBytes();
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.QUESTION, data);
		SendPacket(packet);
	}
	public void SendGuess(byte[] encodedGuess)
	{
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.GUESS, encodedGuess);
		SendPacket(packet);
	}
	public void SendVote(byte vote)
	{
		byte[] data = new byte[1] { vote };
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.VOTE, data);
		SendPacket(packet);
	}
	public void SendChatMessage(byte[] encodedMessage, ushort hideUID)
	{
		ByteWriter writer = new ByteWriter();
		writer.WriteUInt16(hideUID);
		writer.WriteBytes(encodedMessage);

		byte[] data = writer.GetBytes();
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.CHAT, data);
		SendPacket(packet);
	}
	public void SendGiveUp()
	{
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.GIVE_UP, new byte[0]);
		SendPacket(packet);
	}
	public void SendSetMaxNumber(ushort number)
	{
		ByteWriter writer = new ByteWriter();
		writer.WriteUInt16(number);

		byte[] data = writer.GetBytes();
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.SET_MAX_NUMBER, data);
		SendPacket(packet);
	}
	public void SendSetNumberGroupCount(byte count)
	{
		ByteWriter writer = new ByteWriter();
		writer.WriteByte(count);

		byte[] data = writer.GetBytes();
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.SET_MAX_NUMBER, data);
		SendPacket(packet);
	}
	public void SendSetNumberPerPlayer(byte count)
	{
		ByteWriter writer = new ByteWriter();
		writer.WriteByte(count);

		byte[] data = writer.GetBytes();
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.SET_MAX_NUMBER, data);
		SendPacket(packet);
	}
	public void SendPoseNumber()
	{
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.POSE_NUMBER, new byte[0]);
		SendPacket(packet);
	}

	public void SendSetUrgent(bool isUrgent)
	{
		ByteWriter writer = new ByteWriter();
		writer.WriteByte((byte)(isUrgent ? 1 : 0));

		byte[] data = writer.GetBytes();
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.SET_URGENT, data);
		SendPacket(packet);
	}
}
