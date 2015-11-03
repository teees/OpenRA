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
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenRA.Activities;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.TS.Traits
{
	public class SubterraneanInfo : IPositionableInfo, IOccupySpaceInfo, IMoveInfo, ICruiseAltitudeInfo,
		UsesInit<LocationInit>, UsesInit<FacingInit>
	{
		public readonly WDist CruiseDepth = new WDist(1280);
		public readonly int Speed = 1;
		public readonly HashSet<string> EmergableTerrainTypes = new HashSet<string>();

		[Desc("Can the actor be ordered to move in to shroud?")]
		public readonly bool MoveIntoShroud = true;

		public virtual object Create(ActorInitializer init) { return new Subterranean(init, this); }
		public WDist GetCruiseAltitude() { return CruiseDepth; }

		[VoiceReference] public readonly string Voice = "Action";

		[UpgradeGrantedReference]
		[Desc("The upgrades to grant to self while airborne.")]
		public readonly string[] SubterraneanUpgrades = { };

		[Desc("How fast this actor ascends or descends when using vertical take off/landing.")]
		public readonly WDist AltitudeVelocity = new WDist(43);

		[Desc("Sound to play when the actor is taking off.")]
		public readonly string TakeoffSound = null;

		[Desc("Sound to play when the actor is landing.")]
		public readonly string LandingSound = null;

		public IReadOnlyDictionary<CPos, SubCell> OccupiedCells(ActorInfo info, CPos location, SubCell subCell = SubCell.Any) { return new ReadOnlyDictionary<CPos, SubCell>(); }
		bool IOccupySpaceInfo.SharesCell { get { return false; } }
	}

	public class Subterranean : ISync, IMove, IIssueOrder, IResolveOrder, IOrderVoice,
		INotifyCreated, INotifyAddedToWorld
	{
		static readonly Pair<CPos, SubCell>[] NoCells = { };

		public readonly bool IsPlane;
		public readonly SubterraneanInfo Info;
		readonly Actor self;

		UpgradeManager um;

		[Sync] public int Facing { get; set; }
		[Sync] public WPos CenterPosition { get; private set; }
		public CPos TopLeft { get { return self.World.Map.CellContaining(CenterPosition); } }
		public IDisposable Reservation;

		bool underground;
		bool IsUnderground
		{
			get
			{
				return underground;
			}

			set
			{
				if (underground == value)
					return;
				underground = value;
				if (um != null)
				{
					if (underground)
						foreach (var u in Info.SubterraneanUpgrades)
							um.GrantUpgrade(self, u, this);
					else
						foreach (var u in Info.SubterraneanUpgrades)
							um.RevokeUpgrade(self, u, this);
				}
			}
		}

		public Subterranean(ActorInitializer init, SubterraneanInfo info)
		{
			Info = info;
			self = init.Self;

			if (init.Contains<LocationInit>())
				SetPosition(self, init.Get<LocationInit, CPos>());

			if (init.Contains<CenterPositionInit>())
				SetPosition(self, init.Get<CenterPositionInit, WPos>());

		}

		public void Created(Actor self) { um = self.TraitOrDefault<UpgradeManager>(); }

		public void AddedToWorld(Actor self)
		{
			self.World.ActorMap.AddInfluence(self, this);
			self.World.ActorMap.AddPosition(self, this);
			self.World.ScreenMap.Add(self);
		}

		public int MovementSpeed
		{
			get
			{
				var modifiers = self.TraitsImplementing<ISpeedModifier>()
					.Select(m => m.GetSpeedModifier());
				return Util.ApplyPercentageModifiers(Info.Speed, modifiers);
			}
		}

		public IEnumerable<Pair<CPos, SubCell>> OccupiedCells() { return NoCells; }

		public bool CanEmerge(CPos cell)
		{
			if (!self.World.Map.Contains(cell))
				return false;

			if (self.World.ActorMap.AnyActorsAt(cell))
				return false;

			var type = self.World.Map.GetTerrainInfo(cell).Type;
			return Info.EmergableTerrainTypes.Contains(type);
		}

		#region Implement IMove

		public Activity MoveTo(CPos cell, int nearEnough)
		{
			return new HeliFly(self, Target.FromCell(self.World, cell));
		}

		public Activity MoveTo(CPos cell, Actor ignoredActor)
		{
			return new HeliFly(self, Target.FromCell(self.World, cell));
		}

		public Activity MoveWithinRange(Target target, WDist range)
		{
			if (IsPlane)
				return new FlyAndContinueWithCirclesWhenIdle(self, target, WDist.Zero, range);

			return new HeliFly(self, target, WDist.Zero, range);
		}

		public Activity MoveWithinRange(Target target, WDist minRange, WDist maxRange)
		{
			return new HeliFly(self, target, minRange, maxRange);
		}

		public Activity MoveFollow(Actor self, Target target, WDist minRange, WDist maxRange)
		{
			return new Follow(self, target, minRange, maxRange);
		}

		public Activity MoveIntoWorld(Actor self, CPos cell, SubCell subCell = SubCell.Any)
		{
			return new HeliFly(self, Target.FromCell(self.World, cell, subCell));
		}

		public Activity MoveToTarget(Actor self, Target target)
		{
			return new HeliFly(self, target);
		}

		public Activity MoveIntoTarget(Actor self, Target target)
		{
			return new HeliLand(self, false);
		}

		public Activity VisualMove(Actor self, WPos fromPos, WPos toPos)
		{
			// TODO: Ignore repulsion when moving
			if (IsPlane)
				return Util.SequenceActivities(
					new CallFunc(() => SetVisualPosition(self, fromPos)),
					new Fly(self, Target.FromPos(toPos)));

			return Util.SequenceActivities(new CallFunc(() => SetVisualPosition(self, fromPos)),
				new HeliFly(self, Target.FromPos(toPos)));
		}

		public CPos NearestMoveableCell(CPos cell) { return cell; }

		public bool IsMoving { get { return self.World.Map.DistanceAboveTerrain(CenterPosition).Length > 0; } set { } }

		public bool CanEnterTargetNow(Actor self, Target target)
		{
			if (target.Positions.Any(p => self.World.ActorMap.GetActorsAt(self.World.Map.CellContaining(p)).Any(a => a != self && a != target.Actor)))
				return false;

			var res = target.Actor.TraitOrDefault<Reservable>();
			if (res == null)
				return true;

			return true;
		}

		#endregion

		#region Implement order interfaces

		public IEnumerable<IOrderTargeter> Orders
		{
			get
			{
				yield return new EnterAlliedActorTargeter<BuildingInfo>("Enter", 5,
					target => AircraftCanEnter(target), target => !Reservable.IsReserved(target));

				yield return new AircraftMoveOrderTargeter(Info);
			}
		}

		public Order IssueOrder(Actor self, IOrderTargeter order, Target target, bool queued)
		{
			if (order.OrderID == "Enter")
				return new Order(order.OrderID, self, queued) { TargetActor = target.Actor };

			if (order.OrderID == "Move")
				return new Order(order.OrderID, self, queued) { TargetLocation = self.World.Map.CellContaining(target.CenterPosition) };

			return null;
		}

		public string VoicePhraseForOrder(Actor self, Order order)
		{
			switch (order.OrderString)
			{
				case "Move":
				case "Enter":
				case "ReturnToBase":
				case "Stop":
					return Info.Voice;
				default: return null;
			}
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "Move")
			{
				var cell = self.World.Map.Clamp(order.TargetLocation);

				if (!Info.MoveIntoShroud && !self.Owner.Shroud.IsExplored(cell))
					return;

				if (!order.Queued)
					UnReserve();

				var target = Target.FromCell(self.World, cell);

				self.SetTargetLine(target, Color.Green);

				if (IsPlane)
					self.QueueActivity(order.Queued, new FlyAndContinueWithCirclesWhenIdle(self, target));
				else
					self.QueueActivity(order.Queued, new HeliFlyAndLandWhenIdle(self, target, Info));
			}
			else if (order.OrderString == "Enter")
			{
				if (!order.Queued)
					UnReserve();

				if (Reservable.IsReserved(order.TargetActor))
				{
					if (IsPlane)
						self.QueueActivity(new ReturnToBase(self));
					else
						self.QueueActivity(new HeliReturnToBase(self));
				}
				else
				{
					self.SetTargetLine(Target.FromActor(order.TargetActor), Color.Green);

					if (IsPlane)
					{
						self.QueueActivity(order.Queued, Util.SequenceActivities(
							new ReturnToBase(self, order.TargetActor),
							new ResupplyAircraft(self)));
					}
					else
					{
						var res = order.TargetActor.TraitOrDefault<Reservable>();
						if (res != null)
							Reservation = res.Reserve(order.TargetActor, self, this);

						Action enter = () =>
						{
							var exit = order.TargetActor.Info.TraitInfos<ExitInfo>().FirstOrDefault();
							var offset = (exit != null) ? exit.SpawnOffset : WVec.Zero;

							self.QueueActivity(new HeliFly(self, Target.FromPos(order.TargetActor.CenterPosition + offset)));
							self.QueueActivity(new Turn(self, Info.InitialFacing));
							self.QueueActivity(new HeliLand(self, false));
							self.QueueActivity(new ResupplyAircraft(self));
							self.QueueActivity(new TakeOff(self));
						};

						self.QueueActivity(order.Queued, new CallFunc(enter));
					}
				}
			}
			else if (order.OrderString == "Stop")
			{
				self.CancelActivity();
				if (GetActorBelow() != null)
				{
					self.QueueActivity(new ResupplyAircraft(self));
					return;
				}

				UnReserve();

				// TODO: Implement INotifyBecomingIdle instead
				if (!IsPlane && Info.LandWhenIdle)
				{
					if (Info.TurnToLand)
						self.QueueActivity(new Turn(self, Info.InitialFacing));

					self.QueueActivity(new HeliLand(self, true));
				}
			}
			else if (order.OrderString == "ReturnToBase")
			{
				UnReserve();
				self.CancelActivity();
				if (IsPlane)
					self.QueueActivity(new ReturnToBase(self));
				else
					self.QueueActivity(new HeliReturnToBase(self));

				self.QueueActivity(new ResupplyAircraft(self));
			}
			else
				UnReserve();
		}

		#endregion

	}
}
