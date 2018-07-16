using Assets.Scripts.Input;
using Assets.Scripts.Missions;
using Assets.Scripts.Records;
using System;
using System.Collections;
using UnityEngine;

namespace CommandLineAssembly
{
	public class BombCommander
	{
		public Bomb Bomb { get; private set; } = null;
		public TimerComponent TimerComponent = null;
		public WidgetManager WidgetManager { get; private set; } = null;
		public Selectable Selectable { get; private set; } = null;
		public FloatingHoldable FloatingHoldable { get; private set; } = null;

		public SelectableManager SelectableManager { get; private set; } = null;
		public float CurrentTimer
		{
			get => TimerComponent.TimeRemaining;
			set => TimerComponent.TimeRemaining = (value < 0) ? 0 : value;
		}
		public int StrikeLimit
		{
			get => Bomb.NumStrikesToLose;
			set { Bomb.NumStrikesToLose = value; HandleStrikeChanges(); }
		}
		public int StrikeCount
		{
			get => Bomb.NumStrikes;
			set
			{
				if (value < 0) value = 0;
				Bomb.NumStrikes = value;
				HandleStrikeChanges();
			}
		}
		public float CurrentTimerElapsed => TimerComponent.TimeElapsed;
		public readonly int Id;

		public BombCommander(Bomb bomb, int id)
		{
			Bomb = bomb;
			TimerComponent = bomb.GetTimer();
			WidgetManager = bomb.WidgetManager;
			Selectable = bomb.GetComponent<Selectable>();
			FloatingHoldable = bomb.GetComponent<FloatingHoldable>();
			Id = id;
			SelectableManager = KTInputManager.Instance.SelectableManager;
		}

		public bool IsHeld()
		{
			return FloatingHoldable.HoldState == FloatingHoldable.HoldStateEnum.Held ? true : false;
		}

		public void Detonate(string reason)
		{
			for (int strikesToMake = StrikeLimit - StrikeCount; strikesToMake > 0; --strikesToMake)
			{
				CauseStrike(reason);
			}
		}

		public void CauseStrike(string reason)
		{
			StrikeSource strikeSource = new StrikeSource
			{
				ComponentType = ComponentTypeEnum.Mod,
				InteractionType = InteractionTypeEnum.Other,
				Time = CurrentTimerElapsed,
				ComponentName = reason
			};

			RecordManager recordManager = RecordManager.Instance;
			recordManager.RecordStrike(strikeSource);

			Bomb.OnStrike(null);
		}

		private void HandleStrikeChanges()
		{
			int strikeLimit = StrikeLimit;
			int strikeCount = Math.Min(StrikeCount, StrikeLimit);

			RecordManager RecordManager = RecordManager.Instance;
			GameRecord GameRecord = RecordManager.GetCurrentRecord();
			StrikeSource[] Strikes = GameRecord.Strikes;
			if (Strikes.Length != strikeLimit)
			{
				StrikeSource[] newStrikes = new StrikeSource[Math.Max(strikeLimit, 1)];
				Array.Copy(Strikes, newStrikes, Math.Min(Strikes.Length, newStrikes.Length));
				GameRecord.Strikes = newStrikes;
			}

			if (strikeCount == strikeLimit)
			{
				if (strikeLimit < 1)
				{
					Bomb.NumStrikesToLose = 1;
					strikeLimit = 1;
				}
				Bomb.NumStrikes = strikeLimit - 1;
				CommonReflectedTypeInfo.GameRecordCurrentStrikeIndexField.SetValue(GameRecord, strikeLimit - 1);
				CauseStrike("Strike count / limit changed");
			}
			else
			{
				Debug.Log(string.Format("[Bomb] Strike from CommandLine! {0} / {1} strikes", StrikeCount, StrikeLimit));
				CommonReflectedTypeInfo.GameRecordCurrentStrikeIndexField.SetValue(GameRecord, strikeCount);
				float[] rates = { 1, 1.25f, 1.5f, 1.75f, 2 };
				TimerComponent.SetRateModifier(rates[Math.Min(strikeCount, 4)]);
				Bomb.StrikeIndicator.StrikeCount = strikeCount;
			}
		}
#if DEBUG
		public IEnumerator TurnBombCoroutine()
		{
			float duration = FloatingHoldable.PickupTime;
			Transform baseTransform = SelectableManager.GetBaseHeldObjectTransform();

			float oldZSpin = SelectableManager.GetZSpin();
			float targetZSpin = 180.0f;
			FaceEnum CurrentActiveFace = FloatingHoldable.ActiveFace;
			switch (CurrentActiveFace)
			{
				case FaceEnum.Front:
					targetZSpin = 180.0f;
					break;
				case FaceEnum.Rear:
					targetZSpin = 0.0f;
					break;
			}


			float initialTime = Time.time;
			while (Time.time - initialTime < duration)
			{
				float lerp = (Time.time - initialTime) / duration;
				float currentZSpin = Mathf.SmoothStep(oldZSpin, targetZSpin, lerp);

				Quaternion currentRotation = Quaternion.Euler(0.0f, 0.0f, currentZSpin);

				SelectableManager.SetZSpin(currentZSpin);
				SelectableManager.SetControlsRotation(baseTransform.rotation * currentRotation);
				SelectableManager.HandleFaceSelection();
				yield return null;
			}

			SelectableManager.SetZSpin(targetZSpin);
			SelectableManager.SetControlsRotation(baseTransform.rotation * Quaternion.Euler(0.0f, 0.0f, targetZSpin));
			SelectableManager.HandleFaceSelection();
		}
#else
		public IEnumerator TurnBombCoroutine()
		{
			yield break;
		}
#endif
	}
}
