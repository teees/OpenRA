#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public interface INotifyActorAbove
	{
		void Notify(Actor self, Actor above);
	}

	public class NotifyActorsBelowInfo : TraitInfo<NotifyActorsBelow> { }

	public class NotifyActorsBelow : ITick
	{
		public bool IsActive { get; private set; }

		public void Tick(Actor self)
		{
			var actorsBelow = self.World.ActorMap.GetActorsAt(self.Location).Where(a => a != self);
			foreach (var actor in actorsBelow)
			{
				var traits = actor.TraitsImplementing<INotifyActorAbove>().Where(Exts.IsTraitEnabled);
				foreach (var t in traits)
					t.Notify(actor, self);
			}
		}
	}
}
