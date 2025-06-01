using System;
using System.IO;

public enum PROTOCOL_CLIENT
{
	NAME,
	JOIN,
	LEAVE,
	START,
	CANCEL_START,
	QUESTION,
	GUESS,
	VOTE,
}

public enum PROTOCOL_SERVER
{
	INIT,
	CONNECT,
	DISCONNECT,
	NAME,
	JOIN,
	LEAVE,
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
}

public class NetPacket
{
	public const int HEADER_SIZE = 5;

	public byte protocol;
	public int size;

	public byte[] data;

	public NetPacket(byte protocol, int size, byte[] data)
	{
		this.protocol = protocol;
		this.size = size;
		this.data = data;
	}

	public byte[] ToBytes()
	{
		byte[] bytes = new byte[HEADER_SIZE + size];
		bytes[0] = protocol;
		Array.Copy(BitConverter.GetBytes(size), 0, bytes, 1, sizeof(int));
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
			case PROTOCOL_SERVER.CONNECT:
				OnUserConnect(packet);
				break;
			case PROTOCOL_SERVER.DISCONNECT:
				OnUserDisconnect(packet);
				break;
			case PROTOCOL_SERVER.NAME:
				OnUserRename(packet);
				break;
			case PROTOCOL_SERVER.JOIN:
				OnPlayerJoin(packet);
				break;
			case PROTOCOL_SERVER.LEAVE:
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
				if (result == 1)
					player.GuessHistory.Add($"<color=#00ba00>✔</color> 是{guess}");
				else
					player.GuessHistory.Add($"<color=#ba0000>✘</color> 不是{guess}");
			}
			player.SuccessRound = reader.ReadUInt16();
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
	private void OnUserConnect(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		ushort uid = reader.ReadUInt16();
		GameData.Instance.UserDatas[uid] = new UserData { UID = uid };
	}
	private void OnUserDisconnect(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		ushort uid = reader.ReadUInt16();
		if (GameData.Instance.UserDatas.ContainsKey(uid))
			GameData.Instance.UserDatas.Remove(uid);
	}
	private void OnUserRename(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		ushort uid = reader.ReadUInt16();
		string name = reader.ReadString();
		if (GameData.Instance.UserDatas.ContainsKey(uid))
			GameData.Instance.UserDatas[uid].Name = name;
	}
	private void OnPlayerJoin(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		ushort uid = reader.ReadUInt16();
		GameData.Instance.PlayerDatas[uid] = new PlayerData { UID = uid };

		// 更新介面
		if (GamePage.Instance != null)
			GamePage.Instance.UpdatePlayerInfo();
	}
	private void OnPlayerLeave(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		ushort uid = reader.ReadUInt16();
		if (GameData.Instance.PlayerDatas.ContainsKey(uid))
			GameData.Instance.PlayerDatas.Remove(uid);
		if (GameData.Instance.PlayerOrder.Contains(uid))
			GameData.Instance.PlayerOrder.Remove(uid);

		// 更新介面
		if (GamePage.Instance != null)
			GamePage.Instance.UpdatePlayerInfo();
	}
	private void OnStartCountdown(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		GameData.Instance.IsCountingDownStart = reader.ReadByte() == 1;
		if (GameData.Instance.IsCountingDownStart)
		{
			byte countdownTime = reader.ReadByte();
			// TODO: 播放倒數並把開始按鈕文字改成取消開始，同時加上事件訊息
		}
		else
		{
			// TODO:
			// 關閉倒數並把開始按鈕文字改回去，同時加上事件訊息
		}
	}
	private void OnGameStart(NetPacket packet)
	{
		GameData.Instance.ResetGame();
		GameData.Instance.CurrentState = GameState.PREPARING;
	}
	private void OnGameStateChanged(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		GameData.Instance.CurrentState = (GameState)reader.ReadByte();
	}
	private void OnUpdatePlayerOrder(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		GameData.Instance.GuessingPlayerIndex = reader.ReadByte();
		bool needUpdateOrder = reader.ReadByte() == 1;
		if (!needUpdateOrder)
			return;

		GameData.Instance.PlayerOrder.Clear();
		byte orderCount = reader.ReadByte();
		for (int i = 0; i < orderCount; i++)
		{
			ushort uid = reader.ReadUInt16();
			GameData.Instance.PlayerOrder.Add(uid);
		}

		// 更新介面
		if (GamePage.Instance != null)
			GamePage.Instance.UpdatePlayerInfo();
	}
	private void OnQuestionAssigned(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		ushort uid = reader.ReadUInt16();
		if (uid == GameData.Instance.SelfUID)
		{
			// TODO: 提示自身題目已經出好了
		}
		else
		{
			string question = reader.ReadString();
			if (GameData.Instance.PlayerDatas.ContainsKey(uid))
			{
				PlayerData player = GameData.Instance.PlayerDatas[uid];
				player.Question = question;
			}
		}

		// 更新介面
		if (GamePage.Instance != null)
			GamePage.Instance.UpdatePlayerInfo();
	}
	private void OnPlayerSuccess(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		ushort uid = reader.ReadUInt16();
		ushort successRound = reader.ReadUInt16();
		if (GameData.Instance.PlayerDatas.ContainsKey(uid))
		{
			PlayerData player = GameData.Instance.PlayerDatas[uid];
			player.SuccessRound = successRound;
		}

		// 更新介面
		if (GamePage.Instance != null)
			GamePage.Instance.UpdateData();
	}
	private void OnPlayerGuessed(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		GameData.Instance.VotingGuess = reader.ReadString();
		GameData.Instance.Votes.Clear();
		GameData.Instance.CurrentState = GameState.VOTING;

		// 更新介面
		if (GamePage.Instance != null)
			GamePage.Instance.UpdateData();
	}
	private void OnPlayerVoted(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		ushort uid = reader.ReadUInt16();
		byte vote = reader.ReadByte();
		GameData.Instance.Votes[uid] = vote;

		// 更新介面
		if (GamePage.Instance != null)
			GamePage.Instance.UpdatePlayerInfo();
	}
	private void OnGuessAgainRequired(NetPacket packet)
	{
		// TODO: 新增事件紀錄
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
			player.GuessHistory.Add(new Tuple<string, byte>(guess, result));
		}
	}
	private void OnGameEnd(NetPacket packet)
	{
		ByteReader reader = new ByteReader(packet.data);
		bool isForceEnd = reader.ReadByte() == 1;
		
		GameData.Instance.CurrentState = GameState.WAITING;
		// TODO: 顯示結算排名
	}

	// =========================================================

	public void SendName(byte[] encodedName)
	{
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.NAME, encodedName.Length, encodedName);
		SendPacket(packet);
	}
	public void SendJoin()
	{
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.JOIN, 0, new byte[0]);
		SendPacket(packet);
	}
	public void SendLeave()
	{
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.LEAVE, 0, new byte[0]);
		SendPacket(packet);
	}
	public void SendStart()
	{
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.START, 0, new byte[0]);
		SendPacket(packet);
	}
	public void SendCancelStart()
	{
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.CANCEL_START, 0, new byte[0]);
		SendPacket(packet);
	}
	public void SendAssignQuestion(byte[] encodedQuestion)
	{
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.QUESTION, encodedQuestion.Length, encodedQuestion);
		SendPacket(packet);
	}
	public void SendGuess(byte[] encodedGuess)
	{
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.GUESS, encodedGuess.Length, encodedGuess);
		SendPacket(packet);
	}
	public void SendVote(byte vote)
	{
		byte[] data = new byte[1] { vote };
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.VOTE, 1, data);
		SendPacket(packet);
	}
}
