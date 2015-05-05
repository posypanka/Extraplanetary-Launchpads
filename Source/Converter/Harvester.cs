using A;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace ExtraplanetaryLaunchpads {
	public class ExHarvester: BaseDrill, IResourceConsumer//, IModuleInfo
	{
		[KSPField(guiName = "", guiActive = true, guiActiveEditor = false)]
		public string ResourceStatus = "n/a";

		double heat;

		[KSPField]
		public string ResourceName = "";

		[KSPField]
		public float Rate;

		[KSPField(isPersistant = false)]
		public string HeadTransform;

		[KSPField(isPersistant = false)]
		public string TailTransform;

		Transform headTransform;
		Transform tailTransform;

		static List<IResourceProvider> resource_providers;
		double[] resource_amounts;
		RPLocation location;

		public List<PartResourceDefinition> GetConsumedResources()
		{
			var consumed = new List<PartResourceDefinition> ();
			for (int i = 0; i < inputList.Count; i++) {
				var res = inputList[i].ResourceName;
				var def = PartResourceLibrary.Instance.GetDefinition (res);
				consumed.Add (def);
			}
			return consumed;
		}

		protected override float GetHeatMultiplier(ConverterResults result, double deltaTime)
		{
			return 1 / (float)heat * HeatThrottle;
		}

		public override string GetInfo()
		{
			return "";
		}

		public override bool IsSituationValid()
		{
			return true;
		}

		ConversionRecipe LoadRecipe(double rate)
		{
			ConversionRecipe recipe = new ConversionRecipe();
			recipe.Inputs.AddRange(inputList);
			bool dumpExcess = false;
			recipe.Outputs.Add(new ResourceRatio {
				ResourceName = ResourceName,
				Ratio = rate,
				DumpExcess = dumpExcess
			});
			return recipe;
		}

		void FindTransforms ()
		{
			headTransform = part.FindModelTransform(HeadTransform);
			tailTransform = part.FindModelTransform(TailTransform);
		}

		private bool raycastGround(out RaycastHit hit)
		{
			var mask = 1 << 15;
			var start = tailTransform.position;
			var end = headTransform.position;
			var d = end - start;
			var len = d.magnitude;
			d = d.normalized;

			return Physics.Raycast(start, d, out hit, len, mask);
		}

		public override void OnStart(PartModule.StartState state)
		{
			if (!HighLogic.LoadedSceneIsFlight) {
				return;
			}
			if (resource_providers == null) {
				resource_providers = new List<IResourceProvider> ();
				resource_providers.Add (StockResourceProvider.Create ());
				var kethane = KethaneResourceProvider.Create ();
				if (kethane != null) {
					resource_providers.Add (kethane);
				}
			}
			resource_amounts = new double[resource_providers.Count];
			FindTransforms ();
			Fields["ResourceStatus"].guiName = ResourceName + " rate";
			base.OnStart(state);
		}

		protected override void PostProcess(ConverterResults result, double deltaTime)
		{
			for (int i = 0; i < resource_amounts.Length; i++) {
				double amount = resource_amounts[i] * result.TimeFactor;
				resource_providers[i].ExtractResource (ResourceName, location, amount);
			}
			if (result.TimeFactor < 1E-09) {
				status = "stalled";
			}
		}

		protected override void PostUpdateCleanup()
		{
			if (IsActivated) {
				ResourceStatus = string.Format("{0:0.000000}/sec", heat);
			} else {
				ResourceStatus = "n/a";
			}
		}

		protected override ConversionRecipe PrepareRecipe(double deltaTime)
		{
			RaycastHit hit;

			if (!raycastGround (out hit)) {
				status = "no ground contact";
				return null;
			}
			location = new RPLocation (vessel.mainBody, hit.point);
			double abundance = 0;
			for (int i = 0; i < resource_providers.Count; i++) {
				resource_amounts[i] = resource_providers[i].GetAbundance (ResourceName, location, Rate);
				abundance += resource_amounts[i];
			}
			if (abundance < 1e-6f) {
				status = "insufficient abundance";
				IsActivated = false;
				return null;
			}
			if (!IsActivated) {
				status = "Inactive";
				return null;
			}
			double rate = abundance * Efficiency * HeatThrottle;
			heat = rate;
			return LoadRecipe(rate);
		}
	}
}
