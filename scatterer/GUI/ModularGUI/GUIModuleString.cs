using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Runtime;
using UnityEngine;

namespace Scatterer
{
	public class GUIModuleString  : AbstractGUIModule
	{
		string localVariable = "";
		string label = "";
		
		object targetObject;
		FieldInfo targetField;
		
		public GUIModuleString (string label, object targetObject, string fieldName)
		{
			this.label = label;
			this.targetObject = targetObject;
			
			Type targetType = targetObject.GetType ();
			
			this.targetField = targetType.GetField(fieldName, Flags);
			this.localVariable = (string) targetField.GetValue(targetObject);
		}
		
		public override void RenderGUI()
		{
			
			GUILayout.BeginHorizontal ();
			
			GUILayout.Label (label);
			localVariable = GUILayout.TextField (localVariable);
			if (GUILayout.Button ("Set"))
			{
				targetField.SetValue(targetObject, localVariable);
			}
			GUILayout.EndHorizontal ();
		}
	}
}