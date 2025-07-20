using System;
using System.IO;
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
			case PROTOCOL_SERVER.END:
				OnGameEnd(packet);
				break;
			case PROTOCOL_SERVER.CHAT:
				OnChatMessage(packet);
				break;
			case PROTOCOL_SERVER.SKIP_GUESS:
				OnSkipGuess(packet);
				break;
			case PROTOCOL_SERVER.VERSION:
				OnVersionCheckResult(packet);
				break;
		}
	}

	private void OnInitData(NetPacket packet)
	{
		GameData.Instance.Reset();

		ByteReader reader = new ByteReader(packet.data);
		// 使用者自身的 UID
		GameData.Instance.SelfUID = reader.ReadUInt16();
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
			PlayerData player = new PlayerData { UID = uid };
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
		GameData.Instance.CurrentState = (GameState)reader.ReadByte();
		// 玩家順序
		byte orderCount = reader.ReadByte();
		for (int i = 0; i < orderCount; i++)
		{
			ushort uid = reader.ReadUInt16();
			GameData.Instance.PlayerOrder.Add(uid);
		}
		GameData.Instance.GuessingPlayerIndex = reader.ReadByte();
		// 投票狀況
		GameData.Instance.VotingGuess = reader.ReadString();
		byte voteCount = reader.ReadByte();
		for (int i = 0; i < voteCount; i++)
		{
			ushort uid = reader.ReadUInt16();
			byte vote = reader.ReadByte();
			GameData.Instance.Votes[uid] = vote;
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
			GamePage.Instance.UpdateUserInfo();
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
		{
			GamePage.Instance.UpdatePlayerInfo();
			GamePage.Instance.UpdateUserInfo();
		}
	}
	private void OnUserRename(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		ushort uid = reader.ReadUInt16();
		string name = reader.ReadString();
		if (GameData.Instance.UserDatas.ContainsKey(uid))
			GameData.Instance.UserDatas[uid].Name = name;

		if (GamePage.Instance != null)
		{
			GamePage.Instance.UpdatePlayerInfo();
			GamePage.Instance.UpdateUserInfo();
		}
	}
	private void OnPlayerJoin(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		ushort uid = reader.ReadUInt16();
		GameData.Instance.PlayerDatas[uid] = new PlayerData { UID = uid };

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
		if (GameData.Instance.PlayerOrder.Contains(uid))
			GameData.Instance.PlayerOrder.Remove(uid);

		GameData.Instance.AddEventRecord($"<color=yellow>{GameData.Instance.UserDatas[uid].Name}</color> 離開了遊戲");

		// 更新介面
		if (GamePage.Instance != null)
		{
			if (GameData.Instance.CurrentState == GameState.WAITING)
				GamePage.Instance.UpdatePlayerInfo();
			else
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
		GameData.Instance.CurrentState = GameState.PREPARING;

		GameData.Instance.AddEventRecord("遊戲開始");

		// 更新介面
		if (GamePage.Instance != null)
		{
			GamePage.Instance.StopCountdown();
			GamePage.Instance.ResetTempLeaveToggle();
			GamePage.Instance.UpdateData();

			GamePage.Instance.PlaySound("ding");
		}
	}
	private void OnGameStateChanged(NetPacket packet)
	{
		GameState originState = GameData.Instance.CurrentState;

		ByteReader reader = new ByteReader(packet.data);
		GameData.Instance.CurrentState = (GameState)reader.ReadByte();

		if (GameData.Instance.CurrentState == GameState.GUESSING)
		{
			ushort guessingPlayerUID = GameData.Instance.GetCurrentPlayerUID();
			if (GameData.Instance.UserDatas.ContainsKey(guessingPlayerUID))
				GameData.Instance.AddEventRecord($"輪到 <color=yellow>{GameData.Instance.UserDatas[guessingPlayerUID].Name}</color> 進行猜題");
			else
				Debug.LogError($"猜題者 UID 為 {guessingPlayerUID} 的玩家不存在");
		}

		// 更新介面
		if (GamePage.Instance != null)
		{
			GamePage.Instance.UpdateData();
			if (GameData.Instance.CurrentState == GameState.GUESSING && GameData.Instance.GetCurrentPlayerUID() == GameData.Instance.SelfUID)
				GamePage.Instance.StartIdleCheck();
			if (originState == GameState.PREPARING && GameData.Instance.CurrentState == GameState.GUESSING)
				GamePage.Instance.PlaySound("ding");
		}
	}
	private void OnUpdatePlayerOrder(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		GameData.Instance.GuessingPlayerIndex = reader.ReadByte();
		bool needUpdateOrder = reader.ReadByte() == 1;
		if (needUpdateOrder)
		{
			GameData.Instance.PlayerOrder.Clear();
			byte orderCount = reader.ReadByte();
			for (int i = 0; i < orderCount; i++)
			{
				ushort uid = reader.ReadUInt16();
				GameData.Instance.PlayerOrder.Add(uid);
			}
		}

		// 更新介面
		if (GamePage.Instance != null)
		{
			GamePage.Instance.UpdatePlayerInfo();
			// 猜題玩家更換時強制改顯示該玩家的歷史紀錄
			if (GameData.Instance.GuessingPlayerIndex < GameData.Instance.PlayerOrder.Count)
			{
				ushort currentPlayerUID = GameData.Instance.GetCurrentPlayerUID();
				GamePage.Instance.ShowPlayerHistoryRecord(currentPlayerUID);
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
				string colorFormat = isLocked ? "yellow" : "blue";
				player.Question = $"<color={colorFormat}>答案已屏蔽</color>";
				player.QuestionLocked = isLocked;
			}
		}
		else
		{
			string question = reader.ReadString();
			if (GameData.Instance.PlayerDatas.TryGetValue(uid, out PlayerData player))
			{
				player.Question = question;
				player.QuestionLocked = isLocked;
			}
		}

		// 更新介面
		if (GamePage.Instance != null)
		{
			GamePage.Instance.UpdatePlayerInfo();
			if (GameData.Instance.UserDatas.TryGetValue(uid, out UserData user))
			{
				if (isLocked)
					GamePage.Instance.ShowPopupMessage($"<color=yellow>{user.Name}</color> 的題目已指派");
				else
					GamePage.Instance.ShowPopupMessage($"<color=#00aa00>{user.Name}</color> 的題目已宣告");
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
			PlayerData player = GameData.Instance.PlayerDatas[uid];
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
		if (GamePage.Instance != null)
		{
			GamePage.Instance.UpdateData();
			GamePage.Instance.ShowPopupMessage(successMessage);

			bool isEnding = true;
			foreach (PlayerData player in GameData.Instance.PlayerDatas.Values)
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
					GamePage.Instance.PlaySound("boom");
					int randomImage = UnityEngine.Random.Range(1, 4);
					GamePage.Instance.ShowPopupImage($"brain{randomImage}");
				}
				else
				{
					GamePage.Instance.PlaySound("bye");
				}
			}
		}
	}
	private void OnPlayerGuessed(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		GameData.Instance.VotingGuess = reader.ReadString();
		GameData.Instance.Votes.Clear();
		GameData.Instance.CurrentState = GameState.VOTING;

		ushort guessingPlayerUID = GameData.Instance.GetCurrentPlayerUID();
		if (GameData.Instance.UserDatas.ContainsKey(guessingPlayerUID))
			GameData.Instance.AddEventRecord($"<color=yellow>{GameData.Instance.UserDatas[guessingPlayerUID].Name}</color> 提問他的名詞是否為 <color=blue>{GameData.Instance.VotingGuess}</color>");

		// 更新介面
		if (GamePage.Instance != null)
		{
			GamePage.Instance.UpdateData();
			GamePage.Instance.PlaySound("drum");
			if (guessingPlayerUID != GameData.Instance.SelfUID)
				GamePage.Instance.StartIdleCheck();
		}
	}
	private void OnPlayerVoted(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		ushort uid = reader.ReadUInt16();
		byte vote = reader.ReadByte();
		GameData.Instance.Votes[uid] = vote;

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
		if (GamePage.Instance != null)
			GamePage.Instance.UpdatePlayerInfo();
	}
	private void OnGuessAgainRequired(NetPacket packet)
	{
		GameData.Instance.AddEventRecord("沒有人表示意見，要求重新提出猜測");
		if (GamePage.Instance != null)
			GamePage.Instance.PlaySound("huh");
	}
	private void OnGuessRecordAdded(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		ushort uid = reader.ReadUInt16();
		string guess = reader.ReadString();
		byte result = reader.ReadByte();
		if (GameData.Instance.PlayerDatas.ContainsKey(uid))
		{
			PlayerData player = GameData.Instance.PlayerDatas[uid];
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
		if (GamePage.Instance != null)
		{
			if (uid == GameData.Instance.SelfUID)
				GamePage.Instance.UpdateSelfGuessRecord(true);
			GamePage.Instance.UpdateCurrentPlayerGuessRecord(true);

			if (messageText != null)
				GamePage.Instance.ShowPopupMessage(messageText);

			if (result == 1)
				GamePage.Instance.PlaySound("true");
			else
				GamePage.Instance.PlaySound("bruh");
		}
	}
	private void OnGameEnd(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		bool isForceEnd = reader.ReadByte() == 1;
		GameData.Instance.CurrentState = GameState.WAITING;

		GameData.Instance.AddEventRecord(isForceEnd ? "遊戲已被中斷" : "遊戲結束");

		// 更新介面
		if (GamePage.Instance != null)
		{
			GamePage.Instance.UpdateData();
			if (!isForceEnd)
			{
				GamePage.Instance.ShowGameResult();
				GamePage.Instance.PlaySound("end");
			}
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
			if (GamePage.Instance != null)
				GamePage.Instance.ShowPopupMessage(fullMessage);
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
			if (GamePage.Instance != null)
				GamePage.Instance.ShowPopupMessage(message);
		}
	}

	private void OnEnterRoomID(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		GameData.Instance.RoomID = reader.ReadInt32();

		if (RoomPage.Instance != null)
			RoomPage.Instance.OnEnterRoom();
	}

	// =========================================================

	public void SendName(byte[] encodedName)
	{
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.NAME, encodedName);
		SendPacket(packet);
	}
	public void SendCreateRoom()
	{
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.CREATE_ROOM, new byte[0]);
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

	public void SendChatMessage(byte[] encodedMessage, bool isHidden)
	{
		ByteWriter writer = new ByteWriter();
		writer.WriteByte((byte)(isHidden ? 1 : 0));
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

	private void SendVersionCheck()
	{
		ByteWriter writer = new ByteWriter();
		writer.WriteUInt32(GameData.GAME_VERSION);

		byte[] data = writer.GetBytes();
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.VERSION, data);
		SendPacket(packet);
	}
}
