using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CommandLine : MonoBehaviour {

	#region Global Variables
	public Text TextPrefab = null;
	public InputField InputField = null;
	public Canvas Overlay = null;
	public GameObject Content = null;
	public ScrollRect ScrollRect = null;
	public KMGameInfo GameInfo = null;
	private bool _enabled = false;
	private bool _wasAtBottom = true;
	private bool BombActive = false;

	private List<Bomb> Bombs = null;
	#endregion

	private void OnEnable()
	{ 
		_enabled = true;
		Debug.Log("enabled");
		GameInfo = GetComponent<KMGameInfo>();
		GameInfo.OnStateChange += delegate (KMGameInfo.State state)
		{
			StateChange(state);
		};
	}

	private void Start()
	{
		Overlay = GetComponentInChildren<Canvas>();
		Overlay.gameObject.SetActive(false);
		TextPrefab = GetComponentInChildren<Text>();
		ScrollRect = Overlay.GetComponentInChildren<ScrollRect>(true);
		Content = ScrollRect.GetComponentInChildren<Image>(true).GetComponentInChildren<VerticalLayoutGroup>(true).gameObject;
		InputField = Overlay.GetComponentInChildren<InputField>(true);
	}

	private void OnDisable()
	{
		_enabled = false;
		if (Overlay.gameObject.activeSelf)
			Overlay.gameObject.SetActive(false);
		Debug.Log("disabled");
		GameInfo.OnStateChange -= delegate (KMGameInfo.State state)
		{
			StateChange(state);
		};
		StopAllCoroutines();
	}

	private void Update() {
		if (_enabled)
		{
			if (Input.GetKeyDown(KeyCode.Backslash))
			{
				Overlay.gameObject.SetActive(!Overlay.gameObject.activeSelf);
				Debug.Log("activate/deactivate");
				if (Overlay.gameObject.activeSelf)
				{
					InputField.ActivateInputField();
					InputField.text = string.Empty;
				}
			}

			if (Input.GetKeyDown(KeyCode.Return) && Overlay.gameObject.activeSelf && InputField.text != string.Empty)
			{
				Log(">" + InputField.text);
				ProcessCommand(InputField.text);
				InputField.text = string.Empty;
				Debug.Log("clear");
				InputField.ActivateInputField();
			}
		}
		_wasAtBottom = ScrollRect.verticalNormalizedPosition <= 0.001f;
	}

	private void Log(string text)
	{
		Text newText = Instantiate(TextPrefab);
		newText.text = text;
		newText.transform.SetParent(Content.transform, false);

		if (_wasAtBottom)
		{
			Canvas.ForceUpdateCanvases();
			ScrollRect.verticalNormalizedPosition = 0.0f;
			Canvas.ForceUpdateCanvases();
		}
	}

	private void ProcessCommand(string command)
	{
		command = command.Trim().ToLowerInvariant();

		if (command == "exit")
		{
			Overlay.gameObject.SetActive(!Overlay.gameObject.activeSelf);
			InputField.text = string.Empty;
		}
		else if (command == "clear")
		{
			List<GameObject> children = new List<GameObject>();
			foreach (Transform child in Content.transform)
			{
				children.Add(child.gameObject);
			}

			children.ForEach(child => Destroy(child));
		}
		else if (command == "checkactive")
		{
			Log(BombActive ? $"Bomb active: number {Bombs.Count}." : "Bomb not detected.");
			Debug.Log(Bombs);
		}
		else
		{
			Log("Command not valid.");
		} 
	}

	private void StateChange(KMGameInfo.State state)
	{
		switch (state)
		{
			case KMGameInfo.State.Gameplay:
				StartCoroutine(CheckForBomb());
				break;
			case KMGameInfo.State.PostGame:
				StopCoroutine(CheckForBomb());
				Bombs = null;
				BombActive = false;
				break;
		}

	}
	
	private IEnumerator CheckForBomb()
	{
		yield return new WaitUntil(() => (SceneManager.Instance.GameplayState.Bombs != null && SceneManager.Instance.GameplayState.Bombs.Count > 0));
		Debug.Log("Bomb active");
		Bombs = SceneManager.Instance.GameplayState.Bombs;
		BombActive = true;
	}
	
}
