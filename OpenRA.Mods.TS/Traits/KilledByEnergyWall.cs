#region Copyright & License Information
/*
 * Copyright 2007-2016 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.TS.Traits
{
	public interface INotifyActorAboveEnergyWall
	{
		void ActorAboveEnergyWall(Actor self, Actor above);
	}

	[Desc("Actor notifies firestorm wall tiles when positions overlap.")]
	public class KilledByEnergyWallInfo : TraitInfo<KilledByEnergyWall>{ }

	public class KilledByEnergyWall : ITick
	{
		void ITick.Tick(Actor self)
		{
			self.World.ActorMap.GetActorsAt(self.Location).Where(a => a != self)
				.Do(a => a.TraitsImplementing<INotifyActorAboveEnergyWall>()
					.Where(Exts.IsTraitEnabled).Do(t => t.ActorAboveEnergyWall(a, self)));
		}
	}
}
