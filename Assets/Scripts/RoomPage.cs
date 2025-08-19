using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoomPage : MonoBehaviour
{
	public static RoomPage Instance { get; private set; }

	[SerializeField]
	private Text serverNameText;
	[SerializeField]
	private InputField RoomID_Input;
	[SerializeField]
	private PopupMessage messagePopup;

	void Awake()
	{
		Instance = this;
	}

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
		serverNameText.text = GameData.Instance.ServerName;
    }

	void OnDestroy()
	{
		Instance = null;
	}

	public void OnEnterRoom()
	{
		if (GameData.Instance.RoomID < 0)
		{
			switch (GameData.Instance.RoomID)
			{
				case -1:
					messagePopup.ShowMessage("目前使用者人數過多，請稍後再試");
					break;
				case -2:
					messagePopup.ShowMessage("該房號不存在，請確認輸入後再試");
					break;
				default:
					messagePopup.ShowMessage("未知錯誤");
					break;
			}
			return;
		}

		switch (GameData.Instance.GameType)
		{
			case GameType.GUESS_WORD:
				SceneManager.LoadScene("GuessWordScene");
				break;
			case GameType.ARRANGE_NUMBER:
				SceneManager.LoadScene("ArrangeNumberScene");
				break;
			default:
				break;
		}
	}

	public void ClickCreateRoom()
	{
		NetManager.Instance.SendCreateRoom();
	}

	public void ClickJoinRoom()
	{
		if (!uint.TryParse(RoomID_Input.text, out uint roomID))
		{
			messagePopup.ShowMessage("請輸入正確格式的房號");
			return;
		}

		NetManager.Instance.SendJoinRoom(roomID);
	}

	public async void ClickBackToLogin()
	{
		await NetManager.Instance.Disconnect();
	}
}
