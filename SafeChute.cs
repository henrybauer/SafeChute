using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using UnityEngine;

namespace GenesisRage
{
	public class SafeChutePart
	{
		public object partModule;
		public bool safetyActive = false;
		public bool groundSafety = false;
		public bool isRealChute = false;

		public SafeChutePart(PartModule pm)
		{
			partModule = pm;
		}
		public SafeChutePart(PartModule pm, bool rc)
		{
			partModule = pm;
			isRealChute = rc;
		}
	}

	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class SafeChuteModule : MonoBehaviour
	{
		Vessel vessel;
		float deWarpGrnd;
		float maxAlt;
		int vesselParts = 0;
		List<SafeChutePart> safeParts = new List<SafeChutePart>();
		
		public void Awake()
		{
			// load XML config file, set default values if file doesnt exist
			KSP.IO.PluginConfiguration cfg = KSP.IO.PluginConfiguration.CreateForType<GenesisRage.SafeChuteModule>(null);
			cfg.load();

			deWarpGrnd = (float)cfg.GetValue<double>("DeWarpGround", 15.0f);
			maxAlt = (float)cfg.GetValue<double>("MaxAltitude", 10000.0f);
		}
		
		public void Start()
		{
			ListChutes();
		}
		
		public void ListChutes()
		{
			vessel = FlightGlobals.ActiveVessel;
			vesselParts = vessel.parts.Count;
			safeParts.Clear();

			// grab every parachute part from active vessel
			foreach (Part p in vessel.parts)
			{
				foreach (PartModule pm in p.Modules)
				{
					if (pm.moduleName == "ModuleParachute")
					{
						safeParts.Add(new SafeChutePart(pm));
					}
					if (pm.moduleName == "RealChuteModule")
					{
						safeParts.Add(new SafeChutePart(pm, true));
					}
				}
			}
		}
		
		public void FixedUpdate()
		{
			if (HighLogic.LoadedScene == GameScenes.FLIGHT)
			{
				// check to see if switching avtive vessel, or un/docking
				if (vessel != FlightGlobals.ActiveVessel || vesselParts != FlightGlobals.ActiveVessel.parts.Count)
					// re-grab parachute parts
					ListChutes();
				
				// only proceed if the active vessel has parachutes and time warping
				if (safeParts.Count > 0 && TimeWarp.CurrentRateIndex > 0)
				{
					float alt = (float)vessel.altitude;
					float altGrnd = vessel.GetHeightFromTerrain();
					
					// only proceed if current altitude is within range
					if (alt > 0.0f && alt < maxAlt)
					{
						foreach (SafeChutePart part in safeParts)
						{
							if (part.isRealChute)
							{
								// real chute calculations here
								bool mDrag = ((string)part.partModule.GetType().GetField("depState").GetValue(part.partModule) == "PREDEPLOYED" ||
								              (string)part.partModule.GetType().GetField("secDepState").GetValue(part.partModule) == "PREDEPLOYED");
								bool mOpen = ((string)part.partModule.GetType().GetField("depState").GetValue(part.partModule) == "DEPLOYED" ||
								              (string)part.partModule.GetType().GetField("secDepState").GetValue(part.partModule) == "DEPLOYED");

								if (!part.safetyActive && mDrag)
								{
									part.safetyActive = true;
								}
								else if (part.safetyActive && mOpen)
								{
									part.safetyActive = false;
									part.groundSafety = true;
									TimeWarp.SetRate(0, true);
								}

							} else {
								bool mDrag = (((ModuleParachute)part.partModule).deploymentState == ModuleParachute.deploymentStates.SEMIDEPLOYED);
								bool mOpen = (((ModuleParachute)part.partModule).deploymentState == ModuleParachute.deploymentStates.DEPLOYED);

								if (!part.safetyActive && mDrag)
								{
									part.safetyActive = true;
								}
								else if (part.safetyActive && mOpen)
								{
									part.safetyActive = false;
									part.groundSafety = true;
									TimeWarp.SetRate(0, true);
								}
							}

							if (part.groundSafety && ((altGrnd > 0.0f && altGrnd < deWarpGrnd) || (alt > 0.0f && alt < deWarpGrnd)))
							{
								part.groundSafety = false;
								TimeWarp.SetRate(0, true);
							}
						}
					}
				}
			}
		}

#if DEBUG
		public void OnGUI()
		{
			if (HighLogic.LoadedScene == GameScenes.FLIGHT)
			{
				GUI.enabled = false;
				GUILayout.BeginArea(new Rect(100,100,400,400));
				GUILayout.BeginVertical("box");
				int i = 0;
				GUILayout.Label("DeWarp:" + deWarpGrnd + " Current:" + vessel.GetHeightFromTerrain() + "/" + (float)vessel.altitude);
				foreach (SafeChutePart part in safeParts)
				{
					if (part.isRealChute)
					{
						GUILayout.Label("(" + i + ")" +
						                " STATE:" + part.partModule.GetType().GetField("depState").GetValue(part.partModule) +
										" STATE:" + part.partModule.GetType().GetField("secDepState").GetValue(part.partModule));
					}
						GUILayout.Label("(" + i + ")" +
//					                " RC:" + part.isRealChute +
					                " SA:" + part.safetyActive +
					                " GS:" + part.groundSafety);
					i++;
				}
				GUILayout.EndVertical();
				GUILayout.EndArea();
				GUI.enabled = true;
			}
		}
#endif
	}
}