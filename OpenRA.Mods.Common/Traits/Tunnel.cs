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
using OpenRA.Traits;
using OpenRA.Mods.Common.Orders;
using System.Collections.Generic;
using System.Drawing;
using OpenRA.Mods.Common.Activities;
using OpenRA.Activities;
using OpenRA.Graphics;

namespace OpenRA.Mods.Common.Traits
{
	public class TunnelInfo : ITraitInfo
	{
		public virtual object Create(ActorInitializer init) { return new Tunnel(init, this); }
	}

	public class Tunnel
	{
		readonly TunnelInfo info;

		public string ConnectedTunnel;

		public Tunnel(ActorInitializer init, TunnelInfo info)
		{
			this.info = info;
			ConnectedTunnel = init.Contains<ConnectedTunnelInit>() ? init.Get<ConnectedTunnelInit, string>() : "";
		}
	}

	public class ConnectedTunnelInit : IActorInit<string>
	{
		[FieldFromYamlKey] readonly string value = "";
		public ConnectedTunnelInit() { }
		public ConnectedTunnelInit(string init)
		{
			value = init;
		}

		public string Value(World world)
		{
			return value;
		}
	}

	public class TunnelPassengerInfo : ITraitInfo, Requires<UpgradeManagerInfo>
	{
		public readonly string TargetCursor = "enter";

		[VoiceReference] public readonly string Voice = "Action";

		[UpgradeGrantedReference]
		[Desc("The upgrades to grant to self while in tunnel.")]
		public readonly string[] TunnelUpgrades = { };

		public virtual object Create(ActorInitializer init) { return new TunnelPassenger(init, this); }
	}

	public class TunnelPassenger : IIssueOrder, IResolveOrder, IOrderVoice, INotifyCreated
	{
		readonly TunnelPassengerInfo info;
		UpgradeManager upgradeManager;

		public TunnelPassenger(ActorInitializer init, TunnelPassengerInfo info)
		{
			this.info = info;
		}

		void INotifyCreated.Created(Actor self)
		{
			upgradeManager = self.TraitOrDefault<UpgradeManager>();
		}

		IEnumerable<IOrderTargeter> IIssueOrder.Orders
		{
			get { yield return new EnterTunnelOrderTargeter(info); }
		}

		Order IIssueOrder.IssueOrder(Actor self, IOrderTargeter order, Target target, bool queued)
		{
			if (order.OrderID != "EnterTunnel")
				return null;

			if (target.Type == TargetType.FrozenActor)
				return new Order(order.OrderID, self, queued) { ExtraData = target.FrozenActor.ID };

			return new Order(order.OrderID, self, queued) { TargetActor = target.Actor };
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != "EnterTunnel")
				return;

			var target = self.ResolveFrozenActorOrder(order, Color.Green);
			if (target.Type != TargetType.Actor)
				return;

			if (!order.Queued)
				self.CancelActivity();

			self.SetTargetLine(target, Color.Green);
			self.QueueActivity(new EnterTunnel(self, target.Actor, upgradeManager));
		}

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order)
		{
			if (order.OrderString != "EnterTunnel")
				return null;
			return info.Voice;
		}

		class EnterTunnelOrderTargeter : UnitOrderTargeter
		{
			readonly TunnelPassengerInfo info;

			public EnterTunnelOrderTargeter(TunnelPassengerInfo info)
				: base("EnterTunnel", 8, info.TargetCursor, true, true)
			{
				this.info = info;
			}

			public override bool CanTargetActor(Actor self, Actor target, TargetModifiers modifiers, ref string cursor)
			{
				return target.TraitOrDefault<Tunnel>() != null;
			}

			public override bool CanTargetFrozenActor(Actor self, FrozenActor target, TargetModifiers modifiers, ref string cursor)
			{
				return true;
			}
		}

		class EnterTunnel : Enter
		{
			Actor tunnelIn;
			public EnterTunnel(Actor self, Actor target, UpgradeManager upgradeManager)
				: base(self, target, EnterBehaviour.Exit)
			{
				tunnelIn = target;
			}

			protected override void OnInside(Actor self)
			{
				Done(self);

				var tunnelOut = FindTunnelExit(self, tunnelIn);

				self.SetTargetLine(Target.FromActor(tunnelOut), Color.Green);
				self.QueueActivity(new TunnelMove(self, tunnelIn, tunnelOut));

			}

			Actor FindTunnelExit(Actor self, Actor tunnelIn)
			{
				var sma = self.World.WorldActor.Trait<SpawnMapActors>();

				var tunnelTrait = tunnelIn.TraitOrDefault<Tunnel>();
				return sma.Actors[tunnelTrait.ConnectedTunnel];
			}
		}

		class TunnelMove : Activity
		{
			Activity inner;
			UpgradeManager upgradeManager;
			TunnelPassengerInfo info;
			Actor tunnelIn, tunnelOut;

			enum State { Enter, Inside, MoveIntoWorld }
			State state = State.Enter;

			IMove move;
			ExitInfo exit;

			public TunnelMove(Actor self, Actor tunnelIn, Actor tunnelOut)
			{
				info = self.Info.TraitInfoOrDefault<TunnelPassengerInfo>();
				upgradeManager = self.TraitOrDefault<UpgradeManager>();
				this.tunnelIn = tunnelIn;
				this.tunnelOut = tunnelOut;

				move = self.TraitOrDefault<IMove>();

				exit = tunnelOut.Info.TraitInfoOrDefault<ExitInfo>();

				var toPos = tunnelOut.CenterPosition + exit.SpawnOffset;
				var fromPos = self.CenterPosition;

				inner = move.VisualMove(self, fromPos, toPos);
			}

			public override Activity Tick(Actor self)
			{
				if (state == State.Enter && upgradeManager != null)
				{
					foreach (var u in info.TunnelUpgrades)
						upgradeManager.GrantUpgrade(self, u, this);
					state = State.Inside;
				}

				if (state == State.Inside && inner == null)
				{
					//TODO: stay in activity if exit is blocked
					if (!CanUseExit(self, exit))
						return this;

					if (upgradeManager != null)
					{
						foreach (var u in info.TunnelUpgrades)
							upgradeManager.RevokeUpgrade(self, u, this);
					}

					var exitCell = tunnelOut.Location + exit.ExitCell;
					var rallyPoint = tunnelOut.TraitOrDefault<RallyPoint>().Location;

					inner = move.MoveIntoWorld(self, exitCell);
					self.QueueActivity(new AttackMoveActivity(self, move.MoveTo(rallyPoint, 1)));

					self.SetTargetLine(Target.FromCell(self.World, rallyPoint), Color.Green);
					state = State.MoveIntoWorld;
				}

				if (state == State.MoveIntoWorld && inner == null)
					return NextActivity;

				inner = inner.Tick(self);
				return this;
			}

			bool CanUseExit(Actor self, ExitInfo s)
			{
				var mobileInfo = self.Info.TraitInfoOrDefault<MobileInfo>();

				self.NotifyBlocker(self.Location + s.ExitCell);

				return mobileInfo == null ||
					mobileInfo.CanEnterCell(self.World, self, tunnelOut.Location + s.ExitCell, self);
			}

			public override IEnumerable<Target> GetTargets(Actor self)
			{
				yield return Target.FromPos(tunnelOut.CenterPosition);
			}

			// Cannot be cancelled
			public override void Cancel(Actor self) { }
		}
	}


}
