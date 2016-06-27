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
		public bool dewarpedAtDeploy = false;
		public bool dewarpedAtGroundd = false;
		public bool isRealChute = false;
		public bool isFARChute = false;

		public SafeChutePart(PartModule pm, bool rc, bool fc)
		{
			partModule = pm;
			isRealChute = rc;
			isFARChute = fc;
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
			Debug.Log (String.Format("SafeChute values: DeWarpGround = {0}, MaxAltitude = {1}", deWarpGrnd, maxAlt));
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
					switch (pm.moduleName)
					{
						case "ModuleParachute": safeParts.Add(new SafeChutePart(pm, false, false)); break;
						case "RealChuteModule": safeParts.Add(new SafeChutePart(pm, true, false)); break;
						case "RealChuteFAR":    safeParts.Add(new SafeChutePart(pm, false, true)); break;
					}
				}
			}
		}
		
		public void FixedUpdate()
		{
			if (HighLogic.LoadedScene == GameScenes.FLIGHT && FlightGlobals.ActiveVessel.situation == Vessel.Situations.FLYING && TimeWarp.CurrentRateIndex > 0 )
			{
				// check to see if switching avtive vessel, or un/docking
				if (vessel != FlightGlobals.ActiveVessel || vesselParts != FlightGlobals.ActiveVessel.parts.Count)
					// re-grab parachute parts
					ListChutes();
				
				// only proceed if the active vessel has parachutes and time warping
				if (safeParts.Count > 0)
				{
					float alt = (float)vessel.altitude;
					float altGrnd = vessel.GetHeightFromTerrain();
					
					// only proceed if current altitude is within range
					if (alt > 0.0f && alt < maxAlt)
					{
						foreach (SafeChutePart part in safeParts) {
							bool mOpen = false;

							if (part.isRealChute) {
								string depState = "";
								foreach (var p in (System.Collections.IList)part.partModule.GetType ().GetField ("parachutes").GetValue (part.partModule)) {
									depState = (string)p.GetType ().GetField ("depState").GetValue (p);
									if (depState == "DEPLOYED") {
										mOpen = true;
									}
								}

							} else if (part.isFARChute) {
								string depState = "";
								depState = (string)part.partModule.GetType().GetField("depState").GetValue(part.partModule);
								if (depState == "DEPLOYED")
								{
									mOpen = true;
								}
							} else {
								mOpen = (((ModuleParachute)part.partModule).deploymentState == ModuleParachute.deploymentStates.DEPLOYED);
							}

							if (!part.dewarpedAtDeploy && mOpen)
							{
								part.dewarpedAtDeploy = true;
								TimeWarp.SetRate(0, true, true);
							}
								
							if (!part.dewarpedAtGroundd && ((altGrnd < deWarpGrnd && altGrnd > 0.0f) || (alt < deWarpGrnd && alt > 0.0f)))
							{
								part.dewarpedAtGroundd = true;
								TimeWarp.SetRate (0, true, true);
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
					GUILayout.Label(" dewarpedAtDeploy:" + part.dewarpedAtDeploy);
					GUILayout.Label(" dewarpedAtGroundd:" + part.dewarpedAtGroundd);

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