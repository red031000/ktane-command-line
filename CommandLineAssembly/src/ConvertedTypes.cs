using Assets.Scripts.Records;
using System.Reflection;

public static class CommonReflectedTypeInfo
{
    private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;

    static CommonReflectedTypeInfo()
    {
        HandlePassMethod = typeof(BombComponent).GetMethod("HandlePass", Flags);
        GameRecordCurrentStrikeIndexField = typeof(GameRecord).GetField("currentStrikeIndex", Flags);
        UpdateTimerDisplayMethod = typeof(TimerComponent).GetMethod("UpdateDisplay", Flags);
    }

    public static MethodInfo HandlePassMethod { get; private set; }

    public static MethodInfo UpdateTimerDisplayMethod { get; private set; }

    public static FieldInfo GameRecordCurrentStrikeIndexField { get; private set; }
}
