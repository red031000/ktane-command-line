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
				selectable.OnInteract += delegate
				{
					interacting = true;
					return true;
				};
				selectable.OnCancel += delegate
				{
					interacting = false;
					return true;
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
