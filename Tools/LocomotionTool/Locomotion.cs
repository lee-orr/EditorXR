using UnityEngine;
using UnityEngine.InputNew;

// GENERATED FILE - DO NOT EDIT MANUALLY
namespace UnityEngine.InputNew
{
	public class Locomotion : ActionMapInput {
		public Locomotion (ActionMap actionMap) : base (actionMap) { }
		
		public ButtonInputControl @blink { get { return (ButtonInputControl)this[0]; } }
		public ButtonInputControl @grip { get { return (ButtonInputControl)this[1]; } }
		public ButtonInputControl @thumb { get { return (ButtonInputControl)this[2]; } }
		public AxisInputControl @speed { get { return (AxisInputControl)this[3]; } }
		public ButtonInputControl @reverse { get { return (ButtonInputControl)this[4]; } }
		public ButtonInputControl @forward { get { return (ButtonInputControl)this[5]; } }
	}
}
