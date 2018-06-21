using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CommandLineAssembly;
using System.Linq;

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

	private List<Bomb> Bombs = new List<Bomb> { };
	private List<BombCommander> BombCommanders = new List<BombCommander> { };
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
		string commandTrimmed = command.Trim().ToLowerInvariant();
		List<string> part = commandTrimmed.Split(new[] { ' ' }).ToList();

		if (commandTrimmed == "exit")
		{
			Overlay.gameObject.SetActive(!Overlay.gameObject.activeSelf);
			InputField.text = string.Empty;
		}
		else if (commandTrimmed == "clear")
		{
			List<GameObject> children = new List<GameObject>();
			foreach (Transform child in Content.transform)
			{
				children.Add(child.gameObject);
			}

			children.ForEach(child => Destroy(child));
		}
		else if (commandTrimmed == "checkactive")
		{
			Log(BombActive ? $"Bomb active: number {Bombs.Count}." : "Bomb not detected.");
			if (BombActive)
			{
				BombCommander heldBombCommander = GetHeldBomb();
				Log($"Currently held Bomb: {(heldBombCommander != null ? $"ID: {heldBombCommander.Id}" : "None")}");
			}
			Debug.Log(Bombs);
		}
		else if (part[0] == "detonate")
		{
			if (BombActive)
			{
				BombCommander heldBombCommader = GetHeldBomb();
				string reason = "Detonate Command";
				if (part.Count > 1)
					reason = command.Substring(9);
				if (heldBombCommader != null)
				{
					Log($"Detonating{(part.Count > 1 ? $" with reason {command.Substring(9)}" : "")}");
					heldBombCommader.Detonate(reason);
				} else
				{
					Log("Please hold the bomb you wish to detonate");
				}
			} else
			{
				Log("Bomb not active, cannot detonate");
			}
		}
		else if (part[0] == "causestrike")
		{
			if (BombActive)
			{
				BombCommander heldBombCommander = GetHeldBomb();
				string reason = "Strike Command";
				if (part.Count > 1)
					reason = command.Substring(12);
				if (heldBombCommander != null)
				{
					Log($"Causing strike{(part.Count > 1 ? $" with reason {command.Substring(12)}" : "")}");
					heldBombCommander.CauseStrike(reason);
				} else
				{
					Log("Please hold the bomb you wish to cause a strike on");
				}
			} else
			{
				Log("Bomb not active, cannot cause a strike");
			}
		}
		else
		{
			Log("Command not valid.");
		}
	}

	private BombCommander GetHeldBomb()
	{
		BombCommander held = null;
		foreach (BombCommander commander in BombCommanders)
		{
			if (commander.IsHeld())
				held = commander;
		}
		return held;
	}

	private void StateChange(KMGameInfo.State state)
	{
		switch (state)
		{
			case KMGameInfo.State.Gameplay:
				StartCoroutine(CheckForBomb());
				break;
			case KMGameInfo.State.Setup:
			case KMGameInfo.State.Quitting:
			case KMGameInfo.State.PostGame:
				StopCoroutine(CheckForBomb());
				Bombs.Clear();
				BombCommanders.Clear();
				BombActive = false;
				break;
		}

	}
	
	private IEnumerator CheckForBomb()
	{
		yield return new WaitUntil(() => (SceneManager.Instance.GameplayState.Bombs != null && SceneManager.Instance.GameplayState.Bombs.Count > 0));
		Debug.Log("Bomb active");
		Bombs = SceneManager.Instance.GameplayState.Bombs;
		int i = 0;
		foreach (Bomb bomb in Bombs)
		{
			BombCommanders.Add(new BombCommander(bomb, i));
			i++;
		}
		BombActive = true;
	}
	
}
