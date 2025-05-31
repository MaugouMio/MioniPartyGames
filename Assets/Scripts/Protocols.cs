using UnityEngine;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
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

	public byte[] GetBytes() { return stream.ToArray(); }
}

public partial class NetManager
{
	private void ProcessReceive(NetPacket packet)
	{
		switch ((PROTOCOL_SERVER)packet.protocol)
		{
		case PROTOCOL_SERVER.INIT:
			break;
		case PROTOCOL_SERVER.CONNECT:
			break;
		case PROTOCOL_SERVER.DISCONNECT:
			break;
		case PROTOCOL_SERVER.NAME:
			break;
		case PROTOCOL_SERVER.JOIN:
			break;
		case PROTOCOL_SERVER.LEAVE:
			break;
		case PROTOCOL_SERVER.START_COUNTDOWN:
			break;
		case PROTOCOL_SERVER.START:
			break;
		case PROTOCOL_SERVER.GAMESTATE:
			break;
		case PROTOCOL_SERVER.PLAYER_ORDER:
			break;
		case PROTOCOL_SERVER.QUESTION:
			break;
		case PROTOCOL_SERVER.SUCCESS:
			break;
		case PROTOCOL_SERVER.GUESS:
			break;
		case PROTOCOL_SERVER.VOTE:
			break;
		case PROTOCOL_SERVER.GUESS_AGAIN:
			break;
		case PROTOCOL_SERVER.GUESS_RECORD:
			break;
		case PROTOCOL_SERVER.END:
			break;
		}
	}

	public void SendName(byte[] encodedName)
	{
		ByteWriter byteWriter = new ByteWriter();
		byteWriter.WriteBytes(encodedName);

		byte[] data = byteWriter.GetBytes();
		NetPacket packet = new NetPacket((byte)PROTOCOL_CLIENT.NAME, data.Length, data);
	}
}
