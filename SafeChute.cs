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
						foreach (SafeChutePart part in safeParts) {
							bool mDrag = false;
							bool mOpen = false;

							if (part.isRealChute) {
								string depState = "";
								foreach (var p in (System.Collections.IList)part.partModule.GetType ().GetField ("parachutes").GetValue (part.partModule)) {
									depState = (string)p.GetType ().GetField ("depState").GetValue (p);
									if (depState == "PREDEPLOYED") {
										mDrag = true;
									} else if (depState == "DEPLOYED") {
										mOpen = true;
									}
								}

							} else {
								mDrag = (((ModuleParachute)part.partModule).deploymentState == ModuleParachute.deploymentStates.SEMIDEPLOYED);
								mOpen = (((ModuleParachute)part.partModule).deploymentState == ModuleParachute.deploymentStates.DEPLOYED);
							}

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
					GUILayout.Label ("Part number: " + i);
					GUILayout.Label(" isRealChute:" + part.isRealChute);
					GUILayout.Label(" safetyActive:" + part.safetyActive);
					GUILayout.Label(" groundSafety:" + part.groundSafety);

					if (part.isRealChute)
					{
						var chutesArrayObj = part.partModule.GetType ().GetField ("parachutes").GetValue (part.partModule);

						int numChutes = (int)chutesArrayObj.GetType ().GetProperty("Count").GetValue(chutesArrayObj,null);

						bool anydeployed = (bool)part.partModule.GetType ().GetProperty ("anyDeployed").GetValue (part.partModule, null);

						GUILayout.Label (" numChutes in part:" + numChutes);
						GUILayout.Label (" anydeployed:" + anydeployed);

						int pNumber = 0;
						foreach (var p in (System.Collections.IList)chutesArrayObj) {
							GUILayout.Label (pNumber + " state: [" + (string)p.GetType ().GetField ("depState").GetValue (p)+"]");
							pNumber++;
						}

					}
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