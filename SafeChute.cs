using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using UnityEngine;

namespace GenesisRage
{
	public abstract class SafeChutePart
	{
		public object partModule;
		public bool dewarpedAtDeploy = false;
		public bool dewarpedAtGround = false;
		public bool isRealChute = false;
		public bool isFARChute = false;

		public SafeChutePart(PartModule pm)
		{
			partModule = pm;
#if DEBUG
			SafeChuteModule.SCprint("Added SafeChutePart with module "+pm.moduleName +
			                        " from part "+pm.part.name);
#endif
		}
		public abstract bool isDeployed();
	}

	public class SafeChuteFARPart : SafeChutePart
	{
		public override bool isDeployed()
		{
			return ((string)partModule.GetType().GetField("depState").GetValue(partModule) == "DEPLOYED");
		}

		public SafeChuteFARPart(PartModule pm) : base(pm)
		{}

	}

	public class SafeChuteRCPart : SafeChutePart
	{
		public override bool isDeployed()
		{
			System.Collections.IList pms = (System.Collections.IList)partModule.GetType().GetField("parachutes").GetValue(partModule);
			for (int i = pms.Count - 1; i >= 0; --i)
			{
				if ((string)pms[i].GetType().GetField("depState").GetValue(pms[i]) == "DEPLOYED")
				{
					return true;
				}
			}
			return false;
		}

		public SafeChuteRCPart(PartModule pm) : base(pm)
		{}

	}

	public class SafeChuteStockPart:SafeChutePart
	{
		public override bool isDeployed()
		{
			return (((ModuleParachute)partModule).deploymentState == ModuleParachute.deploymentStates.DEPLOYED);
		}

		public SafeChuteStockPart(PartModule pm):base(pm)
		{}

	}

	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class SafeChuteModule : MonoBehaviour
	{
		Vessel vessel;
		float deWarpGrnd;
		double maxAlt;
		List<SafeChutePart> safeParts = new List<SafeChutePart>();
		
		public void Awake()
		{
			// load XML config file, set default values if file doesnt exist
			KSP.IO.PluginConfiguration cfg = KSP.IO.PluginConfiguration.CreateForType<GenesisRage.SafeChuteModule>(null);
			cfg.load();

			deWarpGrnd = (float)cfg.GetValue<double>("DeWarpGround", 15.0f);
			maxAlt = (double)cfg.GetValue<double>("MaxAltitude", 10000.0f);
			SCprint (String.Format("DeWarpGround = {0}, MaxAltitude = {1}", deWarpGrnd, maxAlt));
		}
		
		public void Start()
		{
			ListChutes();
			GameEvents.onVesselWasModified.Add(ListChutes);
			GameEvents.onVesselChange.Add(ListChutes);
		}

		public void ListChutes(Vessel gameEventVessel=null)
		{
#if DEBUG
			SCprint("ListChutes");
#endif
			vessel = FlightGlobals.ActiveVessel;
			safeParts.Clear();

			// grab every parachute part from active vessel

			for (int i = vessel.Parts.Count - 1; i >= 0; --i){
				for (int j = vessel.parts[i].Modules.Count - 1; j >= 0; --j){
#if DEBUG
					SCprint(i.ToString() + "/" + j.ToString() + ": " + vessel.parts[i].Modules[j].moduleName);
#endif
					switch (vessel.parts[i].Modules[j].moduleName)
					{
						case "ModuleParachute": safeParts.Add(new SafeChuteStockPart(vessel.parts[i].Modules[j])); break;
						case "RealChuteModule": safeParts.Add(new SafeChuteRCPart   (vessel.parts[i].Modules[j])); break;
						case "RealChuteFAR":    safeParts.Add(new SafeChuteFARPart  (vessel.parts[i].Modules[j])); break;
					}
				}
			}
		}
		
		public void FixedUpdate()
		{
			// only proceed if we're FLYING and time warp is engaged
			if (FlightGlobals.ActiveVessel.situation == Vessel.Situations.FLYING
			    && TimeWarp.CurrentRateIndex > 0 )
			{
				// only proceed if the active vessel has parachutes
				if (safeParts.Count > 0)
				{
					// only proceed if current altitude is within range
					double alt = vessel.altitude;
					if (alt > 0.0f && alt < maxAlt)
					{
						for (int i = safeParts.Count - 1; i >= 0; --i)
						{
							if (!safeParts[i].dewarpedAtDeploy && safeParts[i].isDeployed())
							{
								safeParts[i].dewarpedAtDeploy = true;
								TimeWarp.SetRate(0, true, true);
							}

							if (!safeParts[i].dewarpedAtGround)
							{
								float altGrnd = vessel.GetHeightFromTerrain();
								if ((altGrnd < deWarpGrnd && altGrnd > 0.0f)
																|| (alt < deWarpGrnd && alt > 0.0f))
								{
									safeParts[i].dewarpedAtGround = true;
									TimeWarp.SetRate(0, true, true);
								}
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
				GUILayout.Label("SafeChute");
				GUILayout.Label("DeWarp:" + deWarpGrnd +
				                " Current:" + vessel.GetHeightFromTerrain().ToString("F02") +
				                "/" + vessel.altitude.ToString("F02"));
				foreach (SafeChutePart part in safeParts)
				{
					GUILayout.Label("#" + i.ToString()+
					                " dewarpedAtDeploy:" + part.dewarpedAtDeploy +
					                " dewarpedAtGround:" + part.dewarpedAtGround);
					i++;
				}

				GUILayout.EndVertical();
				GUILayout.EndArea();
				GUI.enabled = true;
			}
		}
#endif

		static public void SCprint(string tacos)
		{
			print("[SafeChute]: " + tacos); // tacos are awesome
		}

	}
}