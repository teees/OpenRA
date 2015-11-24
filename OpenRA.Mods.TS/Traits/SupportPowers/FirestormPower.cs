#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Effects;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.TS.Traits
{
	class FirestormPowerInfo : SupportPowerInfo
	{
		public override object Create(ActorInitializer init) { return new FirestormPower(init.Self, this); }
	}

	class FirestormPower : SupportPower, INotifyKilled, INotifyStanceChanged, INotifySold, INotifyOwnerChanged
	{
		public FirestormPower(Actor self, FirestormPowerInfo info)
			: base(self, info)
		{
		}

		public override void SelectTarget(Actor self, string order, SupportPowerManager manager)
		{
			self.World.IssueOrder(new Order(order, manager.Self, false));
		}

		public override void Charged(Actor self, string key)
		{
		}

		public override void Activate(Actor self, Order order, SupportPowerManager manager)
		{
		}

		public void Killed(Actor self, AttackInfo e) { }

		public void Selling(Actor self) { }
		public void Sold(Actor self) { }

		public void StanceChanged(Actor self, Player a, Player b, Stance oldStance, Stance newStance)
		{
		}

		public void OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
		}
	}
}
