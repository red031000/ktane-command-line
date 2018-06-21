using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CommandLineAssembly;
using System.Linq;
using System;
using Assets.Scripts.Records;
using Assets.Scripts.Stats;
using Assets.Scripts.Missions;

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
#if DEBUG
	private readonly bool _isDebug = true;
#else
	private readonly bool _isDebug = false;
#endif

	private List<Bomb> Bombs = new List<Bomb> { };
	private List<BombCommander> BombCommanders = new List<BombCommander> { };
	private List<Module> Modules = new List<Module> { };
#endregion

	private void OnEnable()
	{ 
		_enabled = true;
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
		else if (commandTrimmed == "checkactive" && _isDebug)
		{
			Log(BombActive ? $"Bomb active: number {Bombs.Count}." : "Bomb not detected.");
			if (BombActive)
			{
				BombCommander heldBombCommander = GetHeldBomb();
				Log($"Currently held Bomb: {(heldBombCommander != null ? $"ID: {heldBombCommander.Id}" : "None")}");
				Module focusedModule = GetFocusedModule();
				Log($"Currently focused Module: {(focusedModule != null ? $"Name: {focusedModule.ModuleName}" : "None")}");
			}
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
					Debug.Log("[Command Line] Detonating bomb.");
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
					Debug.Log("[Command Line] Causing strike.");
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
		else if (part[0].EqualsAny("time", "t") && part[1].EqualsAny("add", "increase", "change", "subtract", "decrease", "remove", "set"))
		{
			if (BombActive)
			{
				bool negative = part[1].EqualsAny("subtract", "decrease", "remove");
				bool direct = part[1].EqualsAny("set");
				BombCommander heldBombCommander = GetHeldBomb();
				if (heldBombCommander != null)
				{
					float time = 0;
					float originalTime = heldBombCommander.TimerComponent.TimeRemaining;
					Dictionary<string, float> timeLengths = new Dictionary<string, float>()
						{
							{ "ms", 0.001f },
							{ "s", 1 },
							{ "m", 60 },
							{ "h", 3600 },
							{ "d", 86400 },
							{ "w", 604800 },
							{ "y", 31536000 },
						};
					foreach (string split in part.Skip(2))
					{
						bool valid = false;
						foreach (string unit in timeLengths.Keys)
						{
							if (!split.EndsWith(unit) || !float.TryParse(split.Substring(0, split.Length - unit.Length), out float length)) continue;
							time += length * timeLengths[unit];
							valid = true;
							break;
						}

						if (valid)
						{
							time = (float)Math.Round((decimal)time, 2, MidpointRounding.AwayFromZero);
							if (!direct && Math.Abs(time) == 0) break;
							if (negative) time = -time;

							if (direct)
								heldBombCommander.TimerComponent.TimeRemaining = time;
							else
								heldBombCommander.TimerComponent.TimeRemaining = heldBombCommander.CurrentTimer + time;

							if (originalTime < heldBombCommander.TimerComponent.TimeRemaining)
							{
								ChangeLeaderboard(true);
								Debug.Log("[Command Line] Disabling leaderboard.");
							}

							if (direct)
							{
								Log($"Setting the timer to {Math.Abs(time < 0 ? 0 : time).FormatTime()}");
								Debug.Log("[Command Line] Set bomb time.");
							}
							else
							{
								Log($"{(time > 0 ? "Added" : "Subtracted")} {Math.Abs(time).FormatTime()} {(time > 0 ? "to" : "from")} the timer");
								Debug.Log("[Command Line] Changed bomb time.");
							}
							break;
						} else
						{
							Log("Time not valid");
							break;
						}
					}
				} else
				{
					Log("Please hold the bomb you wish to change the time on");
				}
			} else
			{
				Log("Bomb not active, cannot change time");
			}
		}
		else if (part[0].EqualsAny("strikes", "strike", "s") && part[1].EqualsAny("add", "increase", "change", "subtract", "decrease", "remove", "set"))
		{
			if (BombActive)
			{
				BombCommander heldBombCommander = GetHeldBomb();
				if (heldBombCommander != null)
				{
					bool negative = part[1].EqualsAny("subtract", "decrease", "remove");
					bool direct = part[1].EqualsAny("set");
					if (int.TryParse(part[2], out int strikes) && (strikes != 0 || direct))
					{
						int originalStrikes = heldBombCommander.StrikeCount;
						if (negative) strikes = -strikes;

						if (direct && strikes < 0)
						{
							strikes = 0;
						}
						else if (!direct && (heldBombCommander.StrikeCount + strikes) < 0)
						{
							strikes = -heldBombCommander.StrikeCount;
						}

						if (direct)
							heldBombCommander.StrikeCount = strikes;
						else
							heldBombCommander.StrikeCount += strikes;

						if (heldBombCommander.StrikeCount < originalStrikes)
						{
							ChangeLeaderboard(true);
							Debug.Log("[Command Line] Disabling leaderboard.");
						}

						if (direct)
						{
							Log($"Setting the strike count to {Math.Abs(strikes)} {(Math.Abs(strikes) != 1 ? "strikes" : "strike")}");
							Debug.Log("[Command Line] Set bomb strike count.");
						}
						else
						{
							Log($"{(strikes > 0 ? "Added" : "Subtracted")} {Math.Abs(strikes)} {(Math.Abs(strikes) != 1 ? "strikes" : "strike")} {(strikes > 0 ? "to" : "from")} the bomb");
							Debug.Log("[Command Line] Changed bomb strike count.");
						}
					}
				} else
				{
					Log("Please hold the bomb you wish to change the strikes on");
				}
			} else
			{
				Log("Bomb not active, cannot change strikes");
			}
		}
		else if (part[0].EqualsAny("ms", "maxstrikes", "sl", "strikelimit") && part[1].EqualsAny("add", "increase", "change", "subtract", "decrease", "remove", "set"))
		{
			if (BombActive)
			{
				BombCommander heldBombCommander = GetHeldBomb();
				if (heldBombCommander != null)
				{
					bool negative = part[1].EqualsAny("subtract", "decrease", "remove");
					bool direct = part[1].EqualsAny("set");
					if (int.TryParse(part[2], out int maxStrikes) && (maxStrikes != 0 || direct))
					{
						int originalStrikeLimit = heldBombCommander.StrikeLimit;
						if (negative) maxStrikes = -maxStrikes;

						if (direct && maxStrikes < 0)
							maxStrikes = 0;
						else if (!direct && (heldBombCommander.StrikeLimit + maxStrikes) < 0)
							maxStrikes = -heldBombCommander.StrikeLimit;

						if (direct)
							heldBombCommander.StrikeLimit = maxStrikes;
						else
							heldBombCommander.StrikeLimit += maxStrikes;

						if (originalStrikeLimit < heldBombCommander.StrikeLimit)
						{
							ChangeLeaderboard(true);
							Debug.Log("[Command Line] Disabling leaderboard.");
						}

						if (direct)
						{
							Log($"Setting the strike limit to {Math.Abs(maxStrikes)} {(Math.Abs(maxStrikes) != 1 ? "strikes" : "strike")}");
							Debug.Log("[Command Line] Set bomb strike limit.");
						}
						else
						{
							Log($"{(maxStrikes > 0 ? "Added" : "Subtracted")} {Math.Abs(maxStrikes)} {(Math.Abs(maxStrikes) > 1 ? "strikes" : "strike")} {(maxStrikes > 0 ? "to" : "from")} the strike limit");
							Debug.Log("[Command Line] Changed bomb strike limit.");
						}
					}
				} else
				{
					Log("Please hold the bomb you wish to change the strike limit on");
				}
			} else
			{
				Log("Bomb not active, cannot change strike limit");
			} 
		}
		else if (commandTrimmed == "solve")
		{
			if (BombActive)
			{
				BombCommander heldBombCommander = GetHeldBomb();
				if (heldBombCommander != null)
				{
					Module module = GetFocusedModule();
					if (module != null)
					{
						switch (module.ComponentType)
						{
							case ComponentTypeEnum.NeedyCapacitor:
							case ComponentTypeEnum.NeedyKnob:
							case ComponentTypeEnum.NeedyMod:
							case ComponentTypeEnum.NeedyVentGas:
								Log("You cannot solve a needy module");
								break;

							case ComponentTypeEnum.Empty:
								Log("You cannot solve an empty module slot!");
								break;
							case ComponentTypeEnum.Timer:
								Log("You cannot solve the timer!");
								break;

							default:
								if (module.IsKeyModule)
								{
									Log("Solving \"Key\" modules is not supported yet");
									break;
								}
								ChangeLeaderboard(true);
								Debug.Log("[Command Line] Disabling leaderboard.");
								try
								{
									KMBombModule KMmodule = module.BombComponent.GetComponent<KMBombModule>();
									CommonReflectedTypeInfo.HandlePassMethod.Invoke(module.BombComponent, null);
									foreach (MonoBehaviour behavior in module.BombComponent.GetComponentsInChildren<MonoBehaviour>(true))
									{
										behavior.StopAllCoroutines();
									}
								}
								catch (Exception ex)
								{
									Log($"Exception while force solving module: {ex}");
								}
								break;
						}
					} else
					{
						Log("Please focus on the module that you wish to solve");
					}
				} else
				{
					Log("Please hold the bomb that contains the module you wish to solve");
				}
			} else
			{
				Log("Bomb not active, cannot solve a module");
			}
		}
		else if (commandTrimmed == "help")
		{
			Log("Command reference:");
			Log("\"Detonate [reason]\" - detonate the currently held bomb, with an optional reason");
			Log("\"CauseStrike [reason]\" - cause a strike on the currently held bomb, with an optional reason");
			Log("\"Time (set|add|subtract) (time)\" - changes the time on the currently held bomb (NOTE: this will disable leaderboards if you use it to achieve a faster time)");
			Log("\"Strikes (set|add|subtract) (number)\" - changes the strikes on the currently held bomb (NOTE: this will disable leaderboards if you use it to achieve a faster time)");
			Log("\"StrikeLimit (set|add|subtract) (number)\" - changes the strike limit on the currently held bomb (NOTE: this will disable leaderboards if you add a higher strike limit)");
			Log("\"Solve\" - solves the currently focused module (NOTE: this will disable leaderboards)");
		}
		else
		{
			Log("Command not valid");
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
	
	private Module GetFocusedModule()
	{
		Module focused = null;
		foreach (Module module in Modules)
		{
			if (module.IsHeld())
				focused = module;
		}
		return focused;
	}

	private static void ChangeLeaderboard(bool off)
	{
		if (RecordManager.Instance != null)
			RecordManager.Instance.DisableBestRecords = off;

		if (StatsManager.Instance != null)
			StatsManager.Instance.DisableStatChanges = off;
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
		Bombs = SceneManager.Instance.GameplayState.Bombs;
		int i = 0;
		string[] keyModules =
		{
			"SouvenirModule", "MemoryV2", "TurnTheKey", "TurnTheKeyAdvanced", "theSwan", "HexiEvilFMN", "taxReturns"
		};
		foreach (Bomb bomb in Bombs)
		{
			BombCommanders.Add(new BombCommander(bomb, i));
			foreach (BombComponent bombComponent in bomb.BombComponents)
			{
				ComponentTypeEnum componentType = bombComponent.ComponentType;
				bool keyModule = false;
				string moduleName = "";

				switch (componentType)
				{
					case ComponentTypeEnum.Empty:
					case ComponentTypeEnum.Timer:
						continue;

					case ComponentTypeEnum.NeedyCapacitor:
					case ComponentTypeEnum.NeedyKnob:
					case ComponentTypeEnum.NeedyVentGas:
					case ComponentTypeEnum.NeedyMod:
						moduleName = bombComponent.GetModuleDisplayName();
						keyModule = true;
						break;

					case ComponentTypeEnum.Mod:
						KMBombModule KMModule = bombComponent.GetComponent<KMBombModule>();
						keyModule = keyModules.Contains(KMModule.ModuleType);
						goto default;

					default:
						moduleName = bombComponent.GetModuleDisplayName();
						break;
				}
				Module module = new Module(bombComponent)
				{
					ComponentType = componentType,
					IsKeyModule = keyModule,
					ModuleName = moduleName
				};

				Modules.Add(module);
			}
			i++;
		}
		BombActive = true;
	}
	
}
