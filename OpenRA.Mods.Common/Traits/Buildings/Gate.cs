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
	public interface IGate
	{
		bool IsOpen();
		bool CanOpen(Actor opener);
		void OnOpen(Actor opener);
	}

	[Desc("Will open and be passable for actors that appear friendly when there are no enemies in range.")]
	public class GateInfo : BuildingInfo, ITraitInfo, Requires<WithSpriteBodyInfo>
	{
		public readonly string OpenSequence = "open";
		public readonly string ClosingSequence = "closing";
		public readonly string ClosedSequence = "closed";

		public readonly string OpeningSound = "gateup1.aud";
		public readonly string ClosingSound = "gatedwn1.aud";

		[Desc("How many ticks until the gate will close after being opened.")]
		public readonly int CloseDelay = 80;

		public override object Create(ActorInitializer init) { return new Gate(init, this); }
	}

	public class Gate : Building, IGate, ITick, INotifyBlockingMove
	{
		readonly GateInfo info;
		readonly Actor self;
		readonly WithSpriteBody wsb;

		bool isOpen;
		bool opening;

		int remainingTime;

		public Gate(ActorInitializer init, GateInfo info) : base(init, info)
		{
			this.info = info;
			this.self = init.Self;

			wsb = self.Trait<WithSpriteBody>();
		}

		bool IGate.IsOpen() { return isOpen; }

		bool IGate.CanOpen(Actor opener)
		{
			return !self.IsDisabled() && BuildComplete && opener.AppearsFriendlyTo(self);
		}

		void IGate.OnOpen(Actor opener)
		{
			Open();
		}

		void ITick.Tick(Actor self)
		{
			if (self.IsDisabled() || !BuildComplete)
				return;

			if (isOpen)
			{
				if (remainingTime-- <= 0)
				{
					if (!IsBlocked())
						Close();
					else
						remainingTime = info.CloseDelay;
				}
				else if (remainingTime % 10 == 0 && IsBlocked())
					remainingTime = info.CloseDelay;
			}
		}

		void INotifyBlockingMove.OnNotifyBlockingMove(Actor self, Actor blocking)
		{
			if (!self.IsDisabled() && BuildComplete && !isOpen && (this as IGate).CanOpen(blocking))
				Open();
		}

		bool IsBlocked()
		{
			var eligibleLocations = FootprintUtils.Tiles(self).ToList();
			foreach (var loc in eligibleLocations)
			{
				var blockers = self.World.ActorMap.GetActorsAt(loc).Where(a => a != self);
				if (blockers != null && blockers.Any())
					return true;
			}

			return false;
		}

		void Open()
		{
			if (!opening && !isOpen)
			{
				opening = true;
				Game.Sound.Play(info.OpeningSound, self.CenterPosition);

				wsb.PlayCustomAnimationBackwards(self, info.ClosingSequence,
					() => {
						wsb.PlayCustomAnimationRepeating(self, info.OpenSequence);
						Opened();
					});
			}
		}

		void Opened()
		{
			self.World.ActorMap.RemoveInfluence(self, this);
			isOpen = true;
			opening = false;
			remainingTime = info.CloseDelay;
		}

		void Close()
		{
			if (isOpen)
			{
				isOpen = false;

				Game.Sound.Play(info.ClosingSound, self.CenterPosition);

				wsb.PlayCustomAnimation(self, info.ClosingSequence);
				self.World.ActorMap.AddInfluence(self, this);
			}
		}
	}
}
