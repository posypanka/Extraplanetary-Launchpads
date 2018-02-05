/*
This file is part of Extraplanetary Launchpads.

Extraplanetary Launchpads is free software: you can redistribute it and/or
modify it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

Extraplanetary Launchpads is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Extraplanetary Launchpads.  If not, see
<http://www.gnu.org/licenses/>.
*/
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using KSP.IO;
using KSP.UI.Screens;

using ExtraplanetaryLaunchpads_KACWrapper;

namespace ExtraplanetaryLaunchpads {

	[KSPAddon (KSPAddon.Startup.Flight, false)]
	public class ELResourceWindow : MonoBehaviour
	{
		static ELResourceWindow instance;
		static bool hide_ui = false;
		static bool gui_enabled = true;
		static Rect windowpos;
		static bool link_lfo_sliders = true;
		static ScrollView resscroll = new ScrollView (680, 300);

		RMResourceManager resourceManager;
		bool []setSelected;
		List<string> resources;

		internal void Start()
		{
		}

		public static void ToggleGUI ()
		{
			gui_enabled = !gui_enabled;
			if (instance != null) {
				instance.UpdateGUIState ();
			}
		}

		public static void HideGUI ()
		{
			gui_enabled = false;
			if (instance != null) {
				instance.UpdateGUIState ();
			}
		}

		public static void ShowGUI ()
		{
			gui_enabled = true;
			if (instance != null) {
				instance.UpdateGUIState ();
			}
		}

		public static void LoadSettings (ConfigNode node)
		{
			string val = node.GetValue ("rect");
			if (val != null) {
				Quaternion pos;
				pos = ConfigNode.ParseQuaternion (val);
				windowpos.x = pos.x;
				windowpos.y = pos.y;
				windowpos.width = pos.z;
				windowpos.height = pos.w;
			}
			val = node.GetValue ("visible");
			if (val != null) {
				bool.TryParse (val, out gui_enabled);
			}
			val = node.GetValue ("link_lfo_sliders");
			if (val != null) {
				bool.TryParse (val, out link_lfo_sliders);
			}
		}

		public static void SaveSettings (ConfigNode node)
		{
			Quaternion pos;
			pos.x = windowpos.x;
			pos.y = windowpos.y;
			pos.z = windowpos.width;
			pos.w = windowpos.height;
			node.AddValue ("rect", KSPUtil.WriteQuaternion (pos));
			node.AddValue ("visible", gui_enabled);
			node.AddValue ("link_lfo_sliders", link_lfo_sliders);
		}

		void onVesselChange (Vessel v)
		{
			resourceManager = new RMResourceManager (v.parts, true);
			resscroll.Reset ();
			var set = new HashSet<string> ();
			setSelected = null;
			foreach (var s in resourceManager.resourceSets) {
				foreach (string r in s.resources.Keys) {
					set.Add (r);
				}
			}
			resources = set.ToList ();

			UpdateGUIState ();
		}

		void onVesselWasModified (Vessel v)
		{
			if (FlightGlobals.ActiveVessel == v) {

			}
		}

		void UpdateGUIState ()
		{
			enabled = !hide_ui && resourceManager != null && gui_enabled;
		}

		void onHideUI ()
		{
			hide_ui = true;
			UpdateGUIState ();
		}

		void onShowUI ()
		{
			hide_ui = false;
			UpdateGUIState ();
		}

		void Awake ()
		{
			instance = this;
			GameEvents.onVesselChange.Add (onVesselChange);
			GameEvents.onVesselWasModified.Add (onVesselWasModified);
			GameEvents.onHideUI.Add (onHideUI);
			GameEvents.onShowUI.Add (onShowUI);
			enabled = false;
		}

		void OnDestroy ()
		{
			instance = null;
			GameEvents.onVesselChange.Remove (onVesselChange);
			GameEvents.onVesselWasModified.Remove (onVesselWasModified);
			GameEvents.onHideUI.Remove (onHideUI);
			GameEvents.onShowUI.Remove (onShowUI);
		}

		void CloseButton ()
		{
			GUILayout.BeginHorizontal ();
			GUILayout.FlexibleSpace ();
			if (GUILayout.Button ("Close")) {
				HideGUI ();
			}
			GUILayout.FlexibleSpace ();
			GUILayout.EndHorizontal ();
		}

		void ResourceLine (string res)
		{
			GUILayout.BeginHorizontal ();
			GUILayout.Label (res, ELStyles.label);
			GUILayout.FlexibleSpace ();
			GUILayout.EndHorizontal ();
		}

		void HighlightPart (Part part, bool on)
		{
			if (on) {
				part.SetHighlightColor (XKCDColors.LightSeaGreen);
				part.SetHighlight (true, false);
			} else {
				part.SetHighlightDefault ();
			}
		}

		void HighlightSet (RMResourceSet set, string res, bool on)
		{
			RMResourceInfo info;
			if (set.resources.TryGetValue (res, out info)) {
				for (int i = 0; i < info.containers.Count; i++) {
					var c = info.containers[i];
					if (c is PartResourceContainer) {
						HighlightPart (c.part, on);
					} else if (c is ResourceSetContainer) {
						var sc = c as ResourceSetContainer;
						HighlightSet (sc.set, res, on);
					}
				}
			}
		}

		void FlowState (RMResourceSet set, string res)
		{
			bool curFlow = set.GetFlowState (res);
			bool newFlow = GUILayout.Toggle (curFlow, "flow");
			if (newFlow != curFlow) {
				set.SetFlowState (res, newFlow);
			}
		}

		void ModuleResourceLine (int ind, RMResourceSet set, string res,
								 bool highlight)
		{
			double amount = set.ResourceAmount (res);
			double maxAmount = set.ResourceCapacity (res);
			GUILayout.BeginHorizontal ();
			GUILayout.Label (set.name, ELStyles.label);
			GUILayout.FlexibleSpace ();
			GUILayout.Label (amount.ToString(), ELStyles.label);
			GUILayout.Label ("/", ELStyles.label);
			GUILayout.Label (maxAmount.ToString(), ELStyles.label);
			FlowState (set, res);
			GUILayout.EndHorizontal ();
			if (setSelected != null
				&& Event.current.type == EventType.Repaint) {
				var rect = GUILayoutUtility.GetLastRect();
				if (highlight && rect.Contains(Event.current.mousePosition)) {
					if (!setSelected[ind]) {
						setSelected[ind] = true;
						HighlightSet (set, res, true);
					}
				} else {
					if (setSelected[ind]) {
						setSelected[ind] = false;
						HighlightSet (set, res, false);
					}
				}
			}
		}

		void ResourceModules (bool highlight)
		{
			int ind = 0;
			for (int i = 0; i < resources.Count; i++) {
				string res = resources[i];
				ResourceLine (res);
				for (int j = 0; j < resourceManager.resourceSets.Count; j++) {
					var set = resourceManager.resourceSets[j];
					if (!set.resources.ContainsKey (res)) {
						continue;
					}
					ModuleResourceLine (ind++, set, res, highlight);
				}
			}
			if (setSelected == null && ind > 0) {
				setSelected = new bool[ind];
			}
		}

		void WindowGUI (int windowID)
		{
			ELStyles.Init ();

			GUILayout.BeginVertical ();

			resscroll.Begin ();
			ResourceModules (resscroll.mouseOver);
			resscroll.End ();

			GUILayout.EndVertical ();

			CloseButton ();

			GUI.DragWindow (new Rect (0, 0, 10000, 20));

		}

		void OnGUI ()
		{
			GUI.skin = HighLogic.Skin;
			windowpos = GUILayout.Window (GetInstanceID (),
										  windowpos, WindowGUI,
										  ELVersionReport.GetVersion (),
										  GUILayout.Width (695));
		}
	}
}
