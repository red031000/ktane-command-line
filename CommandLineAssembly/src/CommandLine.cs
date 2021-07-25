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
using System.Reflection;
using Module = CommandLineAssembly.Module;
using Assets.Scripts.Services;

public class CommandLine : MonoBehaviour
{

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
#pragma warning disable 414
    private bool Infinite = false; //stored for debugging purposes
#pragma warning restore 414
#if DEBUG
    public static readonly bool _isDebug = true;
#else
	public static readonly bool _isDebug = false;
#endif

    private List<Bomb> Bombs = new List<Bomb> { };
    private List<BombCommander> BombCommanders = new List<BombCommander> { };
    private List<Module> Modules = new List<Module> { };
    private static bool Leaderboardoff = false;

    private bool TwitchPlaysAvailable
    {
        get
        {
            if(_tpPresent == null)
            {
                if(GameObject.Find("TwitchPlays_Info") != null)
                {
                    InitTwitchPlays();
                    return true;
                }
                return false;
            }
            return (bool)_tpPresent;
        }
    }
    private bool? _tpPresent = null;
    private GameObject TwitchPlays;
    private const string TwitchPlaysHandle = "CommandLine";
    #endregion

    public void InitTwitchPlays()
    {
        TwitchPlays = GameObject.Find("TwitchPlays_Info");
        Component comp_gen = TwitchPlays.GetComponent("TwitchPlaysProperties");
        Assembly tp_asm = comp_gen.GetType().Assembly;
        Type useracc_type = tp_asm.GetType("UserAccess");
        MethodInfo adduser_meth = useracc_type.GetMethod("AddUser");
        adduser_meth.Invoke(null, new object[] { TwitchPlaysHandle, 0x10000 | 0x8000 | 0x4000 | 0x2000 });
    }

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
        if(Overlay.gameObject.activeSelf)
            Overlay.gameObject.SetActive(false);
        GameInfo.OnStateChange -= delegate (KMGameInfo.State state)
        {
            StateChange(state);
        };
        StopAllCoroutines();
    }

    private void Update()
    {
        if(_enabled)
        {
            if(Input.GetKeyDown(KeyCode.Backslash))
            {
                Overlay.gameObject.SetActive(!Overlay.gameObject.activeSelf);
                if(Overlay.gameObject.activeSelf)
                {
                    InputField.ActivateInputField();
                    InputField.text = string.Empty;
                }
            }

            if(Input.GetKeyDown(KeyCode.Return) && Overlay.gameObject.activeSelf && InputField.text != string.Empty)
            {
                Log(">" + InputField.text);
                ProcessCommand(InputField.text);
                InputField.text = string.Empty;
                InputField.ActivateInputField();
            }
            if(BombActive)
            {
                foreach(Module module in Modules)
                {
                    module.Update();
                }
            }
        }
        _wasAtBottom = ScrollRect.verticalNormalizedPosition <= 0.001f;
    }

    private void Log(string text)
    {
        Text newText = Instantiate(TextPrefab, Content.transform, false);
        newText.text = text;

        if(_wasAtBottom)
        {
            Canvas.ForceUpdateCanvases();
            ScrollRect.verticalNormalizedPosition = 0.0f;
            Canvas.ForceUpdateCanvases();
        }
    }

    private void HandleTwitchPlays(string message)
    {
        Component comp_gen = TwitchPlays.transform.parent.GetComponent("IRCConnection");
        Type comp_type = comp_gen.GetType();
        object instance_obj = comp_type.GetProperty("Instance").GetValue(null, null);
        FieldInfo messageRec_field = comp_type.GetField("OnMessageReceived");
        object messageRec_obj = messageRec_field.GetValue(instance_obj);
        Type messageRec_type = messageRec_field.FieldType;
        MethodInfo invoke_meth = messageRec_type.GetMethod("Invoke");
        invoke_meth.Invoke(messageRec_obj, new object[] { TwitchPlaysHandle, null, message });
    }

    private void ProcessCommand(string command)
    {
        string commandTrimmed = command.Trim().ToLowerInvariant();
        List<string> part = commandTrimmed.Split(new[] { ' ' }).ToList();
        if(part == null || part.Count == 0) part.Add(commandTrimmed);

        if(commandTrimmed.StartsWith("!") && TwitchPlaysAvailable && _isDebug)
        {
            Log($"Twitch Plays command send: {command}");
            HandleTwitchPlays(command);
        }
        else if(commandTrimmed == "exit")
        {
            Overlay.gameObject.SetActive(!Overlay.gameObject.activeSelf);
            InputField.text = string.Empty;
        }
        else if(commandTrimmed == "clear")
        {
            List<GameObject> children = new List<GameObject>();
            foreach(Transform child in Content.transform)
            {
                children.Add(child.gameObject);
            }

            children.ForEach(child => Destroy(child));
        }
        else if(commandTrimmed == "checkactive" && _isDebug)
        {
            Log(BombActive ? $"Bomb active: number {Bombs.Count}." : "Bomb not detected.");
            if(BombActive)
            {
                BombCommander heldBombCommander = GetHeldBomb();
                Log($"Currently held Bomb: {(heldBombCommander != null ? $"ID: {heldBombCommander.Id}" : "None")}");
                Module focusedModule = GetFocusedModule();
                Log($"Currently focused Module: {(focusedModule != null ? $"Name: {focusedModule.ModuleName}" : "None")}");
                Log($"Infinite mode active: {Infinite}");
            }
        }
        else if(part[0] == "detonate")
        {
            if(BombActive)
            {
                BombCommander heldBombCommader = GetHeldBomb();
                string reason = "Detonate Command";
                if(part.Count > 1)
                    reason = command.Substring(9);
                if(heldBombCommader != null)
                {
                    Log($"Detonating{(part.Count > 1 ? $" with reason {command.Substring(9)}" : "")}");
                    Debug.Log("[Command Line] Detonating bomb.");
                    heldBombCommader.Detonate(reason);
                }
                else
                {
                    Log("Please hold the bomb you wish to detonate");
                }
            }
            else
            {
                Log("Bomb not active, cannot detonate");
            }
        }
        else if(part[0] == "causestrike")
        {
            if(BombActive)
            {
                BombCommander heldBombCommander = GetHeldBomb();
                string reason = "Strike Command";
                if(part.Count > 1)
                    reason = command.Substring(12);
                if(heldBombCommander != null)
                {
                    Log($"Causing strike{(part.Count > 1 ? $" with reason {command.Substring(12)}" : "")}");
                    Debug.Log("[Command Line] Causing strike.");
                    heldBombCommander.CauseStrike(reason);
                }
                else
                {
                    Log("Please hold the bomb you wish to cause a strike on");
                }
            }
            else
            {
                Log("Bomb not active, cannot cause a strike");
            }
        }
        else if(part[0].EqualsAny("time", "t") && part.Count > 1 && part[1].EqualsAny("add", "increase", "change", "subtract", "decrease", "remove", "set"))
        {
            if(BombActive)
            {
                bool negative = part[1].EqualsAny("subtract", "decrease", "remove");
                bool direct = part[1].EqualsAny("set");
                BombCommander heldBombCommander = GetHeldBomb();
                if(heldBombCommander != null)
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
                    foreach(string split in part.Skip(2))
                    {
                        bool valid = false;
                        foreach(string unit in timeLengths.Keys)
                        {
                            if(!split.EndsWith(unit) || !float.TryParse(split.Substring(0, split.Length - unit.Length), out float length)) continue;
                            time += length * timeLengths[unit];
                            valid = true;
                            break;
                        }

                        if(valid)
                        {
                            time = (float)Math.Round((decimal)time, 2, MidpointRounding.AwayFromZero);
                            if(!direct && Math.Abs(time) == 0) break;
                            if(negative) time = -time;

                            if(direct)
                                heldBombCommander.TimerComponent.TimeRemaining = time;
                            else
                                heldBombCommander.TimerComponent.TimeRemaining = heldBombCommander.CurrentTimer + time;

                            if(originalTime < heldBombCommander.TimerComponent.TimeRemaining && !Leaderboardoff)
                            {
                                ChangeLeaderboard(true);
                                Debug.Log("[Command Line] Disabling leaderboard.");
                            }

                            if(direct)
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
                        }
                        else
                        {
                            Log("Time not valid");
                            break;
                        }
                    }
                }
                else
                {
                    Log("Please hold the bomb you wish to change the time on");
                }
            }
            else
            {
                Log("Bomb not active, cannot change time");
            }
        }
        else if(part[0].EqualsAny("strikes", "strike", "s") && part.Count > 1 && part[1].EqualsAny("add", "increase", "change", "subtract", "decrease", "remove", "set"))
        {
            if(BombActive)
            {
                BombCommander heldBombCommander = GetHeldBomb();
                if(heldBombCommander != null)
                {
                    bool negative = part[1].EqualsAny("subtract", "decrease", "remove");
                    bool direct = part[1].EqualsAny("set");
                    if(int.TryParse(part[2], out int strikes) && (strikes != 0 || direct))
                    {
                        int originalStrikes = heldBombCommander.StrikeCount;
                        if(negative) strikes = -strikes;

                        if(direct && strikes < 0)
                        {
                            strikes = 0;
                        }
                        else if(!direct && (heldBombCommander.StrikeCount + strikes) < 0)
                        {
                            strikes = -heldBombCommander.StrikeCount;
                        }

                        if(direct)
                            heldBombCommander.StrikeCount = strikes;
                        else
                            heldBombCommander.StrikeCount += strikes;

                        if(heldBombCommander.StrikeCount < originalStrikes && !Leaderboardoff)
                        {
                            ChangeLeaderboard(true);
                            Debug.Log("[Command Line] Disabling leaderboard.");
                        }

                        if(direct)
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
                }
                else
                {
                    Log("Please hold the bomb you wish to change the strikes on");
                }
            }
            else
            {
                Log("Bomb not active, cannot change strikes");
            }
        }
        else if(part[0].EqualsAny("ms", "maxstrikes", "sl", "strikelimit") && part.Count > 1 && part[1].EqualsAny("add", "increase", "change", "subtract", "decrease", "remove", "set"))
        {
            if(BombActive)
            {
                BombCommander heldBombCommander = GetHeldBomb();
                if(heldBombCommander != null)
                {
                    bool negative = part[1].EqualsAny("subtract", "decrease", "remove");
                    bool direct = part[1].EqualsAny("set");
                    if(int.TryParse(part[2], out int maxStrikes) && (maxStrikes != 0 || direct))
                    {
                        int originalStrikeLimit = heldBombCommander.StrikeLimit;
                        if(negative) maxStrikes = -maxStrikes;

                        if(direct && maxStrikes < 0)
                            maxStrikes = 0;
                        else if(!direct && (heldBombCommander.StrikeLimit + maxStrikes) < 0)
                            maxStrikes = -heldBombCommander.StrikeLimit;

                        if(direct)
                            heldBombCommander.StrikeLimit = maxStrikes;
                        else
                            heldBombCommander.StrikeLimit += maxStrikes;

                        if(originalStrikeLimit < heldBombCommander.StrikeLimit && !Leaderboardoff)
                        {
                            ChangeLeaderboard(true);
                            Debug.Log("[Command Line] Disabling leaderboard.");
                        }

                        if(direct)
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
                }
                else
                {
                    Log("Please hold the bomb you wish to change the strike limit on");
                }
            }
            else
            {
                Log("Bomb not active, cannot change strike limit");
            }
        }
        else if(commandTrimmed == "solve")
        {
            if(BombActive)
            {
                BombCommander heldBombCommander = GetHeldBomb();
                if(heldBombCommander != null)
                {
                    Module module = GetFocusedModule();
                    if(module != null)
                    {
                        if(!module.IsSolved)
                        {
                            switch(module.ComponentType)
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
                                    SolveModule(module);
                                    Log($"Solving module: {module.ModuleName}");
                                    Debug.Log($"[Command Line] Solved module: {module.ModuleName}");
                                    break;
                            }
                        }
                        else
                        {
                            Log("You cannot solve a module that's already been solved");
                        }
                    }
                    else
                    {
                        Log("Please focus on the module that you wish to solve");
                    }
                }
                else
                {
                    Log("Please hold the bomb that contains the module you wish to solve");
                }
            }
            else
            {
                Log("Bomb not active, cannot solve a module");
            }
        }
        else if(commandTrimmed == "solvebomb")
        {
            if(BombActive)
            {
                BombCommander heldBombCommander = GetHeldBomb();
                if(heldBombCommander != null)
                {
                    if(!Leaderboardoff)
                    {
                        ChangeLeaderboard(true);
                        Debug.Log("[Command Line] Disabling leaderboard.");
                    }
                    SolveMethods = new Queue<IEnumerator>();
                    SolveMethodsModules = new Queue<Module>();
                    foreach(Module module in Modules.Where(x => x.BombId == heldBombCommander.Id && x.IsSolvable && x.ComponentType != ComponentTypeEnum.Empty && x.ComponentType != ComponentTypeEnum.Timer))
                    {
                        if(!module.IsSolved) SolveModule(module);
                    }
                }
                else
                {
                    Log("Please hold the bomb that you wish to solve");
                }
            }
            else
            {
                Log("Bomb not active, cannot solve a bomb");
            }
        }
        else if(commandTrimmed == "pause")
        {
            if(BombActive)
            {
                BombCommander heldBombCommander = GetHeldBomb();
                if(heldBombCommander != null)
                {
                    if(heldBombCommander.TimerComponent.IsUpdating)
                    {
                        if(!Leaderboardoff)
                        {
                            ChangeLeaderboard(true);
                            Debug.Log("[Command Line] Disabling leaderboard.");
                        }
                        heldBombCommander.TimerComponent.StopTimer();
                        Debug.Log("[Command Line] Paused the bomb timer.");
                    }
                    else
                    {
                        Log("The held bomb is already paused");
                    }
                }
                else
                {
                    Log("Please hold the bomb that you wish to pause");
                }
            }
            else
            {
                Log("Bomb not active, cannot pause a bomb");
            }
        }
        else if(commandTrimmed == "unpause")
        {
            if(BombActive)
            {
                BombCommander heldBombCommander = GetHeldBomb();
                if(heldBombCommander != null)
                {
                    if(!heldBombCommander.TimerComponent.IsUpdating)
                    {
                        heldBombCommander.TimerComponent.StartTimer();
                        Debug.Log("[Command Line] Unpaused the bomb timer.");
                    }
                    else
                    {
                        Log("The held bomb is not paused");
                    }
                }
                else
                {
                    Log("Please hold the bomb you wish to pause");
                }
            }
            else
            {
                Log("Bomb not active, cannot unpause a bomb");
            }
        }
        else if(part[0].Trim().ToLowerInvariant().EqualsAny("turn", "rotate", "flip"))
        {
            if(BombActive)
            {
                BombCommander heldBombCommander = GetHeldBomb();
                if(heldBombCommander != null)
                {
                    StartCoroutine(heldBombCommander.TurnBombCoroutine());
                }
                else
                {
                    Log("Please hold the bomb you wish to turn");
                }
            }
            else
            {
                Log("Bomb not active, cannot turn bomb");
            }
        }
        else if(commandTrimmed == "help")
        {
            Log("Command reference:");
            Log("\"Detonate [reason]\" - detonate the currently held bomb, with an optional reason");
            Log("\"CauseStrike [reason]\" - cause a strike on the currently held bomb, with an optional reason");
            Log("\"Time (set|add|subtract) (time)(s|m|h)\" - changes the time on the currently held bomb (NOTE: this will disable leaderboards if you use it to achieve a faster time)");
            Log("\"Strikes (set|add|subtract) (number)\" - changes the strikes on the currently held bomb (NOTE: this will disable leaderboards if you use it to achieve a faster time)");
            Log("\"StrikeLimit (set|add|subtract) (number)\" - changes the strike limit on the currently held bomb (NOTE: this will disable leaderboards if you add a higher strike limit)");
            Log("\"Solve\" - solves the currently focused module (NOTE: this will disable leaderboards)");
            Log("\"SolveBomb\" - solves the currently held bomb (NOTE: this will disable leaderboards)");
            Log("\"Pause\" - pauses the timer on the currently held bomb (NOTE: this will disable leaderboards)");
            Log("\"Unpause\" - unpauses the timer on the currently held bomb");
            Log("\"Turn\" - turns the bomb to the opposite face");
            if(_isDebug)
            {
                Log("\"CheckActive\" - returns debugging info about the current bomb");
            }
        }
        else
        {
            Log("Command not valid");
        }
    }

    private BombCommander GetHeldBomb()
    {
        BombCommander held = null;
        foreach(BombCommander commander in BombCommanders)
        {
            if(commander.IsHeld())
                held = commander;
        }
        return held;
    }

    private Module GetFocusedModule()
    {
        Module focused = null;
        foreach(Module module in Modules)
        {
            if(module.IsHeld())
                focused = module;
        }
        return focused;
    }

    private static void ChangeLeaderboard(bool off)
    {
        if(RecordManager.Instance != null)
            RecordManager.Instance.DisableBestRecords = off;

        if(StatsManager.Instance != null)
            StatsManager.Instance.DisableStatChanges = off;

        if(AbstractServices.Instance != null)
            AbstractServices.Instance.LeaderboardMediator.DisableLeaderboardRequests = off;

        Leaderboardoff = off;
    }

    private void SolveModule(Module module)
    {
        if(!Leaderboardoff)
        {
            ChangeLeaderboard(true);
            Debug.Log("[Command Line] Disabling leaderboard.");
        }
        Debug.LogFormat("[Command Line] Solving module: {0}", module.ModuleName);
        try
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
            bool FoundSolveMethod = false;
            foreach(MonoBehaviour mb in module.BombComponent.GetComponentsInChildren<MonoBehaviour>())
            {
                MethodInfo method = mb.GetType().GetMethod("TwitchHandleForcedSolve", Flags);
                if(method == null) continue;
                if(method.ReturnType == typeof(void)) try { method.Invoke(mb, null); FoundSolveMethod = true; } catch { continue; }
                if(method.ReturnType == typeof(IEnumerator))
                {
                    try
                    {
                        IEnumerator e = (IEnumerator)method.Invoke(mb, null);
                        SolveMethods.Enqueue(e);
                        SolveMethodsModules.Enqueue(module);
                        if(!IsSolving)
                            IsSolving = true;
                        FoundSolveMethod = true;
                    }
                    catch { }
                }
            }
            if(IsSolving && !IsCoroutineStarted)
            {
                StartCoroutine(SolveBomb());
                IsSolving = false;
            }
            if(!FoundSolveMethod)
                throw new Exception();
        }
        catch
        {
            OldSolveModule(module);
        }
    }

    private void OldSolveModule(Module module)
    {
        try
        {
            CommonReflectedTypeInfo.HandlePassMethod.Invoke(module.BombComponent, null);
            foreach(MonoBehaviour behavior in module.BombComponent.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behavior.StopAllCoroutines();
            }
        }
        catch(Exception ex)
        {
            Log($"Exception while force solving module: {ex}");
        }
    }

    private Queue<IEnumerator> SolveMethods = new Queue<IEnumerator>();
    private Queue<Module> SolveMethodsModules = new Queue<Module>();
    private bool IsSolving = false;
    private bool IsCoroutineStarted = false;

    private IEnumerator SolveBomb()
    {
        IsCoroutineStarted = true;
        Debug.Log("[Command Line] Starting solve coroutine.");
        while(SolveMethods.Count > 0)
        {
            if(SolveMethods.Peek().Current is true)
            {
                SolveMethods.Enqueue(SolveMethods.Dequeue());
                SolveMethodsModules.Enqueue(SolveMethodsModules.Dequeue());
                yield return null;
            }
            else if(SolveMethods.Peek().Current is KMSelectable)
            {
                try
                {
                    ((KMSelectable)SolveMethods.Peek().Current).OnInteract();
                }
                catch
                {
                    SolveMethods.Dequeue();
                    OldSolveModule(SolveMethodsModules.Dequeue());
                }
                yield return null;
            }
            /*
            else if(SolveMethods.Peek().Current is IEnumerable<KMSelectable>)
            {
                foreach(KMSelectable item in SolveMethods.Peek().Current as IEnumerable<KMSelectable>)
                {
                    try
                    {
                        item.OnInteract();
                    }
                    catch
                    {
                        SolveMethods.Dequeue();
                        try
                        {
                            CommonReflectedTypeInfo.HandlePassMethod.Invoke(SolveMethodsModules.Peek().BombComponent, null);
                            foreach(MonoBehaviour behavior in SolveMethodsModules.Peek().BombComponent.GetComponentsInChildren<MonoBehaviour>(true))
                            {
                                behavior.StopAllCoroutines();
                            }
                        }
                        catch(Exception ex)
                        {
                            Log($"Exception while force solving module: {ex}");
                        }
                        SolveMethodsModules.Dequeue();
                    }
                    yield return null;
                }
            }
            */
            else
                yield return SolveMethods.Peek().Current;
            try
            {
                if(!SolveMethods.Peek().MoveNext())
                {
                    SolveMethods.Dequeue();
                    if(!SolveMethodsModules.Peek().IsSolved)
                        OldSolveModule(SolveMethodsModules.Peek());
                    SolveMethodsModules.Dequeue();
                }
            }
            catch
            {
                SolveMethods.Dequeue();
                OldSolveModule(SolveMethodsModules.Dequeue());
            }
        }
        IsCoroutineStarted = false;
    }

    private void StateChange(KMGameInfo.State state)
    {
        switch(state)
        {
            case KMGameInfo.State.Gameplay:
                ChangeLeaderboard(false);
                StartCoroutine(CheckForBomb());
                StartCoroutine(FactoryCheck());
                break;
            case KMGameInfo.State.Setup:
            case KMGameInfo.State.Quitting:
            case KMGameInfo.State.PostGame:
                Modules.Clear();
                BombActive = false;
                StopCoroutine(CheckForBomb());
                StopCoroutine(FactoryCheck());
                StopCoroutine(WaitUntilEndFactory());
                Bombs.Clear();
                BombCommanders.Clear();
                break;
        }
    }

    private IEnumerator CheckForBomb()
    {
        yield return new WaitUntil(() => SceneManager.Instance.GameplayState.Bombs != null && SceneManager.Instance.GameplayState.Bombs.Count > 0);
        yield return new WaitForSeconds(2.0f);
        Bombs.AddRange(SceneManager.Instance.GameplayState.Bombs);
        int i = 0;
        string[] keyModules =
        {
            "SouvenirModule", "MemoryV2", "TurnTheKey", "TurnTheKeyAdvanced", "theSwan", "HexiEvilFMN", "taxReturns"
        };
        foreach(Bomb bomb in Bombs)
        {
            BombCommanders.Add(new BombCommander(bomb, i));
            foreach(BombComponent bombComponent in bomb.BombComponents)
            {
                ComponentTypeEnum componentType = bombComponent.ComponentType;
                bool keyModule = false;
                string moduleName = string.Empty;

                switch(componentType)
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
                Module module = new Module(bombComponent, i)
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

    #region Factory Implementation
    private IEnumerator FactoryCheck()
    {
        yield return new WaitUntil(() => SceneManager.Instance.GameplayState.Bombs != null && SceneManager.Instance.GameplayState.Bombs.Count > 0);
        GameObject _gameObject = null;
        for(var i = 0; i < 4 && _gameObject == null; i++)
        {
            _gameObject = GameObject.Find("Factory_Info");
            yield return null;
        }

        if(_gameObject == null) yield break;

        _factoryType = ReflectionHelper.FindType("FactoryAssembly.FactoryRoom");
        if(_factoryType == null) yield break;

        _factoryBombType = ReflectionHelper.FindType("FactoryAssembly.FactoryBomb");
        _internalBombProperty = _factoryBombType.GetProperty("InternalBomb", BindingFlags.NonPublic | BindingFlags.Instance);

        _factoryStaticModeType = ReflectionHelper.FindType("FactoryAssembly.StaticMode");
        _factoryFiniteModeType = ReflectionHelper.FindType("FactoryAssembly.FiniteSequenceMode");
        _factoryInfiniteModeType = ReflectionHelper.FindType("FactoryAssembly.InfiniteSequenceMode");
        _currentBombField = _factoryFiniteModeType.GetField("_currentBomb", BindingFlags.NonPublic | BindingFlags.Instance);

        _gameModeProperty = _factoryType.GetProperty("GameMode", BindingFlags.NonPublic | BindingFlags.Instance);

        List<UnityEngine.Object> factoryObject = FindObjectsOfType(_factoryType).ToList();

        if(factoryObject == null || factoryObject.Count == 0) yield break;

        _factory = factoryObject[0];
        _gameroom = _gameModeProperty.GetValue(_factory, new object[] { });
        if(_gameroom.GetType() == _factoryInfiniteModeType)
        {
            Infinite = true;
            StartCoroutine(WaitUntilEndFactory());
        }
    }

    private UnityEngine.Object GetBomb => (UnityEngine.Object)_currentBombField.GetValue(_gameroom);

    private IEnumerator WaitUntilEndFactory()
    {
        yield return new WaitUntil(() => GetBomb != null);

        while(GetBomb != null)
        {
            UnityEngine.Object currentBomb = GetBomb;
            Bomb bomb1 = (Bomb)_internalBombProperty.GetValue(currentBomb, null);
            yield return new WaitUntil(() => bomb1.HasDetonated || bomb1.IsSolved());

            Modules.Clear();
            BombCommanders.Clear();
            Bombs.Clear();

            while(currentBomb == GetBomb)
            {
                yield return new WaitForSeconds(0.10f);
                if(currentBomb != GetBomb)
                    continue;
                yield return new WaitForSeconds(0.10f);
            }

            StartCoroutine(CheckForBomb());
        }
    }
    //factory specific types

    private static Type _factoryType = null;
    private static Type _factoryBombType = null;
    private static PropertyInfo _internalBombProperty = null;

    private static Type _factoryStaticModeType = null;
    private static Type _factoryFiniteModeType = null;
    private static Type _factoryInfiniteModeType = null;

    private static PropertyInfo _gameModeProperty = null;
    private static FieldInfo _currentBombField = null;

    private object _factory = null;
    private object _gameroom = null;
    #endregion
}
