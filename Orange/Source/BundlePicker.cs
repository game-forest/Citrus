using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lime;

namespace Orange
{
	public class BundlePicker
	{
		private Dictionary<string, bool> bundleSelectionStates;
		private List<string> allBundles;

		/// <summary>
		/// When enabled, user can select which bundles to use in actions.
		/// When disabled, actions will use all / required bundles.
		/// </summary>
		public bool Enabled;

		/// <summary>
		/// Creates selection states dictionary for bundles in current project.
		/// To be able to select bundles you should manually enable BundlePicker after setup.
		/// </summary>
		public void Setup()
		{
			if (bundleSelectionStates == null) {
				bundleSelectionStates = new Dictionary<string, bool>();
			} else {
				bundleSelectionStates.Clear();
			}
			Enabled = false;

			allBundles = Toolbox.GetListOfAllBundles(The.UI.GetActiveTarget());
			foreach (var bundle in allBundles) {
				bundleSelectionStates.Add(bundle, true);
			}
		}

		/// <summary>
		/// Updates current bundle list: new bundles will be added to list (default state: checked),
		/// deleted bundles will be removed from list. Returns list of changed (added or deleted) bundles.
		/// </summary>
		public List<string> Refresh()
		{
			var changed = new List<string>();
			allBundles = Toolbox.GetListOfAllBundles(The.UI.GetActiveTarget());

			// Remove no longer existing bundles
			foreach (var bundle in bundleSelectionStates.Keys.ToArray()) {
				if (!allBundles.Contains(bundle)) {
					changed.Add(bundle);
					bundleSelectionStates.Remove(bundle);
				}
			}

			// Add new bundles
			foreach (var bundle in allBundles) {
				if (!bundleSelectionStates.ContainsKey(bundle)) {
					changed.Add(bundle);
					bundleSelectionStates.Add(bundle, true);
				}
			}

			return changed;
		}

		/// <summary>
		/// Returns list of all bundles.
		/// </summary>
		/// <param name="refresh">Set to 'true' if you want receive always up-to-date list.
		/// Careful it's a heavy operation and may impact performance significantly.</param>
		public List<string> GetListOfBundles(bool refresh = false)
		{
			if (allBundles == null) {
				Setup();
			} else if (refresh) {
				Refresh();
			}
			return allBundles;
		}

		/// <summary>
		/// Returns list of all / required bundles if not enabled; Returns list of selected bundles otherwise.
		/// </summary>
		public List<string> GetSelectedBundles()
		{
			if (allBundles == null) {
				Setup();
			}
			if (!Enabled) {
				Refresh();
				return The.UI.GetActiveAction().UsesTargetBundles && The.UI.GetActiveTarget().Bundles.Any()
					? The.UI.GetActiveTarget().Bundles.ToList()
					: allBundles;
			}
			return bundleSelectionStates.Where(x => x.Value).Select(x => x.Key).ToList();
		}

		/// <summary>
		/// Sets bundle state.
		/// </summary>
		/// <param name="bundle">Path to bundle, relative to current project folder</param>
		/// <param name="state">'true' if bundle should be selected, 'false' otherwise.
		/// It would be ignored and always stays 'true' if both current <see cref="Orange.Target"/> requires 'bundle'
		/// to be built and current <see cref="Orange.Actions"/> has 'UsesTargetBundles' set to 'true'. </param>
		public bool SetBundleSelection(string bundle, bool state)
		{
			return bundleSelectionStates[bundle] =
				state ||
				The.UI.GetActiveAction().UsesTargetBundles &&
				The.UI.GetActiveTarget().Bundles.Contains(bundle);
		}
	}
}
