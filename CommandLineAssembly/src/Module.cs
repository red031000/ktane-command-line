using Assets.Scripts.Missions;

namespace CommandLineAssembly
{
	public class Module
	{
		public BombComponent BombComponent { get; private set; }
		public string ModuleName;
		public Selectable selectable;
		public bool IsKeyModule = false;

		private bool interacting = false;

		public ComponentTypeEnum ComponentType;

		public bool IsSolved => BombComponent.IsSolved;
		
		public Module(BombComponent bombComponent)
		{
			BombComponent = bombComponent;
			if (bombComponent.ComponentType != ComponentTypeEnum.Empty && BombComponent.ComponentType != ComponentTypeEnum.Timer)
			{
				selectable = BombComponent.GetComponent<Selectable>();
				var OldOnInterract = selectable.OnInteract;
				selectable.OnInteract = delegate
				{
					var result = OldOnInterract();
					interacting = true;
					return result;
				};
				var OldOnCancel = selectable.OnCancel;
				selectable.OnCancel += delegate
				{
					var result = OldOnCancel();
					interacting = false;
					return result;
				};
				selectable.OnDeselect += delegate
				{
					interacting = false;
				};
			}
		}

		public bool IsHeld()
		{
			return interacting;
		}
	}
}
