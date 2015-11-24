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

using System;
using System.Linq;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.TS.Traits
{
	class FirestormPowerInfo : SupportPowerInfo
	{
		[ActorReference, FieldLoader.Require]
		[Desc("Dummy Actor to spawn.")]
		public readonly string Actor = null;

		[Desc("Amount of time to keep the actor alive in ticks. Value < 0 means this actor will not remove itself.")]
		public readonly int LifeTime = 250;

		public override object Create(ActorInitializer init) { return new FirestormPower(init.Self, this); }
	}

	class FirestormPower : SupportPower, INotifyKilled, INotifySold, INotifyOwnerChanged, ITick
	{
		readonly FirestormPowerInfo info;

		Actor dummyActor;
		SupportPowerInstance instance;

		public FirestormPower(Actor self, FirestormPowerInfo info)
			: base(self, info)
		{
			this.info = info;
			instance = null;
		}

		public override void SelectTarget(Actor self, string order, SupportPowerManager manager)
		{
			self.World.IssueOrder(new Order(order, manager.Self, false));
		}

		public override void Activate(Actor self, Order order, SupportPowerManager manager)
		{
			if (manager.Powers.ContainsKey(order.OrderString))
				instance = manager.Powers[order.OrderString];
			if (dummyActor != null)
				DisposeDummyActor(self);
			if (info.Actor != null)
			{
				self.World.AddFrameEndTask(w =>
				{
					dummyActor = w.CreateActor(info.Actor, new TypeDictionary
					{
						new LocationInit(order.TargetLocation),
						new OwnerInit(self.Owner),
					});

					if (info.LifeTime > -1)
					{
						dummyActor.QueueActivity(new Wait(info.LifeTime));
						dummyActor.QueueActivity(new RemoveSelf());
					}
				});
			}
		}

		void ITick.Tick(Actor self)
		{
			if (dummyActor != null && self.IsDisabled())
				DisposeDummyActor(self);
		}

		void DisposeDummyActor(Actor self)
		{
			if (dummyActor == null)
				return;
			else if (dummyActor.IsDead)
			{
				dummyActor = null;
				return;
			}

			if (instance != null && instance.Active)
			{
				var sp = instance.Instances.FirstOrDefault(a => !a.Self.IsDisabled() && a.Self != self);
				if (sp != null)
				{
					((FirestormPower)sp).dummyActor = dummyActor;
					return;
				}
			}

			self.World.AddFrameEndTask(w =>
			{
				if (dummyActor != null)
				{
					dummyActor.Dispose();
					dummyActor = null;
				}
			});
		}

		void INotifyKilled.Killed(Actor self, AttackInfo e) { DisposeDummyActor(self); }

		void INotifySold.Selling(Actor self) { DisposeDummyActor(self); }

		void INotifySold.Sold(Actor self) { }

		void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			DisposeDummyActor(self);
		}
	}
}
