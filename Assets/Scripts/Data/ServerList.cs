using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ServerList", menuName = "Scriptable Objects/ServerList")]
public class ServerList : ScriptableObject
{
	[Serializable]
	public class ServerData
	{
		public string name;
		public string ip;
		public int port;
	}

	public List<ServerData> servers = new List<ServerData>();
}
