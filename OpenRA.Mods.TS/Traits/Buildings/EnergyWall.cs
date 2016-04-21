﻿#region Copyright & License Information
/*
 * Copyright 2007-2016 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.GameRules;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.TS.Traits
{
	[Desc("Will open and be passable for actors that appear friendly when there are no enemies in range.")]
	public class EnergyWallInfo : UpgradableTraitInfo, IRulesetLoaded
	{
		[WeaponReference] public readonly string Weapon = "Firestorm";
		public WeaponInfo WeaponInfo { get; private set; }

		public readonly bool KillsAir = false;

		public override object Create(ActorInitializer init) { return new EnergyWall(init, this); }
		public void RulesetLoaded(Ruleset rules, ActorInfo ai) { WeaponInfo = rules.Weapons[Weapon.ToLowerInvariant()]; }
	}

	public class EnergyWall : UpgradableTrait<EnergyWallInfo>, ITick, INotifyAddedToWorld, ITemporaryBlocker, INotifyActorAboveEnergyWall
	{
		readonly EnergyWallInfo info;
		IEnumerable<CPos> blockedPositions;

		public EnergyWall(ActorInitializer init, EnergyWallInfo info) :
			base(info)
		{
			this.info = info;
		}

		void ITick.Tick(Actor self)
		{
			if (IsTraitDisabled || self.IsDisabled())
				return;

			foreach (var loc in blockedPositions)
			{
				var blockers = self.World.ActorMap.GetActorsAt(loc).Where(a => !a.IsDead && a != self);

				foreach (var blocker in blockers)
					info.WeaponInfo.Impact(Target.FromActor(blocker), self, Enumerable.Empty<int>());
			}
		}

		bool ITemporaryBlocker.IsBlocking(Actor self, CPos cell)
		{
			return !IsTraitDisabled && !self.IsDisabled() && blockedPositions.Contains(cell);
		}

		bool ITemporaryBlocker.CanRemoveBlockage(Actor self, Actor blocking)
		{
			return IsTraitDisabled || self.IsDisabled();
		}

		void INotifyActorAboveEnergyWall.ActorAboveEnergyWall(Actor self, Actor above)
		{
			if (!self.IsDisabled() && info.KillsAir && !above.IsDead)
				info.WeaponInfo.Impact(Target.FromActor(above), self, Enumerable.Empty<int>());
		}

		void INotifyAddedToWorld.AddedToWorld(Actor self)
		{
			blockedPositions = FootprintUtils.Tiles(self);
		}
	}
}
