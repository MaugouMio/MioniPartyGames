using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public abstract class GamePage : MonoBehaviour
{
	public static GamePage Instance { get; private set; }

	[SerializeField]
	private List<PlayerInfo> PlayerList;
	[SerializeField]
	private List<UserInfo> UserList;
	[SerializeField]
	private Button JoinButton;
	[SerializeField]
	private Text JoinButtonText;

	[SerializeField]
	private Slider VolumeSlider;
	[SerializeField]
	private Text VolumeText;

	[SerializeField]
	private TextList EventList;
	[SerializeField]
	private TextList ChatList;
	[SerializeField]
	private InputField ChatInput;
	[SerializeField]
	private Button StartButton;
	[SerializeField]
	private Text StartButtonText;

	[SerializeField]
	private Text StartCountdownText;
	[SerializeField]
	private PopupImage ImagePopup;
	[SerializeField]
	private PopupMessage MessagePopup;

	[SerializeField]
	private AudioSource SFXPlayer;

	private bool needUpdate = true;
	private IEnumerator countdownCoroutine = null;

	protected virtual void Awake()
	{
		VolumeSlider.value = PlayerPrefs.GetFloat("SoundVolume", 0.5f);
		VolumeText.text = ((int)(VolumeSlider.value * 100)).ToString();

		if (Instance != null)
		{
			Debug.LogError($"Trying to instatiate a new GamePage object {name} while another GamePage object {Instance.name} existing.");
			Destroy(Instance);
		}
		Instance = this;
	}

    // Update is called once per frame
    protected virtual void Update()
    {
		if (needUpdate)
			UpdateDataReal();
	}

	protected virtual void OnDestroy()
	{
		PlayerPrefs.SetFloat("SoundVolume", VolumeSlider.value);
		Instance = null;
	}

	protected virtual void UpdateDataReal()
	{
		UpdatePlayerInfo();
		UpdateUserInfo();
		UpdateChatList();
		UpdateEventList();
		UpdateStartButton();

		needUpdate = false;
	}

	public void UpdateData()
	{
		needUpdate = true;
	}

	protected virtual IEnumerable<PlayerData> GetDisplayPlayerList()
	{
		return GameData.Instance.PlayerDatas.Values;
	}

	public void UpdatePlayerInfo()
	{
		int idx = 0;
		foreach (var player in GetDisplayPlayerList())
		{
			PlayerInfo obj;
			if (idx >= PlayerList.Count)
			{
				obj = Instantiate(PlayerList[0]);
				obj.transform.SetParent(PlayerList[0].transform.parent, false);
				PlayerList.Add(obj);
			}
			else
			{
				obj = PlayerList[idx];
			}

			obj.gameObject.SetActive(true);
			obj.UpdateData(player);
			idx++;
		}
		// 關閉未使用的物件
		while (idx < PlayerList.Count)
			PlayerList[idx++].gameObject.SetActive(false);

		bool isJoined = GameData.Instance.IsPlayer();
		JoinButton.interactable = isJoined || GameData.Instance.IsCanJoinGameState();
		JoinButtonText.text = isJoined ? "離開遊戲" : "加入遊戲";
	}

	public void UpdateUserInfo()
	{
		int idx = 0;
		foreach (var user in GameData.Instance.UserDatas.Values)
		{
			// 還沒取名字的忽略不顯示
			if (user.Name == "")
				continue;

			UserInfo obj;
			if (idx >= UserList.Count)
			{
				obj = Instantiate(UserList[0]);
				obj.transform.SetParent(UserList[0].transform.parent, false);
				UserList.Add(obj);
			}
			else
			{
				obj = UserList[idx];
			}
			obj.gameObject.SetActive(true);
			obj.UpdateData(user);
			idx++;
		}
		// 關閉未使用的物件
		while (idx < UserList.Count)
			UserList[idx++].gameObject.SetActive(false);
	}

	public void UpdateChatList(bool isNewMessage = false)
	{
		ChatList.UpdateData(GameData.Instance.ChatRecord);
		if (isNewMessage)
			ChatList.MoveToLast();
	}

	public void UpdateEventList(bool isNewRecord = false)
	{
		EventList.UpdateData(GameData.Instance.EventRecord);
		if (isNewRecord)
			EventList.MoveToLast();
	}

	public void UpdateStartButton()
	{
		// 可以加入遊戲的階段也代表可以開始遊戲
		if (GameData.Instance.IsCanJoinGameState())
		{
			StartButton.interactable = true;
			StartButtonText.text = GameData.Instance.IsCountingDownStart ? "取消開始" : "開始遊戲";
		}
		else
		{
			StartButtonText.text = "遊戲中";
			StartButton.interactable = false;
		}
	}

	private IEnumerator Countdown(int seconds)
	{
		while (seconds > 0)
		{
			StartCountdownText.text = $"遊戲開始倒數 {seconds} 秒";
			PlaySound("clock");
			yield return new WaitForSeconds(1f);
			seconds--;
		}
		StartCountdownText.text = "";
		countdownCoroutine = null;
	}

	public virtual void StartCountdown(int seconds)
	{
		countdownCoroutine = Countdown(seconds);
		StartCoroutine(countdownCoroutine);
	}

	public void StopCountdown()
	{
		if (countdownCoroutine != null)
		{
			StopCoroutine(countdownCoroutine);
			StartCountdownText.text = "";
			countdownCoroutine = null;
		}
	}

	public virtual void OnStartGame() {}

	public void ShowPopupImage(string filename)
	{
		if (ImagePopup != null)
			ImagePopup.ShowImage(filename);
		else
			Debug.LogWarning("PopupImage is not assigned.");
	}

	public void ShowPopupMessage(string message)
	{
		if (MessagePopup != null)
			MessagePopup.ShowMessage(message);
		else
			Debug.LogWarning("PopupMessage is not assigned.");
	}

	public void PlaySound(string name)
	{
		if (SFXPlayer != null)
		{
			AudioClip clip = Resources.Load<AudioClip>($"Sounds/{name}");
			if (clip != null)
				SFXPlayer.PlayOneShot(clip);
			else
				Debug.LogWarning($"Audio clip '{name}' not found.");
		}
		else
		{
			Debug.LogWarning("SFXPlayer is not assigned.");
		}
	}

	public void ClickCopyRoomID()
	{
		ShowPopupMessage("已將房號複製到剪貼簿");
#if !UNITY_WEBGL || UNITY_EDITOR
		UniClipboard.SetText(GameData.Instance.RoomID.ToString());
#else
		WebGLCopyAndPaste.WebGLCopyAndPasteAPI.CopyToClipboard(GameData.Instance.RoomID.ToString());
#endif
	}

	public void ClickJoinGame()
	{
		if (GameData.Instance.IsPlayer())
			NetManager.Instance.SendLeaveGame();
		else
			NetManager.Instance.SendJoinGame();
	}

	public void ClickLeaveRoom()
	{
		NetManager.Instance.SendLeaveRoom();
		SceneManager.LoadScene("RoomScene");
	}

	public void ClickStartGame()
	{
		if (GameData.Instance.IsCountingDownStart)
			NetManager.Instance.SendCancelStart();
		else if (!GameData.Instance.IsPlayer())
			ShowPopupMessage("須先加入遊戲才能進行操作");
		else if (GameData.Instance.PlayerDatas.Count < 2)
			ShowPopupMessage("至少需要兩名玩家才能開始遊戲");
		else
			NetManager.Instance.SendStart();
	}

	protected abstract ushort GetHideChatUID();

	public void ClickSendChat()
	{
		string processedMessage = ChatInput.text.Trim();
		if (processedMessage == "")
			return;

		byte[] encodedMessage = System.Text.Encoding.UTF8.GetBytes(processedMessage);
		if (encodedMessage.Length > 255)
		{
			ShowPopupMessage("訊息內容過長");
			return;
		}

		NetManager.Instance.SendChatMessage(encodedMessage, GetHideChatUID());
		ChatInput.text = "";
		ChatInput.ActivateInputField();
	}

	public void OnVolumeChanged()
	{
		if (SFXPlayer != null)
		{
			SFXPlayer.volume = VolumeSlider.value;
			VolumeText.text = ((int)(VolumeSlider.value * 100)).ToString();
		}
		else
		{
			Debug.LogWarning("SFXPlayer is not assigned.");
		}
	}
}
