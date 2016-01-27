#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.TS.Traits
{
	[Desc("Will AttackMove to a random location within MoveRadius when idle.",
		"This conflicts with player orders and should only be added to animal creeps.")]
	class VisceroidInfo : AttackWanderInfo, Requires<MobileInfo>, Requires<AttackBaseInfo>
	{
		[Desc("Time between rescanning for targets (in ticks).")]
		public readonly int TargetRescanInterval = 125;

		[Desc("The radius in which the actor \"searches\" for targets.")]
		public readonly WDist MaxSearchRadius = WDist.FromCells(20);

		public override object Create(ActorInitializer init) { return new Visceroid(init.Self, this); }
	}

	class Visceroid : AttackWander, ITick
	{
		enum VisceroidState
		{
			Roam,
			Flee,
			Heal,
			Fuse
		}

		readonly VisceroidInfo info;
		readonly Mobile mobile;
		readonly AttackBase attackTrait;

		VisceroidState state = VisceroidState.Roam;

		public Visceroid(Actor self, VisceroidInfo info)
			: base(self, info)
		{
		}

		public void Tick(Actor self)
		{
			throw new NotImplementedException();
		}

		protected override void DoAction(Actor self, CPos targetCell)
		{
			switch (state)
			{
				case VisceroidState.Flee:
					SearchForHealingArea(self);
					break;
				default:
					base.DoAction(self, targetCell);
					break;
			}
		}

		void SearchForHealingArea(Actor self)
		{
		}
	}
}
