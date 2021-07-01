using System;
using System.Collections.Generic;
using Lime;

namespace Orange
{
	public interface ICookingStage
	{
		IEnumerable<(string, SHA256)> EnumerateCookingUnits();
		void Cook(string cookingUnit, SHA256 cookingUnitHash);
	}
}
